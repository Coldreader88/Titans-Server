using System;
using System.Collections.Generic;
using System.Linq;
using Common.Characters;
using Common.Database;
using Common.Network.Packets;
using SmartEngine.Core;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Packets.Client;
using TitansUC.GameServer.World;

namespace TitansUC.GameServer.Network.Client
{
    /// <summary>
    /// One client on the game server.
    ///
    /// Order of a login, from UCGOZone-Login.pcap: 0x41 login, 0x38 register, 0x13 server time,
    /// 0x16 for every container and item, 0x70 occupation cities, 0x00 position record, then 0x03 about
    /// once a second (and 0x02 as the player moves), with 0x06 / 0x0A for each player it sees.
    /// Java reference: mina_gameserver GameSession.java and the handlers in net/packets/incoming.
    /// </summary>
    public class UCGameSession : Session<GSOpcode>
    {
        private readonly object sync = new object();

        /// <summary>
        /// Source of machine ids. The official server filled in its own nonzero id per entity in the
        /// position records (0x1268, 0x1269, ... in UCGOZone-Login.pcap); the client sends 0xFFFF.
        /// </summary>
        private static int nextMachineID;

        public ushort MachineID { get; private set; }

        /// <summary>
        /// The session key the client logged in with; a flight between Earth and Space reuses it.
        /// </summary>
        private uint sessionKey;

        /// <summary>
        /// What the client sent when it last bought a vehicle to ride away (a shuttle's take-off point).
        /// </summary>
        private Common.Characters.Transport lastTransport;
        private DateTime lastSave = DateTime.UtcNow;

        /// <summary>
        /// The character being played, null until 0x41 succeeds.
        /// </summary>
        public Character Character { get; private set; }

        public uint AccountID { get; private set; }

        public byte AccountLevel { get; private set; }

        public uint CharacterID { get { return Character != null ? Character.ClientID : 0; } }

        public PlayerInventory Inventory { get; private set; }

        /// <summary>
        /// The last position record, from the database until the client sends its own.
        /// </summary>
        public CoordData Coord { get; private set; }

        public bool InGame { get { return Character != null; } }

        public override void OnDisconnect()
        {
            if (Character == null)
            {
                return;
            }

            if (GameWorld.Instance.Remove(this))
            {
                Save();
            }
            Logger.ShowInfo(string.Format("{0} left the game ({1} players online).", Character.Name, GameWorld.Instance.Count));
        }

        /// <summary>
        /// 0x41: check the session key from the Lobby, load the character and put it in the world.
        /// </summary>
        public void OnLoginGame(CM_LOGIN_GAME p)
        {
            if (InGame)
            {
                Logger.ShowWarning(string.Format("{0} sent a second game login, ignoring it.", Character.Name));
                return;
            }

            if (GameWorld.Instance.Closed)
            {
                RefuseLogin(string.Format("the server is closed for maintenance (character {0})", p.CharacterID));
                return;
            }

            try
            {
                uint accountID = 0;
                if (Configuration.Instance.CheckSessionKey)
                {
                    accountID = LoginSessionDatabase.Instance.Verify(p.SessionKey, p.CharacterID);
                    if (accountID == 0)
                    {
                        RefuseLogin(string.Format("character {0} has no valid session key from the Lobby", p.CharacterID));
                        return;
                    }
                }

                var character = CharacterDatabase.Instance.LoadCharacterByClientID(p.CharacterID);
                if (character == null)
                {
                    RefuseLogin(string.Format("character {0} does not exist", p.CharacterID));
                    return;
                }
                if (accountID != 0 && character.AccountID != accountID)
                {
                    RefuseLogin(string.Format("character {0} does not belong to account {1}", character.Name, accountID));
                    return;
                }

                var items = CharacterDatabase.Instance.LoadItems(character);

                this.Character = character;
                this.AccountID = character.AccountID;
                this.AccountLevel = character.Access > 0 ? (byte)character.Access : (byte)Common.Account.Account.AccountLevel.PLAYER;
                this.Inventory = new PlayerInventory(character, items);
                this.MachineID = (ushort)(0x1000 + (System.Threading.Interlocked.Increment(ref nextMachineID) % 0xE000));
                this.Coord = CoordData.FromCharacter(character, AccountLevel);
                this.Coord.MachineID = MachineID;
                this.sessionKey = p.SessionKey;

                // Arriving from a flight between Earth and Space: still in the shuttle.
                var flight = GameWorld.Instance.GetFlight(character.ClientID);
                if (flight != null && flight.Shuttle != null)
                {
                    Inventory.Resume(flight.Shuttle);
                    SetVehicle(Inventory.Piloting);
                }

                // Vehicles left on the ground were saved in the hangar; they are back there now.
                foreach (var vehicle in GameWorld.Instance.TakeGroundVehicles(character.ClientID))
                {
                    var taken = vehicle;
                    GameWorld.Instance.SendNear(taken.ClusterID, taken.X, taken.Y, BroadcastDistance,
                        () => new SM_UPDATE_ITEM_INFO(SM_UPDATE_ITEM_INFO.VehicleTaken, taken, character.ClientID));
                }

                var previous = GameWorld.Instance.Add(this);
                if (previous != null)
                {
                    Logger.ShowWarning(string.Format("{0} logged in again, dropping the older connection.", character.Name));
                    previous.Network.Disconnect();
                }

                Logger.ShowInfo(string.Format("{0} (character {1}, account {2}) entered the game ({3} players online).",
                    character.Name, character.ClientID, character.AccountID, GameWorld.Instance.Count));

                this.Network.SendPacket(new SM_LOGIN_GAME(true));
            }
            catch (Exception ex)
            {
                Logger.ShowError(ex);
                RefuseLogin("an error occurred");
            }
        }

        /// <summary>
        /// 0x38: confirm with the character id and account level.
        /// </summary>
        public void OnRegisterPlayer(CM_REGISTER_PLAYER p)
        {
            if (!CheckInGame("Register player"))
            {
                return;
            }
            this.Network.SendPacket(new SM_REGISTER_PLAYER(AccountLevel, CharacterID));
        }

        /// <summary>
        /// 0x13: send the server's Unix time.
        /// </summary>
        public void OnServerTime(CM_SERVER_TIME p)
        {
            this.Network.SendPacket(new SM_SERVER_TIME(GameWorld.UnixTime()));
        }

        /// <summary>
        /// 0x16: describe one of the character's containers or items.
        /// </summary>
        public void OnItemInfo(CM_ITEM_INFO p)
        {
            if (!CheckInGame("Item info"))
            {
                return;
            }

            var node = Inventory.Get(p.UniqueID);
            if (node == null)
            {
                // Answer anyway with an empty entry, so the client does not wait for it.
                Logger.ShowWarning(string.Format("{0} asked for unknown container or item {1:X8} (static id {2}).",
                    Character.Name, p.UniqueID, p.StaticID));
                node = new ItemNode(p.UniqueID, p.Format == ItemNode.Singleton ? ItemNode.Singleton : ItemNode.Multi, p.StaticID);
            }

            this.Network.SendPacket(new SM_ITEM_INFO(p, node));
        }

        /// <summary>
        /// 0x17: move an item between containers, or get in or out of a vehicle at the hangar.
        /// Nothing is sent back when the move is refused (as the Java server).
        /// </summary>
        public void OnMoveItem(CM_MOVE_ITEM p)
        {
            if (!CheckInGame("Move item"))
            {
                return;
            }

            switch (p.Section)
            {
                case CM_MOVE_ITEM.SectionRide:
                {
                    var vehicle = Inventory.Ride(p.ItemUniqueID, p.SourceUniqueID);
                    if (vehicle == null)
                    {
                        RefuseMove(p, "cannot get in that vehicle");
                        return;
                    }
                    SetVehicle(vehicle);
                    Logger.ShowInfo(string.Format("{0} got in {1} ({2}).", Character.Name, vehicle.Name, vehicle.StaticID));
                    this.Network.SendPacket(new SM_MOVE_ITEM(SM_MOVE_ITEM.Rode, p, null, -1));
                    SaveItems();
                    return;
                }
                case CM_MOVE_ITEM.SectionPutBack:
                {
                    var vehicle = Inventory.PutBack(p.ItemUniqueID, p.DestUniqueID);
                    if (vehicle == null)
                    {
                        RefuseMove(p, "is not piloting that vehicle");
                        return;
                    }
                    SetVehicle(null);
                    Logger.ShowInfo(string.Format("{0} put {1} back in the hangar.", Character.Name, vehicle.Name));
                    this.Network.SendPacket(new SM_MOVE_ITEM(SM_MOVE_ITEM.PutBack, p, null, -1));
                    SaveItems();
                    return;
                }
                case CM_MOVE_ITEM.SectionContainers:
                {
                    var result = Inventory.Move(p.ItemUniqueID, p.SourceUniqueID, p.DestUniqueID, p.Amount);
                    if (result == null)
                    {
                        RefuseMove(p, "invalid move");
                        return;
                    }
                    uint kind;
                    switch (result.Kind)
                    {
                        case MoveKind.Split: kind = SM_MOVE_ITEM.Split; break;
                        case MoveKind.Merged: kind = SM_MOVE_ITEM.Merged; break;
                        case MoveKind.AddedToStack: kind = SM_MOVE_ITEM.AddedToStack; break;
                        default: kind = SM_MOVE_ITEM.Moved; break;
                    }
                    this.Network.SendPacket(new SM_MOVE_ITEM(kind, p, result.Target, result.Item.StaticID));
                    SaveItems();
                    return;
                }
                default:
                    RefuseMove(p, "section " + p.Section + " is not implemented");
                    return;
            }
        }

        private void RefuseMove(CM_MOVE_ITEM p, string reason)
        {
            Logger.ShowWarning(string.Format("{0}: move of {1:X8} from {2:X8} to {3:X8} refused: {4}.",
                Character.Name, p.ItemUniqueID, p.SourceUniqueID, p.DestUniqueID, reason));
        }

        /// <summary>
        /// Updates the position record for getting in or out of a vehicle, as the official server did before
        /// the client's next 0x02 ("TGM-79 GM TRAINER in out of Hanger.pcap"): vehicle unique id and template,
        /// the update counter raised so other clients ask for the new looks, and the damage byte (0xFF on foot).
        /// </summary>
        private void SetVehicle(ItemNode vehicle)
        {
            lock (sync)
            {
                Coord.VehicleUniqueID = vehicle != null ? vehicle.UniqueID : 0;
                Coord.VehicleTemplateID = vehicle != null ? vehicle.StaticID : -1;
                Coord.Damage = vehicle != null ? (byte)0 : (byte)0xFF;
                Coord.UpdateCounter++;
            }
        }

        /// <summary>
        /// 0x70: send the occupation cities.
        /// </summary>
        public void OnOccupationCityInfoList(CM_OCCUPATION_CITY_INFO_LIST p)
        {
            this.Network.SendPacket(new SM_OCCUPATION_CITY_INFO_LIST(GameWorld.OccupationTimes()));
        }

        /// <summary>
        /// 0x00: the client's first position record in the world.
        /// </summary>
        public void OnRegistCoordMgr(CM_REGIST_COORD_MGR p)
        {
            if (!CheckInGame("Coordinate registration"))
            {
                return;
            }

            UpdateCoord(p.Coord);
            this.Network.SendPacket(new SM_REGIST_COORD_MGR());
        }

        /// <summary>
        /// 0x02: the player moved.
        /// </summary>
        public void OnPlayerCoordUpdate(CM_PLAYER_COORD_UPDATE p)
        {
            if (!CheckInGame("Coordinate update"))
            {
                return;
            }

            UpdateCoord(p.Coord);
        }

        /// <summary>
        /// 0x03: send the player's own record and those of the players around them.
        /// </summary>
        public void OnPlayerCoordDataList(CM_PLAYER_COORD_DATA_LIST p)
        {
            if (!CheckInGame("Coordinate list"))
            {
                return;
            }

            CoordData self;
            lock (sync)
            {
                Coord.X = p.X;
                Coord.Y = p.Y;
                Coord.Z = p.Z;
                self = Coord;
            }

            int radius = Configuration.Instance.ViewDistance > 0
                ? Configuration.Instance.ViewDistance
                : (p.Radius > 0 && p.Radius < int.MaxValue ? (int)p.Radius : 8000);

            var others = GameWorld.Instance.Visible(this, radius).Select(s => s.Coord).ToList();

            this.Network.SendPacket(new SM_PLAYER_COORD_DATA_LIST(AccountID, self, others));

            SaveIfDue();
        }

        /// <summary>
        /// 0x05: the items of one list lying around a point.
        /// </summary>
        public void OnSpaceCircuitItem(CM_SPACE_CIRCUIT_ITEM p)
        {
            if (!CheckInGame("Ground item list"))
            {
                return;
            }
            int radius = p.Radius > 0 && p.Radius < int.MaxValue ? (int)p.Radius : BroadcastDistance;
            var items = GameWorld.Instance.GroundNear((ushort)Character.Zone, p.X, p.Y, radius, p.List);
            this.Network.SendPacket(new SM_SPACE_CIRCUIT_ITEM(p.List, items));
        }

        /// <summary>
        /// 0x23: drop an item on the ground, or get out of the vehicle and leave it there.
        /// Nothing is sent back when it is refused.
        /// </summary>
        public void OnSpacePlacedItem(CM_SPACE_PLACED_ITEM p)
        {
            if (!CheckInGame("Drop item"))
            {
                return;
            }

            ItemNode placed = null;
            uint action = 0;
            if (p.CharacterID != CharacterID)
            {
                RefuseGround("drop", p.ItemUniqueID, "not their character");
                return;
            }
            if (p.MiniOp == CM_SPACE_PLACED_ITEM.GetOff)
            {
                placed = Inventory.GetOff(p.ItemUniqueID);
                action = SM_UPDATE_ITEM_INFO.VehicleLeft;
            }
            else if (p.MiniOp == CM_SPACE_PLACED_ITEM.DropItem)
            {
                placed = Inventory.TakeForDrop(p.ItemUniqueID, p.ContainerUniqueID, p.Amount);
                action = SM_UPDATE_ITEM_INFO.ItemDropped;
            }
            if (placed == null)
            {
                RefuseGround("drop", p.ItemUniqueID, "mini op " + p.MiniOp + ", invalid item, container or amount");
                return;
            }

            var ground = new GroundItem(placed, (ushort)Character.Zone, p.X, p.Y, p.Z, p.Rotation, CharacterID);
            GameWorld.Instance.Place(ground);
            if (action == SM_UPDATE_ITEM_INFO.VehicleLeft)
            {
                SetVehicle(null);
                Logger.ShowInfo(string.Format("{0} got out of {1} and left it at {2}, {3}, {4}.", Character.Name, placed.Name, p.X, p.Y, p.Z));
            }
            else
            {
                Logger.ShowInfo(string.Format("{0} dropped {1} x {2} at {3}, {4}, {5}.", Character.Name, placed.StaticID, placed.Amount, p.X, p.Y, p.Z));
            }

            this.Network.SendPacket(new SM_SPACE_PLACED_ITEM(p, placed));
            BroadcastGround(action, ground);
            SaveItems();
        }

        /// <summary>
        /// 0x24: pick an item up from the ground, or get in one of the player's vehicles standing there.
        /// Anyone can pick up items; only the owner can take a vehicle. Nothing is sent back when it is refused.
        /// </summary>
        public void OnSpacePickupItem(CM_SPACE_PICKUP_ITEM p)
        {
            if (!CheckInGame("Pick up item"))
            {
                return;
            }
            if (p.CharacterID != CharacterID ||
                (p.MiniOp != CM_SPACE_PICKUP_ITEM.PickUpItem && p.MiniOp != CM_SPACE_PICKUP_ITEM.GetIn))
            {
                RefuseGround("pick up", p.ItemUniqueID, "mini op " + p.MiniOp + " for character " + p.CharacterID);
                return;
            }

            bool vehicle = p.MiniOp == CM_SPACE_PICKUP_ITEM.GetIn;
            var zone = (ushort)Character.Zone;
            var ground = GameWorld.Instance.Take(p.ItemUniqueID, g => g.ClusterID == zone &&
                (vehicle ? g.IsVehicle && !g.IsWreck && g.OwnerID == CharacterID : !g.IsVehicle));
            if (ground == null)
            {
                RefuseGround("pick up", p.ItemUniqueID, "nothing there they can take");
                return;
            }

            bool ok = vehicle ? Inventory.Board(ground.Node, p.DestUniqueID) : Inventory.PickUp(ground.Node, p.DestUniqueID);
            if (!ok)
            {
                GameWorld.Instance.Place(ground);
                RefuseGround("pick up", p.ItemUniqueID, "destination " + p.DestUniqueID.ToString("X8") + " cannot take it");
                return;
            }

            if (vehicle)
            {
                SetVehicle(ground.Node);
                Logger.ShowInfo(string.Format("{0} got in {1} ({2}).", Character.Name, ground.Node.Name, ground.Node.StaticID));
            }
            else
            {
                Logger.ShowInfo(string.Format("{0} picked up {1} x {2}.", Character.Name, ground.Node.StaticID, ground.Node.Amount));
            }

            this.Network.SendPacket(new SM_SPACE_PICKUP_ITEM(p, ground.Node));
            BroadcastGround(vehicle ? SM_UPDATE_ITEM_INFO.VehicleTaken : SM_UPDATE_ITEM_INFO.ItemPickedUp, ground);
            SaveItems();
        }

        /// <summary>
        /// 0x21: buy from a shop. The price comes from the item templates; the shop lists are the client's, so
        /// anything the templates sell can be bought. Nothing is sent back when it is refused.
        /// </summary>
        public void OnBuyItem(CM_BUY_ITEM p)
        {
            if (!CheckInGame("Buy item"))
            {
                return;
            }

            var template = ItemTemplates.Get(p.StaticID);
            if (p.CharacterID != CharacterID || template == null || !template.ForSale)
            {
                RefuseShop("buy", p.StaticID, template == null ? "unknown item" : "not for sale");
                return;
            }

            long total = (long)template.Price * p.Amount;
            if (total > int.MaxValue)
            {
                RefuseShop("buy", p.StaticID, "amount " + p.Amount);
                return;
            }

            var result = Inventory.Buy(template, p.DestUniqueID, p.Amount, (int)total);
            if (result == null)
            {
                RefuseShop("buy", p.StaticID, "container " + p.DestUniqueID.ToString("X8") + " cannot take it, or not enough money");
                return;
            }
            if (result.Item == Inventory.Piloting)
            {
                SetVehicle(result.Item);
                lastTransport = p.Transport;
            }

            Logger.ShowInfo(string.Format("{0} bought {1} x {2} for {3}.", Character.Name, template.Name, p.Amount, total));
            this.Network.SendPacket(new SM_BUY_ITEM(p, result, (int)total));
            SaveItems();
        }

        /// <summary>
        /// 0x22: sell to a shop, for half the shop price (the official prices varied between about half and
        /// all of it).
        /// </summary>
        public void OnSellItem(CM_SELL_ITEM p)
        {
            if (!CheckInGame("Sell item"))
            {
                return;
            }

            var template = ItemTemplates.Get(p.StaticID);
            var item = Inventory.Get(p.ItemUniqueID);
            if (p.CharacterID != CharacterID || template == null || item == null || item.StaticID != p.StaticID ||
                template.Price <= 0 || template.Price >= ItemTemplates.NotForSale)
            {
                RefuseShop("sell", p.StaticID, template == null ? "unknown item" : "no price");
                return;
            }

            int unitPrice = template.Price / 2;
            int amount = template.IsVehicle ? 1 : p.Amount;
            var soldAll = Inventory.Sell(p.ItemUniqueID, p.ContainerUniqueID, amount, unitPrice);
            if (soldAll == null)
            {
                RefuseShop("sell", p.StaticID, "not in that container, or not that many");
                return;
            }

            Logger.ShowInfo(string.Format("{0} sold {1} x {2} for {3}.", Character.Name, template.Name, amount, unitPrice * amount));
            this.Network.SendPacket(new SM_SELL_ITEM(p, soldAll.Value, unitPrice * amount));
            SaveItems();
        }

        /// <summary>
        /// 0x15: throw an item away (a bought car or shuttle once the client is done with it).
        /// </summary>
        public void OnDeleteItem(CM_DELETE_ITEM p)
        {
            if (!CheckInGame("Delete item"))
            {
                return;
            }

            bool wasPiloting = Inventory.Piloting != null && Inventory.Piloting.UniqueID == p.ItemUniqueID;
            var deleted = p.CharacterID == CharacterID ? Inventory.Delete(p.ItemUniqueID, p.ContainerUniqueID) : null;
            if (deleted == null)
            {
                RefuseShop("delete", (int)p.ItemUniqueID, "not theirs");
                return;
            }
            if (wasPiloting)
            {
                SetVehicle(null);
            }

            Logger.ShowInfo(string.Format("{0} threw away {1} ({2}).", Character.Name, deleted.Name, deleted.StaticID));
            this.Network.SendPacket(new SM_ECHO(GSOpcode.SM_DELETE_ITEM, p.Body));
            SaveItems();
        }

        /// <summary>
        /// 0x19: move money to or from the bank (the credit container).
        /// </summary>
        public void OnUpdateDeposit(CM_UPDATE_DEPOSIT p)
        {
            if (!CheckInGame("Deposit"))
            {
                return;
            }

            bool toBank = p.Direction == CM_UPDATE_DEPOSIT.ToBank;
            if (p.CharacterID != CharacterID || (!toBank && p.Direction != CM_UPDATE_DEPOSIT.FromBank) ||
                !Inventory.TransferMoney(toBank, p.Amount))
            {
                RefuseShop(toBank ? "deposit" : "withdrawal", p.Amount, "not enough money");
                return;
            }

            this.Network.SendPacket(new SM_ECHO(GSOpcode.SM_UPDATE_DEPOSIT, p.Body));
            SaveItems();
        }

        /// <summary>
        /// 0x18: repair a vehicle to full health. Price: a tenth of the vehicle's shop price for the share of
        /// health it lost (a guess; the official prices depended on the damage).
        /// </summary>
        public void OnPayRepair(CM_PAY_REPAIR p)
        {
            if (!CheckInGame("Repair"))
            {
                return;
            }

            var vehicle = Inventory.Get(p.VehicleUniqueID);
            if (p.CharacterID != CharacterID || vehicle == null || vehicle.MaxHealth <= 0)
            {
                RefuseShop("repair", (int)p.VehicleUniqueID, "not their vehicle");
                return;
            }

            var template = ItemTemplates.Get(vehicle.StaticID);
            int shopPrice = template != null && template.ForSale ? template.Price : 0;
            int price = (int)((long)shopPrice * (vehicle.MaxHealth - vehicle.Health) / vehicle.MaxHealth / 10);
            if (!Inventory.Repair(p.VehicleUniqueID, price))
            {
                RefuseShop("repair", vehicle.StaticID, "not enough money");
                return;
            }

            this.Network.SendPacket(new SM_PAY_REPAIR(p, price));
            SaveItems();
        }

        /// <summary>
        /// 0x40: the player takes off for the other side (Earth or Space). This server handles both, so the
        /// client is sent back here (TransferHost / TransferPort); it logs in again with the same session key
        /// and arrives in the other cluster.
        /// </summary>
        public void OnReserveAnotherGameFE(CM_RESERVE_ANOTHER_GAME_FE p)
        {
            if (!CheckInGame("Flight"))
            {
                return;
            }
            if (p.CharacterID != CharacterID || (p.Cluster != 1 && p.Cluster != 2) || p.Cluster == (ushort)Character.Zone)
            {
                Logger.ShowWarning(string.Format("{0}: flight to cluster {1} refused (in cluster {2}).", Character.Name, p.Cluster, Character.Zone));
                return;
            }

            GameWorld.Instance.StartFlight(CharacterID, new Flight
            {
                Cluster = p.Cluster,
                Shuttle = Inventory.Piloting,
                Transport = lastTransport ?? new Common.Characters.Transport { A = -1, B = -1 },
            });
            Character.Zone = (Common.Characters.Zone)p.Cluster;
            if (Configuration.Instance.CheckSessionKey)
            {
                LoginSessionDatabase.Instance.Refresh(sessionKey, CharacterID);
            }

            Logger.ShowInfo(string.Format("{0} takes off for {1}.", Character.Name, p.Cluster == 2 ? "Space" : "Earth"));
            this.Network.SendPacket(new SM_RESERVE_ANOTHER_GAME_FE(CharacterID, p.Cluster,
                Configuration.Instance.TransferHost, Configuration.Instance.TransferPort));
            Save();
        }

        /// <summary>
        /// 0x5F: the player info, asked for after a flight.
        /// </summary>
        public void OnGCPlayerInfo(CM_GC_PLAYER_INFO p)
        {
            if (!CheckInGame("Player info"))
            {
                return;
            }

            var flight = GameWorld.Instance.GetFlight(CharacterID);
            GameWorld.Instance.EndFlight(CharacterID);
            int vehicle = flight != null && flight.Shuttle != null ? flight.Shuttle.StaticID : 0;
            this.Network.SendPacket(new SM_GC_PLAYER_INFO(AccountID, Character, vehicle, flight != null ? flight.Transport : null));
        }

        /// <summary>
        /// 0x0F: the attacker's client says it fired the weapon in armament slot <c>Slot</c> at a player. The
        /// server decides the result (see <see cref="Combat"/>) and sends 0x800F to the attacker and the target
        /// and 0x8036 to everyone near the target.
        /// </summary>
        public void OnAttackResult(CM_ATTACK_RESULT p)
        {
            if (!CheckInGame("Attack") || p.AttackerID != CharacterID)
            {
                return;
            }
            var weapon = Inventory.Armament(p.Slot);
            var target = GameWorld.Instance.Get(p.TargetID);
            if (weapon == null || target == null || !target.InGame || target.Coord == null ||
                target.Coord.ClusterID != Coord.ClusterID)
            {
                Logger.ShowWarning(string.Format("{0}: attack on {1} with slot {2} refused: no weapon or no target.",
                    Character.Name, p.TargetID, p.Slot));
                return;
            }

            var r = target.TakeHit(weapon, p.Distance);
            if (r == null)
            {
                return;
            }
            uint attacker = CharacterID;
            this.Network.SendPacket(new SM_ATTACK_RESULT(attacker, target.CharacterID, r));
            if (target != this)
            {
                target.Network.SendPacket(new SM_ATTACK_RESULT(attacker, target.CharacterID, r));
            }
            var c = target.Coord;
            GameWorld.Instance.SendNear(c.ClusterID, c.X, c.Y, BroadcastDistance,
                () => new SM_ATTACK_RESULT_NEAR(attacker, target.CharacterID, r));
            AfterHit(target, r);
        }

        /// <summary>
        /// 0x67: an attack on several players at once (beam weapons, explosions): 0x8067 to the attacker and
        /// each target, 0x8068 to everyone near the point of impact.
        /// </summary>
        public void OnMultiAttackResult(CM_MULTI_ATTACK_RESULT p)
        {
            if (!CheckInGame("Multi attack") || p.AttackerID != CharacterID)
            {
                return;
            }
            var weapon = Inventory.Armament(p.Slot);
            if (weapon == null)
            {
                Logger.ShowWarning(string.Format("{0}: multi attack with empty slot {1} refused.", Character.Name, p.Slot));
                return;
            }

            var results = new List<KeyValuePair<uint, HitResult>>();
            var targets = new List<UCGameSession>();
            foreach (var t in p.Targets)
            {
                var target = GameWorld.Instance.Get(t.TargetID);
                if (target == null || !target.InGame || target.Coord == null || target.Coord.ClusterID != Coord.ClusterID ||
                    targets.Contains(target))
                {
                    continue;
                }
                var r = target.TakeHit(weapon, t.Distance);
                if (r != null)
                {
                    results.Add(new KeyValuePair<uint, HitResult>(target.CharacterID, r));
                    targets.Add(target);
                }
            }
            if (results.Count == 0)
            {
                return;
            }

            uint attacker = CharacterID;
            uint number = results[0].Value.AttackNumber;
            this.Network.SendPacket(new SM_MULTI_ATTACK_RESULT(attacker, weapon, number, p.X, p.Y, p.Z, results));
            foreach (var target in targets.Where(t => t != this))
            {
                target.Network.SendPacket(new SM_MULTI_ATTACK_RESULT(attacker, weapon, number, p.X, p.Y, p.Z, results));
            }
            GameWorld.Instance.SendNear(Coord.ClusterID, p.X, p.Y, BroadcastDistance,
                () => new SM_MULTI_ATTACK_RESULT_NEAR(attacker, weapon, number, p.X, p.Y, p.Z, results));
            for (int i = 0; i < targets.Count; i++)
            {
                AfterHit(targets[i], results[i].Value);
            }
        }

        /// <summary>
        /// Applies an attack on this player's piloted vehicle; null when they are on foot (or have no vehicle
        /// any more). Sets the damage and attack number of the position record, as the official server did.
        /// </summary>
        public HitResult TakeHit(ItemNode weapon, int distance)
        {
            var vehicle = Inventory.Piloting;
            if (vehicle == null || vehicle.Health <= 0)
            {
                return null;
            }
            var shield = Inventory.Armament(Combat.ShieldSlot);
            var shieldTemplate = shield != null ? ItemTemplates.Get(shield.StaticID) : null;
            var r = Combat.Attack(weapon, vehicle, shieldTemplate != null && shieldTemplate.IsShield ? shield : null, distance);
            if (r.Result != Combat.ResultMiss)
            {
                lock (sync)
                {
                    Coord.AttackNumber = (int)r.AttackNumber;
                    if (r.VehicleDamaged)
                    {
                        Coord.Damage = r.Percent;
                    }
                }
            }
            return r;
        }

        private void AfterHit(UCGameSession target, HitResult r)
        {
            if (r.ShieldBroken)
            {
                target.Inventory.DestroyArmament(Combat.ShieldSlot);
                target.RaiseUpdateCounter();
            }
            if (r.Destroyed)
            {
                Logger.ShowInfo(string.Format("{0} destroyed {1}'s {2}.", Character.Name, target.Character.Name, r.DamagedItem.Name));
                target.LoseVehicle();
            }
        }

        /// <summary>
        /// The piloted vehicle was destroyed: it becomes a wreck on the ground (0x8035 action 1, health 0) that
        /// lies for ten minutes and cannot be boarded, and the player is on foot.
        /// </summary>
        public void LoseVehicle()
        {
            var vehicle = Inventory.LoseVehicle();
            if (vehicle == null)
            {
                return;
            }
            var c = Coord;
            var rotation = new byte[]
            {
                (byte)(c.Tilt >> 8), (byte)c.Tilt, (byte)(c.Roll >> 8), (byte)c.Roll, (byte)(c.Direction >> 8), (byte)c.Direction,
            };
            var wreck = new GroundItem(vehicle, c.ClusterID, c.X, c.Y, c.Z, rotation, CharacterID) { IsWreck = true };
            GameWorld.Instance.Place(wreck);
            SetVehicle(null);
            BroadcastGround(SM_UPDATE_ITEM_INFO.ItemDropped, wreck);
            SaveItems();
        }

        public void RaiseUpdateCounter()
        {
            lock (sync)
            {
                Coord.UpdateCounter++;
            }
        }

        /// <summary>
        /// Packets a client may have sent to other players with 0x39: lock on (0x8010) and trade (0x802A-0x8031).
        /// </summary>
        private static bool CanRelay(uint opcode)
        {
            return opcode == (uint)GSOpcode.SM_LOCK_ON || (opcode >= 0x802A && opcode <= 0x8031);
        }

        /// <summary>
        /// 0x39: send a packet to the listed players (lock on, trade), unchanged.
        /// </summary>
        public void OnRelay(CM_RELAY p)
        {
            if (!CheckInGame("Relay"))
            {
                return;
            }
            if (!CanRelay(p.Opcode))
            {
                Logger.ShowWarning(string.Format("{0}: relay of opcode {1:X} refused.", Character.Name, p.Opcode));
                return;
            }
            foreach (var id in p.Receivers.Distinct())
            {
                var receiver = GameWorld.Instance.Get(id);
                if (receiver != null && receiver.InGame)
                {
                    receiver.Network.SendPacket(new SM_RAW(p.Opcode, p.Payload));
                }
            }
        }

        /// <summary>
        /// 0x3A: send a packet (fire effects and gestures, 0x803B) to everyone within the radius, the sender
        /// included, as the official server did.
        /// </summary>
        public void OnBroadcast(CM_BROADCAST p)
        {
            if (!CheckInGame("Broadcast"))
            {
                return;
            }
            if (p.Opcode != (uint)GSOpcode.SM_BROADCAST || p.CharacterID != CharacterID)
            {
                Logger.ShowWarning(string.Format("{0}: broadcast of opcode {1:X} refused.", Character.Name, p.Opcode));
                return;
            }
            int radius = float.IsNaN(p.Radius) || p.Radius <= 0 ? BroadcastDistance : (int)Math.Min(p.Radius, BroadcastDistance);
            var payload = p.Payload;
            GameWorld.Instance.SendNear(Coord.ClusterID, p.X, p.Y, radius, () => new SM_RAW(p.Opcode, payload));
        }

        /// <summary>
        /// 0x1B: equip or unequip the piloted vehicle's weapons and shield; the reply echoes the request, and the
        /// others see the new looks.
        /// </summary>
        public void OnEquipItem(CM_EQUIP_ITEM p)
        {
            if (!CheckInGame("Equip item"))
            {
                return;
            }
            if (p.SubOp != CM_EQUIP_ITEM.Armaments || p.Entries.Count == 0)
            {
                this.Network.SendPacket(new SM_ECHO(GSOpcode.SM_EQUIP_ITEM, p.Body));
                return;
            }
            if (p.CharacterID != CharacterID)
            {
                return;
            }
            foreach (var e in p.Entries)
            {
                bool ok = e.Action == CM_EQUIP_ITEM.Equip ? Inventory.Equip(e.ItemUniqueID, e.Slot) : Inventory.Unequip(e.Slot);
                if (!ok)
                {
                    Logger.ShowWarning(string.Format("{0}: equip of {1:X8} in slot {2} refused.", Character.Name, e.ItemUniqueID, e.Slot));
                    return;
                }
            }
            this.Network.SendPacket(new SM_ECHO(GSOpcode.SM_EQUIP_ITEM, p.Body));
            RaiseUpdateCounter();
            SaveItems();
        }

        /// <summary>
        /// 0x1D: reload a weapon from an ammunition stack; the reply echoes the request.
        /// </summary>
        public void OnReload(CM_USE_ITEM_WITH_TARGET p)
        {
            if (!CheckInGame("Reload"))
            {
                return;
            }
            if (p.CharacterID != CharacterID || !Inventory.Reload(p.AmmoUID, p.ContainerUID, p.WeaponUID, p.Rounds))
            {
                Logger.ShowWarning(string.Format("{0}: reload of {1:X8} refused.", Character.Name, p.WeaponUID));
                return;
            }
            this.Network.SendPacket(new SM_ECHO(GSOpcode.SM_USE_ITEM_WITH_TARGET, p.Body));
            SaveItems();
        }

        /// <summary>
        /// 0x1C: use an ER kit on the piloted vehicle.
        /// </summary>
        public void OnUseItemBuff(CM_USE_ITEM_BUFF p)
        {
            if (!CheckInGame("Use item"))
            {
                return;
            }
            var vehicle = Inventory.Piloting;
            int repaired = p.CharacterID == CharacterID ? Inventory.UseRepairKit(p.ItemUID, p.ContainerUID) : -1;
            this.Network.SendPacket(new SM_USE_ITEM_BUFF(p, vehicle != null ? vehicle.UniqueID : 0, repaired, repaired >= 0));
            if (repaired >= 0)
            {
                lock (sync)
                {
                    Coord.Damage = Combat.DamagePercent(vehicle.Health, vehicle.MaxHealth);
                }
                SaveItems();
            }
        }

        private void RefuseShop(string what, int id, string reason)
        {
            Logger.ShowWarning(string.Format("{0}: {1} of {2} refused: {3}.", Character.Name, what, id, reason));
        }

        /// <summary>
        /// How far ground item events reach (and the 0x05 radius when the client sends none).
        /// </summary>
        private static int BroadcastDistance
        {
            get { return Configuration.Instance.ViewDistance > 0 ? Configuration.Instance.ViewDistance : 8000; }
        }

        private void BroadcastGround(uint action, GroundItem ground)
        {
            uint actor = CharacterID;
            GameWorld.Instance.SendNear(ground.ClusterID, ground.X, ground.Y, BroadcastDistance,
                () => new SM_UPDATE_ITEM_INFO(action, ground, actor));
        }

        private void RefuseGround(string what, uint uniqueID, string reason)
        {
            Logger.ShowWarning(string.Format("{0}: {1} of {2:X8} refused: {3}.", Character.Name, what, uniqueID, reason));
        }

        /// <summary>
        /// 0x06: name and faction of a player the client sees.
        /// </summary>
        public void OnSimplePlayerInfo(CM_SIMPLE_PLAYER_INFO p)
        {
            var other = GameWorld.Instance.Get(p.CharacterID);
            if (other == null)
            {
                this.Network.SendPacket(new SM_SIMPLE_PLAYER_INFO(0, null));
                return;
            }
            this.Network.SendPacket(new SM_SIMPLE_PLAYER_INFO(other.AccountID, other.Character));
        }

        /// <summary>
        /// 0x0A: how a player the client sees looks.
        /// </summary>
        public void OnPlayerLooks(CM_PLAYER_LOOKS p)
        {
            var other = GameWorld.Instance.Get(p.CharacterID);
            if (other == null)
            {
                Logger.ShowWarning(string.Format("Looks asked for {0}, who is not in the game.", p.CharacterID));
                return;
            }
            var vehicle = other.Inventory.Piloting;
            this.Network.SendPacket(vehicle != null
                ? new SM_PLAYER_LOOKS(other.CharacterID, vehicle.StaticID, PlayerInventory.ArmamentTemplates(vehicle), other.Coord.UpdateCounter)
                : new SM_PLAYER_LOOKS(other.Character));
        }

        /// <summary>
        /// 0x6F: a player's profile window.
        /// </summary>
        public void OnPaperDollInfo(CM_PAPER_DOLL_INFO p)
        {
            var other = GameWorld.Instance.Get(p.CharacterID);
            if (other == null)
            {
                Logger.ShowWarning(string.Format("Paper doll asked for {0}, who is not in the game.", p.CharacterID));
                return;
            }
            this.Network.SendPacket(new SM_PAPER_DOLL_INFO(other.Character));
        }

        /// <summary>
        /// 0x55: the player logs out; save and confirm.
        /// </summary>
        public void OnLogoutGS(CM_LOGOUT_GS p)
        {
            if (InGame)
            {
                Save();
            }
            this.Network.SendPacket(new SM_LOGOUT_GS(p.CharacterID));
        }

        /// <summary>
        /// 0x42: the client leaves the game server; take the player out of the world and confirm.
        /// </summary>
        public void OnLogoutGameFE(CM_LOGOUT_GAME_FE p)
        {
            if (InGame && GameWorld.Instance.Remove(this))
            {
                Save();
            }
            this.Network.SendPacket(new SM_LOGOUT_GAME_FE());
        }

        /// <summary>
        /// 0x37: log the client's message.
        /// </summary>
        public void OnClientMsg(CM_CLIENT_MSG p)
        {
            Logger.ShowInfo(string.Format("Client message from {0} (type {1}): {2}",
                InGame ? Character.Name : "?", p.Type, p.Text));
        }

        private void UpdateCoord(CoordData coord)
        {
            if (coord == null)
            {
                return;
            }

            if (coord.CharacterID != CharacterID)
            {
                Logger.ShowWarning(string.Format("{0} sent a position record for {1}, ignoring it.", Character.Name, coord.CharacterID));
                return;
            }

            // Fields the server owns: the client sends 0xFFFF for the machine id and cannot promote itself.
            coord.MachineID = MachineID;
            coord.AccountLevel = AccountLevel;
            coord.ClusterID = (ushort)Character.Zone;

            // The client cannot claim a vehicle it is not piloting.
            var vehicle = Inventory.Piloting;
            if (vehicle == null ? coord.VehicleUniqueID != 0 : coord.VehicleUniqueID != vehicle.UniqueID)
            {
                coord.VehicleUniqueID = vehicle != null ? vehicle.UniqueID : 0;
                coord.VehicleTemplateID = vehicle != null ? vehicle.StaticID : -1;
            }

            lock (sync)
            {
                Coord = coord;
            }
        }

        /// <summary>
        /// Moves the player (GM #tele, #bookmark and #tp through the CMS server) and tells the client.
        /// </summary>
        public void Teleport(int x, int y, int z)
        {
            if (!InGame)
            {
                return;
            }

            CoordData coord;
            lock (sync)
            {
                Coord.X = x;
                Coord.Y = y;
                Coord.Z = z;
                coord = Coord;
            }
            Logger.ShowInfo(string.Format("{0} was teleported to {1}, {2}, {3}.", Character.Name, x, y, z));
            this.Network.SendPacket(new SM_COMPULSION_MOVE(coord));
            Save();
        }

        /// <summary>
        /// Saves the player and tells the client to log out (maintenance). The client then disconnects.
        /// </summary>
        public void ForceLogout()
        {
            if (!InGame)
            {
                return;
            }
            Save();
            this.Network.SendPacket(new SM_FORCE_LOGOUT(CharacterID));
        }

        private void SaveIfDue()
        {
            if ((DateTime.UtcNow - lastSave).TotalSeconds >= Configuration.Instance.SaveInterval)
            {
                Save();
            }
        }

        /// <summary>
        /// Writes the player's position and items back to the database.
        /// </summary>
        private void Save()
        {
            lastSave = DateTime.UtcNow;
            try
            {
                CoordData coord;
                lock (sync)
                {
                    coord = Coord;
                }
                Character.X = coord.X;
                Character.Y = coord.Y;
                Character.Z = coord.Z;
                Character.RotY = coord.Tilt;
                Character.RotX = coord.Roll;
                Character.Direction = coord.Direction;
                CharacterDatabase.Instance.SavePosition(Character);
            }
            catch (Exception ex)
            {
                Logger.ShowError(ex);
            }
            SaveItems();
        }

        /// <summary>
        /// Writes the player's items back to the container table (with the vehicles they left on the ground),
        /// and their money when it changed.
        /// </summary>
        private void SaveItems()
        {
            try
            {
                // The shuttle of a flight is not saved: it is thrown away on arrival.
                var flight = GameWorld.Instance.GetFlight(CharacterID);
                bool flying = flight != null && flight.Shuttle != null && flight.Shuttle == Inventory.Piloting;
                CharacterDatabase.Instance.SaveItems(Character, Inventory.ToRows(GameWorld.Instance.GroundVehicles(CharacterID), flying));
                var money = Inventory.Money;
                if (money != null && money.Amount != Character.Money)
                {
                    Character.Money = money.Amount;
                    CharacterDatabase.Instance.SaveMoney(Character);
                }
            }
            catch (Exception ex)
            {
                Logger.ShowError(ex);
            }
        }

        private void RefuseLogin(string reason)
        {
            Logger.ShowWarning("Game login refused: " + reason + ".");
            this.Network.SendPacket(new SM_LOGIN_GAME(false));
        }

        private bool CheckInGame(string what)
        {
            if (!InGame)
            {
                Logger.ShowWarning(what + " requested before a game login, disconnecting.");
                this.Network.Disconnect();
                return false;
            }
            return true;
        }
    }
}
