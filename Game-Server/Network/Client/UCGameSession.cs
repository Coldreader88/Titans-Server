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

        /// <summary>
        /// Set once the player took off for the other side's server (0x40).
        /// </summary>
        private bool departed;

        public override void OnDisconnect()
        {
            if (Character == null)
            {
                return;
            }

            // After a flight everything was saved at take-off, and the other side's server may already have
            // loaded (and changed) the character: saving again here could undo that.
            if (GameWorld.Instance.Remove(this) && !departed)
            {
                // Cleared for the other side and gone without 0x42: the client is on its way there.
                if (reservedCluster != 0)
                {
                    Depart();
                }
                else
                {
                    Save();
                }
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
                if ((ushort)character.Zone != Configuration.Instance.Zone)
                {
                    RefuseLogin(string.Format("{0} is in {1}, but this is the {2} server (port {3}); the Lobby should send them to port {4}",
                        character.Name, (ushort)character.Zone == Configuration.ZoneSpace ? "Space" : "Earth", Configuration.Instance.InstanceName,
                        Configuration.Instance.ListenPort, Configuration.Instance.TransferPort));
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

                // Back in the vehicle they were piloting when they left (a shuttle, after a flight between Earth
                // and Space). Vehicles they left on the ground are still lying there.
                if (Inventory.Piloting != null)
                {
                    Inventory.KeepPilotingID(WorldDatabase.FlightShuttleID(character.ClientID));
                    SetVehicle(Inventory.Piloting);
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
                // Something inside a vehicle or wreck on the ground the player opened.
                ItemNode inside;
                var holder = GameWorld.Instance.FindHolder(p.UniqueID, out inside);
                node = holder != null && CanOpen(holder) ? inside : null;
            }
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
                    if (result == null && TakeFromGroundVehicle(p))
                    {
                        return;
                    }
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

        /// <summary>
        /// Distance within which a player can open a vehicle or wreck on the ground and take things out of it.
        /// </summary>
        public const int OpenDistance = 3000;

        /// <summary>
        /// Whether this player may open that vehicle on the ground: their own vehicle or a wreck they made, near them.
        /// </summary>
        private bool CanOpen(GroundItem g)
        {
            var c = Coord;
            // A wreck belongs to whoever destroyed it; one an NPC destroyed is open to anyone (its pilot first of all).
            bool mine = g != null && (g.OwnerID == CharacterID || (g.IsWreck && (g.OwnerID == 0xFFFFFFFF || Npc.IsNpcID(g.OwnerID))));
            return g != null && g.IsVehicle && c != null && g.ClusterID == c.ClusterID && mine &&
                Math.Abs((long)g.X - c.X) <= OpenDistance && Math.Abs((long)g.Y - c.Y) <= OpenDistance;
        }

        /// <summary>
        /// 0x26: the player opens a vehicle or wreck on the ground (or their own vehicle); 0x8026 lets them.
        /// </summary>
        public void OnSpaceItemLock(CM_SPACE_ITEM_LOCK p)
        {
            if (!CheckInGame("Open vehicle"))
            {
                return;
            }
            var g = GameWorld.Instance.GetGround(p.VehicleUniqueID);
            bool allowed = CanOpen(g) || Inventory.Get(p.VehicleUniqueID) != null;
            var body = (byte[])p.Body.Clone();
            body[2] = 0;
            body[3] = allowed ? (byte)2 : (byte)1;
            this.Network.SendPacket(new SM_RAW((uint)GSOpcode.SM_SPACE_ITEM_LOCK, body));
            if (!allowed)
            {
                Logger.ShowWarning(string.Format("{0} may not open {1:X8}.", Character.Name, p.VehicleUniqueID));
            }
        }

        /// <summary>
        /// 0x27: what is inside the vehicle or wreck the player opened: its cargo and armaments in one list.
        /// </summary>
        public void OnSpaceItemList(CM_SPACE_ITEM_LOCK p)
        {
            if (!CheckInGame("List vehicle"))
            {
                return;
            }
            var g = GameWorld.Instance.GetGround(p.VehicleUniqueID);
            var vehicle = CanOpen(g) ? g.Node : Inventory.Get(p.VehicleUniqueID);
            if (vehicle == null)
            {
                Logger.ShowWarning(string.Format("{0} listed {1:X8}, which they cannot open.", Character.Name, p.VehicleUniqueID));
                return;
            }
            List<ItemNode> items;
            lock (vehicle)
            {
                items = vehicle.Descendants().Where(n => n != vehicle && !n.IsEmptySlot &&
                    n.StaticID != PlayerInventory.VehicleArmaments && n.StaticID != PlayerInventory.VehicleInventory).ToList();
            }
            this.Network.SendPacket(new SM_SPACE_ITEM_LIST(vehicle, items, CharacterID));
        }

        /// <summary>
        /// 0x17 with an item that lies in a vehicle or wreck on the ground (opened with 0x26): takes it (or
        /// part of the stack) out into one of the player's containers. False when the item is not in one.
        /// </summary>
        private bool TakeFromGroundVehicle(CM_MOVE_ITEM p)
        {
            ItemNode item;
            var g = GameWorld.Instance.FindHolder(p.ItemUniqueID, out item);
            if (g == null || item == g.Node || !CanOpen(g))
            {
                return false;
            }

            ItemNode taken;
            lock (g.Node)
            {
                var parent = item.Parent;
                if (parent == null)
                {
                    return false;
                }
                int amount = p.Amount > 0 ? p.Amount : item.Amount;
                if (item.Format == ItemNode.Singleton && amount < item.Amount)
                {
                    item.Amount -= amount;
                    taken = PlayerInventory.NewItem(item.StaticID, amount, item.Name);
                    taken.Created = item.Created;
                }
                else if (parent.StaticID == PlayerInventory.VehicleArmaments)
                {
                    parent.SetSlot(parent.Children.IndexOf(item), ItemNode.EmptySlot(0));
                    taken = item;
                }
                else
                {
                    parent.Remove(item);
                    taken = item;
                }
            }

            if (!Inventory.PickUp(taken, p.DestUniqueID))
            {
                // Nowhere to put it: back where it was.
                lock (g.Node)
                {
                    var cargo = g.Node.Children.Find(c => c.StaticID == PlayerInventory.VehicleInventory);
                    if (taken != item)
                    {
                        item.Amount += taken.Amount;
                    }
                    else if (cargo != null)
                    {
                        cargo.Add(taken);
                    }
                }
                RefuseMove(p, "cannot take it out of the vehicle into that container");
                return true;
            }

            GameWorld.Instance.GroundChanged();
            Logger.ShowInfo(string.Format("{0} took {1} x {2} out of {3} {4:X8}.", Character.Name, taken.StaticID, taken.Amount,
                g.IsWreck ? "the wreck" : "the vehicle", g.UniqueID));
            this.Network.SendPacket(new SM_MOVE_ITEM(SM_MOVE_ITEM.Moved, p, null, taken.StaticID));
            SaveItems();
            var owner = g.IsWreck ? null : GameWorld.Instance.Get(g.OwnerID);
            if (owner != null && owner != this)
            {
                owner.SaveItems();
            }
            return true;
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
            others.AddRange(NpcManager.Instance.Visible(self.ClusterID, self.X, self.Y, radius));

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
            if (p.CharacterID != CharacterID || p.MiniOp < CM_SPACE_PICKUP_ITEM.PickUpItem ||
                p.MiniOp > CM_SPACE_PICKUP_ITEM.PickUpOntoStack)
            {
                RefuseGround("pick up", p.ItemUniqueID, "mini op " + p.MiniOp + " for character " + p.CharacterID);
                return;
            }

            bool vehicle = p.MiniOp == CM_SPACE_PICKUP_ITEM.GetIn;
            var zone = (ushort)Character.Zone;
            // A vehicle of theirs can also be dragged into the factory (mini op 1 with the factory as destination).
            bool toFactory = !vehicle && PlayerInventory.IsFactory(Inventory.Get(p.DestUniqueID));
            var ground = GameWorld.Instance.Take(p.ItemUniqueID, g => g.ClusterID == zone &&
                (vehicle || (toFactory && g.IsVehicle) ? g.IsVehicle && !g.IsWreck && g.OwnerID == CharacterID : !g.IsVehicle));
            if (ground == null)
            {
                RefuseGround("pick up", p.ItemUniqueID, "nothing there they can take");
                return;
            }

            // Mini ops 2 (money) and 4: onto the stack of the same item already there.
            if (!vehicle && p.MiniOp != CM_SPACE_PICKUP_ITEM.PickUpItem)
            {
                var stack = Inventory.PickUpOntoStack(ground.Node, p.DestUniqueID);
                if (stack != null)
                {
                    Logger.ShowInfo(string.Format("{0} picked up {1} x {2} onto their stack of {3}.", Character.Name,
                        ground.Node.StaticID, ground.Node.Amount, stack.Amount));
                    this.Network.SendPacket(new SM_SPACE_PICKUP_ITEM(p, stack, ground.UniqueID, ground.Node.Format));
                    BroadcastGround(SM_UPDATE_ITEM_INFO.ItemPickedUp, ground);
                    SaveItems();
                    return;
                }
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

        private static readonly Random productionRandom = new Random();

        private static int Roll()
        {
            lock (productionRandom)
            {
                return productionRandom.Next(10000);
            }
        }

        /// <summary>
        /// 0x28: make something in the productive container (the factory) from the ingredients the client
        /// lists, or take a vehicle there apart. The recipes are the client's (<see cref="Production"/>). The
        /// product goes in the factory at once and the client shows its timer; a failure uses the ingredients
        /// up and gives the recipe's share of them back (70%).
        /// </summary>
        public void OnProductItem(CM_PRODUCT_ITEM p)
        {
            if (!CheckInGame("Production"))
            {
                return;
            }
            var factory = Inventory.Get(p.FactoryUniqueID);
            string refusal = null;
            if (p.CharacterID != CharacterID)
            {
                refusal = "not their character";
            }
            else if (!PlayerInventory.IsFactory(factory))
            {
                refusal = string.Format("{0:X8} is not their productive container", p.FactoryUniqueID);
            }
            else if (p.Action == CM_PRODUCT_ITEM.ActionUpgrade)
            {
                refusal = "vehicle upgrades are not implemented yet";
            }
            else if (p.Action == CM_PRODUCT_ITEM.ActionDismantle)
            {
                refusal = Dismantle(p, factory);
            }
            else
            {
                refusal = Craft(p, factory);
            }
            if (refusal != null)
            {
                // Nothing was used: the ingredients keep state 7.
                Logger.ShowWarning(string.Format("{0} cannot make {1} (action {2}): {3}.", Character.Name, p.ProductID, p.Action, refusal));
                this.Network.SendPacket(new SM_PRODUCT_ITEM(p, SM_PRODUCT_ITEM.Failed, (uint)GameWorld.UnixTime(),
                    new List<uint>(), new List<KeyValuePair<int, int>>()));
            }
        }

        /// <summary>
        /// 0x28 actions 1-4: builds the product, or fails. Returns why it cannot, or null once it answered.
        /// </summary>
        private string Craft(CM_PRODUCT_ITEM p, ItemNode factory)
        {
            var recipe = Production.Get(p.ProductID);
            if (recipe == null)
            {
                return "no recipe for it";
            }
            var product = ItemTemplates.Get(p.ProductID);
            bool stacks = product == null || product.Stacks;
            int batches = stacks ? Math.Max(1, p.Amount / recipe.Yield) : 1;
            if (batches > 10000)
            {
                return "amount " + p.Amount;
            }

            // Which of the listed items go to which ingredient, and how much of each.
            var use = new int[p.Inputs.Count];
            int engine = -1;
            foreach (var input in recipe.Inputs)
            {
                int needed = input.Amount * batches;
                for (int i = 0; i < p.Inputs.Count && needed > 0; i++)
                {
                    var listed = p.Inputs[i];
                    if (use[i] > 0 || !input.Matches(listed.TemplateID))
                    {
                        continue;
                    }
                    int take = Math.Min(needed, listed.Amount);
                    if (!Inventory.CanUse(listed.UniqueID, listed.ContainerUniqueID, listed.TemplateID, take))
                    {
                        return string.Format("{0:X8} is not {1} x {2} of theirs", listed.UniqueID, take, listed.TemplateID);
                    }
                    use[i] = take;
                    needed -= take;
                    if (input.Range == 29)
                    {
                        engine = listed.TemplateID;
                    }
                }
                if (needed > 0)
                {
                    return string.Format("{0} more of ingredient {1} needed", needed, input.TemplateID != -1 ? input.TemplateID.ToString() : "range " + input.Range);
                }
            }

            var states = new List<uint>();
            var used = new Dictionary<int, int>();
            for (int i = 0; i < p.Inputs.Count; i++)
            {
                uint state = 7;
                if (use[i] > 0)
                {
                    state = Inventory.UseUp(p.Inputs[i].UniqueID, p.Inputs[i].ContainerUniqueID, use[i]);
                    int sum;
                    used.TryGetValue(p.Inputs[i].TemplateID, out sum);
                    used[p.Inputs[i].TemplateID] = sum + use[i];
                }
                states.Add(state);
            }

            var output = new List<KeyValuePair<int, int>>();
            int chance = Recipe.Chance(Character, recipe.Skills, recipe.SuccessRate);
            bool success = Roll() < chance;
            if (success)
            {
                int made = batches * recipe.Yield;
                int productID = p.ProductID;
                if (recipe.Kind == Recipe.Kinds.Refine && made > 1)
                {
                    // Refining in bulk came out 17-27% over (1000 iron ore: 585 steel, not 500; 1000 bauxite:
                    // 590 alumina; 1000 alumina: 635 fine ceramics). Single ones came out as asked.
                    lock (productionRandom)
                    {
                        made += made * productionRandom.Next(15, 28) / 100;
                    }
                }
                else if (recipe.ExID > 0 && Recipe.BestSkill(Character, recipe.Skills) >= recipe.ExSkill && Roll() < recipe.ExRate)
                {
                    productID = recipe.ExID;
                }
                Inventory.Produce(factory, productID, made, engine);
                output.Add(new KeyValuePair<int, int>(productID, made));
                GainSkill(recipe.Skills);
            }
            else
            {
                foreach (var r in recipe.Returns)
                {
                    int sum;
                    int back = used.TryGetValue(r.Key, out sum) ? sum * r.Value / 10000 : 0;
                    if (back > 0)
                    {
                        Inventory.Produce(factory, r.Key, back);
                        output.Add(new KeyValuePair<int, int>(r.Key, back));
                    }
                }
            }

            Logger.ShowInfo(string.Format("{0} {1} {2} ({3}) x {4} at {5:0.#}% ({6}).", Character.Name,
                success ? "made" : "failed to make", recipe.ProductID, product != null ? product.Name : "?", batches * recipe.Yield,
                chance / 100.0, string.Join(", ", output.Select(o => o.Value + " x " + o.Key))));
            this.Network.SendPacket(new SM_PRODUCT_ITEM(p, success ? SM_PRODUCT_ITEM.Done : SM_PRODUCT_ITEM.Failed,
                (uint)GameWorld.UnixTime(), states, output));
            SaveItems();
            return null;
        }

        /// <summary>
        /// 0x28 action 5: takes the vehicle in the factory apart. It gives back the recipe's share (70%) of what
        /// it was built from, engine aside (a Zaku II F2 of 60 super high tensile steel gave 42,
        /// Zaku_F2A_Dismantle_Success.pcap); a failure gives back 70% of that (an Oggo of 10 gave 5,
        /// Oggo_Dismantle_Fail_(ZSSAEO3).pcap). Returns why it cannot, or null once it answered.
        /// </summary>
        private string Dismantle(CM_PRODUCT_ITEM p, ItemNode factory)
        {
            if (p.Inputs.Count != 1)
            {
                return p.Inputs.Count + " items listed";
            }
            var node = Inventory.Get(p.Inputs[0].UniqueID);
            var recipe = node != null ? Production.Get(node.StaticID) : null;
            if (recipe == null || !recipe.CanDismantle)
            {
                return "it cannot be taken apart";
            }
            if (Inventory.Dismantle(p.Inputs[0].UniqueID, factory.UniqueID) == null)
            {
                return "it is not a vehicle in the factory";
            }

            int chance = Recipe.Chance(Character, recipe.DismantleSkills, recipe.DismantleRate);
            bool success = Roll() < chance;
            var output = new List<KeyValuePair<int, int>>();
            foreach (var r in recipe.Returns)
            {
                var input = recipe.Inputs.Find(i => i.TemplateID == r.Key);
                int back = input != null ? input.Amount * r.Value / 10000 : 0;
                if (!success)
                {
                    back = (int)Math.Round(back * r.Value / 10000.0);
                }
                if (back > 0)
                {
                    Inventory.Produce(factory, r.Key, back);
                    output.Add(new KeyValuePair<int, int>(r.Key, back));
                }
            }
            if (success)
            {
                GainSkill(recipe.DismantleSkills);
            }

            Logger.ShowInfo(string.Format("{0} {1} {2} ({3}) apart: {4}.", Character.Name, success ? "took" : "failed to take",
                node.Name, node.StaticID, string.Join(", ", output.Select(o => o.Value + " x " + o.Key))));
            this.Network.SendPacket(new SM_PRODUCT_ITEM(p, success ? SM_PRODUCT_ITEM.Done : SM_PRODUCT_ITEM.Failed,
                (uint)GameWorld.UnixTime(), new List<uint> { SM_PRODUCT_ITEM.StateUsedUp }, output));
            SaveItems();
            return null;
        }

        /// <summary>
        /// A success raises the best of the skills it used by 0.1, up to 130. The captures show no skill packet
        /// after crafting, so the client sees it at the next login. The rate of gain is ours.
        /// </summary>
        private void GainSkill(List<KeyValuePair<Skill, int>> skills)
        {
            if (skills.Count == 0)
            {
                return;
            }
            var skill = skills.OrderByDescending(s => Character.GetSkill(s.Key)).First().Key;
            int level = Character.GetSkill(skill);
            if (level >= GmCommands.MaxSkill)
            {
                return;
            }
            Character.SetSkill(skill, level + 1);
            try
            {
                CharacterDatabase.Instance.SaveSkill(Character, skill);
            }
            catch (Exception ex)
            {
                Logger.ShowError(ex);
            }
        }

        /// <summary>
        /// 0x29: the client's production timer ran out; 0x8029 is the request with 0x0002 in bytes 2-3 (the
        /// product has been in the factory since 0x8028).
        /// </summary>
        public void OnProductDone(CM_PRODUCT_ITEM p)
        {
            if (!CheckInGame("Production done"))
            {
                return;
            }
            var body = (byte[])p.Body.Clone();
            body[2] = 0;
            body[3] = 2;
            this.Network.SendPacket(new SM_RAW((uint)GSOpcode.SM_PRODUCT_DONE, body));
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

        private int lastDig;

        public const int MiningWeaponKind = 29;

        /// <summary>
        /// 0x32: mine the block the player's vehicle stands on (see <see cref="Mining.Dig"/>); 0x8032 always
        /// answers, so the client is never left waiting. Attempts closer together than the block's mining time
        /// (the client's own progress bar, about 10 seconds) fail.
        /// </summary>
        public void OnExcavation(CM_EXCAVATION p)
        {
            if (!CheckInGame("Mining") || p.CharacterID != CharacterID)
            {
                return;
            }

            var vehicle = Inventory.Piloting;
            // The mining weapon: sub-type 29 (0x1D), as the client checks (uc.exe 0x7cc6c4).
            var weapon = vehicle != null ? Enumerable.Range(0, 8).Select(i => Inventory.Armament(i))
                .FirstOrDefault(a => a != null && a.UniqueID == p.WeaponUniqueID) : null;
            var weaponTemplate = weapon != null ? ItemTemplates.Get(weapon.StaticID) : null;
            if (weaponTemplate == null || weaponTemplate.Kind != MiningWeaponKind)
            {
                weapon = null;
            }
            var cargo = vehicle != null ? vehicle.Children.Find(c => c.StaticID == PlayerInventory.VehicleInventory) : null;
            var block = Mining.Block(p.BlockID);
            int now = Environment.TickCount;
            ushort code;
            ItemNode mined = null;
            int template = 0, amount = 0;
            if (vehicle == null || vehicle.UniqueID != p.VehicleUniqueID || weapon == null)
            {
                code = Mining.Error;
            }
            else if (block == null)
            {
                code = Mining.Exhausted;
            }
            else if (cargo == null)
            {
                code = Mining.ContainerFull;
            }
            else if (lastDig != 0 && now - lastDig < block.MiningTimeMs * 8 / 10)
            {
                code = Mining.Failed;
            }
            else
            {
                lastDig = now;
                code = Mining.Dig(block, Character.GetSkill(Skill.MINING), out template, out amount);
            }

            if (weapon != null && weapon.Stats != null && weapon.Stats.Length > 0)
            {
                weapon.Stats[0] = Math.Max(0, weapon.Stats[0] - 1);
            }
            if (code == Mining.Success)
            {
                mined = Inventory.Produce(cargo, template, amount);
                GainSkill(new List<KeyValuePair<Skill, int>> { new KeyValuePair<Skill, int>(Skill.MINING, 0) });
            }
            this.Network.SendPacket(new SM_EXCAVATION(p.Body, code, mined, amount));
            Logger.ShowInfo(string.Format("{0} mined block {1}: {2}", Character.Name, p.BlockID,
                code == Mining.Success ? amount + " x " + template : "code " + code));
            if (weapon != null)
            {
                SaveItems();
            }
        }

        /// <summary>
        /// Health an MR tool kit restores in one use: VEHICLEREPAIRTEMPLATE value_a, by kit (280167-280170 MS/MA
        /// Lv.1-4, 280171-280174 tank/fighter Lv.1-4). The official Lv.4 kit restored 1088 and 1050, all that
        /// was missing both times.
        /// </summary>
        private static readonly Dictionary<int, int> RepairKitHealth = new Dictionary<int, int>
        {
            { 280167, 200 }, { 280168, 400 }, { 280169, 1000 }, { 280170, 3000 },
            { 280171, 800 }, { 280172, 2000 }, { 280173, 4000 }, { 280174, 10000 },
        };

        /// <summary>
        /// Distance within which an MR tool kit reaches the other player's vehicle.
        /// </summary>
        public const int RepairDistance = 3000;

        /// <summary>
        /// 0x69: repair another player's vehicle with the MR tool kit in this player's vehicle; 0x8069 to both,
        /// 0x806A to everyone near.
        /// </summary>
        public void OnRepairPlayer(CM_REPAIR_PLAYER p)
        {
            if (!CheckInGame("Repair player"))
            {
                return;
            }

            ItemNode kit = Inventory.Armament(p.Weapon);
            if (kit == null || !RepairKitHealth.ContainsKey(kit.StaticID))
            {
                kit = Enumerable.Range(0, 8).Select(i => Inventory.Armament(i))
                    .FirstOrDefault(a => a != null && RepairKitHealth.ContainsKey(a.StaticID));
            }
            var target = GameWorld.Instance.Get(p.TargetID);
            var vehicle = target != null && target.InGame ? target.Inventory.Piloting : null;
            var c = Coord;
            var tc = target != null ? target.Coord : null;
            string refusal =
                p.CharacterID != CharacterID ? "not their character" :
                kit == null ? "no MR tool kit in their vehicle" :
                vehicle == null || vehicle.UniqueID != p.VehicleUniqueID || vehicle.MaxHealth <= 0 ? "the other player is not in that vehicle" :
                vehicle.Health <= 0 ? "that vehicle is destroyed" :
                c == null || tc == null || c.ClusterID != tc.ClusterID ||
                    Math.Abs((long)c.X - tc.X) > RepairDistance || Math.Abs((long)c.Y - tc.Y) > RepairDistance ? "too far away" :
                null;
            if (refusal != null)
            {
                RefuseGround("repair of character " + p.TargetID, p.VehicleUniqueID, refusal);
                return;
            }

            int amount, health;
            lock (vehicle)
            {
                amount = Math.Max(0, Math.Min(RepairKitHealth[kit.StaticID], vehicle.MaxHealth - vehicle.Health));
                health = vehicle.Health + amount;
                PlayerInventory.SetHealth(vehicle, health);
            }
            if (kit.Stats != null && kit.Stats.Length > 0)
            {
                kit.Stats[0] = Math.Max(0, kit.Stats[0] - 1);
            }
            byte damage = Combat.DamagePercent(health, vehicle.MaxHealth);
            uint number = Combat.NextAttackNumber();
            lock (target.sync)
            {
                tc.Damage = damage;
            }
            target.RaiseUpdateCounter();

            uint src = CharacterID, tgt = target.CharacterID;
            int max = vehicle.MaxHealth;
            this.Network.SendPacket(new SM_REPAIR_PLAYER(false, src, tgt, amount, max, p.SpecialAttackID, number, kit, vehicle, damage));
            if (target != this)
            {
                target.Network.SendPacket(new SM_REPAIR_PLAYER(false, src, tgt, amount, max, p.SpecialAttackID, number, kit, vehicle, damage));
            }
            GameWorld.Instance.SendNear(c.ClusterID, c.X, c.Y, BroadcastDistance,
                () => new SM_REPAIR_PLAYER(true, src, tgt, amount, max, p.SpecialAttackID, number, kit, vehicle, damage));
            Logger.ShowInfo(string.Format("{0} repaired {1}'s {2} by {3} ({4}/{5}).", Character.Name, target.Character.Name,
                vehicle.Name, amount, health, max));
            SaveItems();
            if (target != this)
            {
                target.SaveItems();
            }
        }

        /// <summary>
        /// Damage a chain explosion does: a tenth of the exploding vehicle's health, between 50 and 800. The
        /// official server's formula is unknown; a Magellan (a ship, not in our templates) did 800.
        /// </summary>
        public static int ChainExplosionDamage(int explodedTemplateID)
        {
            var t = VehicleTemplates.Get(explodedTemplateID);
            return t != null && t.Health > 0 ? Math.Max(50, Math.Min(800, t.Health / 10)) : 800;
        }

        /// <summary>
        /// 0x12: the player's vehicle was caught in a nearby vehicle's explosion; 0x8012 says how much it took.
        /// A vehicle destroyed this way is a wreck of its own pilot.
        /// </summary>
        public void OnChainExplosion(CM_CHAIN_EXPLOSION p)
        {
            if (!CheckInGame("Chain explosion"))
            {
                return;
            }
            var vehicle = Inventory.Piloting;
            if (p.CharacterID != CharacterID || vehicle == null || vehicle.UniqueID != p.VehicleUniqueID || vehicle.Health <= 0)
            {
                RefuseGround("chain explosion", p.VehicleUniqueID, "not the vehicle they are piloting");
                return;
            }

            int damage = ChainExplosionDamage(p.ExplodedTemplateID);
            int health;
            lock (vehicle)
            {
                health = Math.Max(0, vehicle.Health - damage);
                PlayerInventory.SetHealth(vehicle, health);
            }
            lock (sync)
            {
                Coord.Damage = health == 0 ? (byte)100 : Combat.DamagePercent(health, vehicle.MaxHealth);
            }
            RaiseUpdateCounter();
            this.Network.SendPacket(new SM_CHAIN_EXPLOSION(CharacterID, damage, vehicle.UniqueID, vehicle.Format));
            Logger.ShowInfo(string.Format("{0}'s {1} took {2} from the explosion of a {3} ({4} left).", Character.Name,
                vehicle.Name, damage, p.ExplodedTemplateID, health));
            if (health == 0)
            {
                LoseVehicle(0xFFFFFFFF);
            }
            else
            {
                SaveItems();
            }
        }

        /// <summary>
        /// 0x25: give a vehicle standing on the ground to another player, take an unowned one, or give it up
        /// (new owner FFFFFFFF). Only its owner can give it away; wrecks keep their owner.
        /// </summary>
        public void OnChangeMachineOwner(CM_CHANGE_MACHINE_OWNER p)
        {
            if (!CheckInGame("Change vehicle owner"))
            {
                return;
            }
            var g = GameWorld.Instance.GetGround(p.VehicleUniqueID);
            var c = Coord;
            bool unowned = g != null && (g.OwnerID == 0xFFFFFFFF || g.OwnerID == 0);
            bool newOwnerOK = p.NewOwnerID == 0xFFFFFFFF || p.NewOwnerID == CharacterID || GameWorld.Instance.Get(p.NewOwnerID) != null;
            string refusal =
                p.CharacterID != CharacterID ? "not their character" :
                g == null || !g.IsVehicle ? "no vehicle there" :
                g.IsWreck ? "it is a wreck" :
                c == null || g.ClusterID != c.ClusterID || Math.Abs((long)g.X - c.X) > OpenDistance ||
                    Math.Abs((long)g.Y - c.Y) > OpenDistance ? "too far away" :
                g.OwnerID != CharacterID && !(unowned && p.NewOwnerID == CharacterID) ? "not theirs" :
                !newOwnerOK ? "the new owner is not online" :
                null;
            if (refusal != null)
            {
                RefuseGround("owner change to " + p.NewOwnerID, p.VehicleUniqueID, refusal);
                return;
            }

            uint former = g.OwnerID;
            g.ChangeOwner(p.NewOwnerID);
            GameWorld.Instance.GroundChanged();
            this.Network.SendPacket(new SM_CHANGE_MACHINE_OWNER());
            BroadcastGround(SM_UPDATE_ITEM_INFO.OwnerChanged, g);
            Logger.ShowInfo(string.Format("{0} changed the owner of {1} {2:X8} from {3} to {4}.", Character.Name, g.Node.Name,
                g.UniqueID, former, p.NewOwnerID == 0xFFFFFFFF ? "nobody" : p.NewOwnerID.ToString()));
        }

        /// <summary>
        /// 0x40: the player takes off in a shuttle for the other side. As on the Java server (RequestReserveAnotherGameFE,
        /// NotifyReserveAnotherGameFE) the character's zone changes and 0x8040 sends the client to the other side's
        /// game server (TransferHost, Port or Port + 1), where it logs in again with the same session key. The
        /// shuttle is saved as the vehicle being piloted and the take-off point in the flights table, for the
        /// other server's 0x805F.
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

            // Only a reservation: the official client goes on playing on this server after 0x8040. It buys its
            // FREIGHTER (0x21 service 3), gets in, flies the launch for about 50 seconds and only then leaves
            // with 0x42 and logs in to the other server (Earth_To_Space.pcap). The flight is saved then.
            reservedCluster = p.Cluster;
            if (Configuration.Instance.CheckSessionKey)
            {
                LoginSessionDatabase.Instance.Refresh(sessionKey, CharacterID);
            }
            Logger.ShowInfo(string.Format("{0} is cleared for {1} ({2}:{3}).", Character.Name, p.Cluster == 2 ? "Space" : "Earth",
                Configuration.Instance.TransferHost, Configuration.Instance.TransferPort));
            this.Network.SendPacket(new SM_RESERVE_ANOTHER_GAME_FE(CharacterID, p.Cluster,
                Configuration.Instance.TransferHost, Configuration.Instance.TransferPort));
        }

        /// <summary>
        /// The cluster 0x40 cleared the player for (1 Earth, 2 Space), 0 for none.
        /// </summary>
        private ushort reservedCluster;

        /// <summary>
        /// The player leaves for the other side after 0x40: saves the flight (the vehicle they fly in, which
        /// keeps its unique id, and the take-off point) and the character in the other zone.
        /// </summary>
        private void Depart()
        {
            try
            {
                WorldDatabase.SaveFlight(CharacterID, reservedCluster, Inventory.Piloting != null ? Inventory.Piloting.UniqueID : 0,
                    lastTransport ?? new Common.Characters.Transport { A = -1, B = -1 });
            }
            catch (Exception ex)
            {
                Logger.ShowError(ex);
            }
            Character.Zone = (Common.Characters.Zone)reservedCluster;
            if (Configuration.Instance.CheckSessionKey)
            {
                LoginSessionDatabase.Instance.Refresh(sessionKey, CharacterID);
            }
            Logger.ShowInfo(string.Format("{0} takes off for {1} in {2}.", Character.Name, reservedCluster == 2 ? "Space" : "Earth",
                Inventory.Piloting != null ? Inventory.Piloting.Name : "nothing"));
            Save();
            departed = true;
        }

        /// <summary>
        /// 0x5F: the player info, asked for after a flight: the shuttle they arrived in and where it took off.
        /// </summary>
        public void OnGCPlayerInfo(CM_GC_PLAYER_INFO p)
        {
            if (!CheckInGame("Player info"))
            {
                return;
            }

            Common.Characters.Transport transport = null;
            try
            {
                transport = WorldDatabase.TakeFlight(CharacterID);
            }
            catch (Exception ex)
            {
                Logger.ShowError(ex);
            }
            var shuttle = Inventory.Piloting;
            this.Network.SendPacket(new SM_GC_PLAYER_INFO(AccountID, Character, shuttle != null ? shuttle.StaticID : 0, transport));
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
            if (Npc.IsNpcID(p.TargetID))
            {
                AttackNpc(weapon, NpcManager.Instance.Get(p.TargetID), p.Distance);
                return;
            }
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
        /// 0x11: an attack on a vehicle standing on the ground with nobody in it. 0x8011 goes to the attacker,
        /// the vehicle's new health to everyone near (0x8035 action 5); a destroyed one becomes a wreck
        /// (0x8035 action 1 with health 0) and is lost to its owner.
        /// </summary>
        public void OnAttackItem(CM_ATTACK_ITEM p)
        {
            if (!CheckInGame("Attack item") || p.AttackerID != CharacterID)
            {
                return;
            }
            var weapon = Inventory.Armament(p.Slot);
            var ground = GameWorld.Instance.GetGround(p.ItemUniqueID);
            if (weapon == null || ground == null || !ground.IsVehicle || ground.IsWreck || ground.ClusterID != Coord.ClusterID ||
                ground.Node.MaxHealth <= 0)
            {
                Logger.ShowWarning(string.Format("{0}: attack on ground item {1:X8} refused: no weapon, or not a vehicle.",
                    Character.Name, p.ItemUniqueID));
                return;
            }

            var r = Combat.Attack(weapon, ground.Node, null, 0);
            uint attacker = CharacterID;
            this.Network.SendPacket(new SM_ATTACK_ITEM(attacker, p.Echo, r, ground));
            if (r.Result == Combat.ResultMiss)
            {
                return;
            }
            bool gone = false;
            if (r.Destroyed)
            {
                uint formerOwner = ground.OwnerID;
                gone = !Combat.LeavesWreck();
                if (gone)
                {
                    GameWorld.Instance.Take(ground.UniqueID, g => true);
                }
                else
                {
                    ground.BecomeWreck(CharacterID);
                }
                Logger.ShowInfo(string.Format("{0} destroyed the empty {1} of character {2}{3}.", Character.Name, ground.Node.Name,
                    formerOwner, gone ? "; nothing is left of it" : ""));
                var owner = GameWorld.Instance.Get(formerOwner);
                if (owner != null)
                {
                    owner.SaveItems();
                }
            }
            if (!gone)
            {
                GameWorld.Instance.Place(ground);
            }
            ushort echo = p.Echo;
            uint action = gone ? SM_UPDATE_ITEM_INFO.ItemPickedUp : r.Destroyed ? SM_UPDATE_ITEM_INFO.ItemDropped : SM_UPDATE_ITEM_INFO.Damaged;
            GameWorld.Instance.SendNear(ground.ClusterID, ground.X, ground.Y, BroadcastDistance, () =>
                new SM_UPDATE_ITEM_INFO(action, ground, attacker, echo));
        }

        /// <summary>
        /// An attack on an NPC: 0x800F to the attacker only (an NPC has no client), 0x8036 to everyone near it.
        /// </summary>
        private void AttackNpc(ItemNode weapon, Npc npc, int distance)
        {
            if (weapon == null || npc == null || npc.Zone != Coord.ClusterID)
            {
                Logger.ShowWarning(string.Format("{0}: attack on an NPC refused: no weapon or no such NPC.", Character.Name));
                return;
            }
            var r = NpcManager.Instance.Attack(npc, weapon, distance);
            if (r == null)
            {
                return;
            }
            uint attacker = CharacterID;
            this.Network.SendPacket(new SM_ATTACK_RESULT(attacker, npc.ID, r));
            GameWorld.Instance.SendNear(npc.Zone, npc.X, npc.Y, BroadcastDistance,
                () => new SM_ATTACK_RESULT_NEAR(attacker, npc.ID, r));
            NpcManager.Instance.AfterAttack(npc, r, this);
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
            var npcHits = new List<KeyValuePair<Npc, HitResult>>();
            foreach (var t in p.Targets)
            {
                if (Npc.IsNpcID(t.TargetID))
                {
                    var npc = NpcManager.Instance.Get(t.TargetID);
                    var hit = npc != null && npc.Zone == Coord.ClusterID && !npcHits.Any(h => h.Key == npc)
                        ? NpcManager.Instance.Attack(npc, weapon, t.Distance) : null;
                    if (hit != null)
                    {
                        npcHits.Add(new KeyValuePair<Npc, HitResult>(npc, hit));
                    }
                    continue;
                }
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
            results.AddRange(npcHits.Select(h => new KeyValuePair<uint, HitResult>(h.Key.ID, h.Value)));
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
            foreach (var h in npcHits)
            {
                NpcManager.Instance.AfterAttack(h.Key, h.Value, this);
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
            if (r.Destroyed)
            {
                Logger.ShowInfo(string.Format("{0} destroyed {1}'s {2}.", Character.Name, target.Character.Name, r.DamagedItem.Name));
            }
            target.ApplyHit(r, CharacterID);
        }

        /// <summary>
        /// After the results of a hit on this player went out: a broken shield is gone, a destroyed vehicle
        /// becomes a wreck.
        /// </summary>
        public void ApplyHit(HitResult r, uint killerID)
        {
            if (r.ShieldBroken)
            {
                Inventory.DestroyArmament(Combat.ShieldSlot);
                RaiseUpdateCounter();
            }
            if (r.Destroyed)
            {
                LoseVehicle(killerID);
            }
        }

        /// <summary>
        /// The piloted vehicle was destroyed: it becomes a wreck on the ground (0x8035 action 1, health 0) that
        /// lies for ten minutes and cannot be boarded, and the player is on foot. The wreck belongs to whoever
        /// destroyed it (<paramref name="killerID"/>, a player or an NPC).
        /// </summary>
        public void LoseVehicle(uint killerID)
        {
            var vehicle = Inventory.LoseVehicle();
            if (vehicle == null)
            {
                return;
            }
            SetVehicle(null);
            if (Combat.LeavesWreck())
            {
                // The wreck of a player's vehicle is theirs when an NPC destroyed it, else the killer's.
                uint owner = Npc.IsNpcID(killerID) || killerID == 0xFFFFFFFF || killerID == 0 ? CharacterID : killerID;
                var c = Coord;
                var rotation = new byte[]
                {
                    (byte)(c.Tilt >> 8), (byte)c.Tilt, (byte)(c.Roll >> 8), (byte)c.Roll, (byte)(c.Direction >> 8), (byte)c.Direction,
                };
                var wreck = new GroundItem(vehicle, c.ClusterID, c.X, c.Y, c.Z, rotation, owner) { IsWreck = true };
                GameWorld.Instance.Place(wreck);
                BroadcastGround(SM_UPDATE_ITEM_INFO.ItemDropped, wreck);
                Logger.ShowInfo(string.Format("{0} lost {1}; the wreck belongs to {2}.", Character.Name, vehicle.Name, owner));
            }
            else
            {
                Logger.ShowInfo(string.Format("{0} lost {1}; nothing is left of it.", Character.Name, vehicle.Name));
            }
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
        /// A GM's #spawn (from the CMS server's chat command), as the Java server did it: an item or vehicle
        /// on the ground next to the GM, or a hostile NPC. Returns the message for the GM.
        /// <code>
        /// id::templateID[::amount]     name::item name        ideng::vehicleID::engine (id or name)
        /// npc[::vehicleID]             (default: a random mobile suit; either way a random loadout)
        /// </code>
        /// Items and vehicles belong to the GM, so only the GM can get in a spawned vehicle.
        /// </summary>
        public string GmSpawn(string[] args)
        {
            if (!InGame || Coord == null || args.Length == 0)
            {
                return "Usage: #spawn::id::itemID | #spawn::name::item name | #spawn::ideng::vehicleID::engine | #spawn::npc[::vehicleID]";
            }
            var c = Coord;
            string type = args[0].Trim().ToLowerInvariant();
            int n;

            if (type == "npc")
            {
                byte enemy = Character.Faction == Faction.ZEON ? (byte)1 : (byte)2;
                ItemTemplate suit = args.Length > 1 && int.TryParse(args[1], out n) ? ItemTemplates.Get(n) : Loadouts.RandomMobileSuit();
                int template = suit != null ? suit.ID : enemy == 1 ? 410000 : 410007;
                suit = suit ?? ItemTemplates.Get(template);
                string weapons = "its default guns";
                int[] armaments = suit != null && Loadouts.Applies(suit) ? Loadouts.RandomArmaments(suit, out weapons) : null;
                var npc = NpcManager.Instance.Spawn(template, enemy, c.ClusterID, c.X + 1000, c.Y, c.Z, c.Direction, armaments);
                Logger.ShowInfo(string.Format("{0} spawned NPC {1} ({2}, {3}) at {4}, {5}, {6}.", Character.Name, npc.ID, template, weapons, npc.X, npc.Y, npc.Z));
                var vt = VehicleTemplates.Get(template);
                return string.Format("Spawned a hostile {0} ({1}) 1000 away with {2}.",
                    vt != null ? vt.Name : template.ToString(), enemy == 1 ? "EF" : "Zeon", weapons);
            }

            ItemTemplate item = null;
            int amount = 1, engine = -1;
            if (type == "id" && args.Length > 1 && int.TryParse(args[1], out n))
            {
                item = ItemTemplates.Get(n);
                if (args.Length > 2 && int.TryParse(args[2], out n) && n > 0)
                {
                    amount = n;
                }
            }
            else if (type == "name" && args.Length > 1)
            {
                item = ItemTemplates.Find(string.Join(" ", args.Skip(1)));
            }
            else if (type == "ideng" && args.Length > 2 && int.TryParse(args[1], out n))
            {
                item = ItemTemplates.Get(n);
                engine = EngineTemplates.Find(string.Join(" ", args.Skip(2)));
                if (engine == 0)
                {
                    return "No engine \"" + args[2] + "\" (an id like 290033 or a name like jet engine typeA lv.3).";
                }
            }
            else
            {
                return "Bad spawn type \"" + args[0] + "\". Use id, name, ideng or npc.";
            }
            if (item == null)
            {
                return "No such item.";
            }

            ItemNode node;
            string loadout = null;
            if (item.IsVehicle && engine < 0 && Loadouts.Applies(item))
            {
                // No engine named: a random loadout and a lv.3 engine, ready to fight.
                engine = Loadouts.Engine(c.ClusterID);
                node = Inventory.CreateVehicle(item.ID, engine, Loadouts.Random(item, out loadout));
            }
            else if (item.IsVehicle)
            {
                node = Inventory.CreateVehicle(item.ID, engine);
            }
            else
            {
                node = PlayerInventory.NewItem(item.ID, item.Stacks ? amount : 1, item.Name);
            }
            // Items 300 in front (Java: 500 along y), vehicles where the GM stands.
            var ground = new GroundItem(node, c.ClusterID, c.X, item.IsVehicle ? c.Y : c.Y + 300, c.Z, new byte[6], CharacterID);
            GameWorld.Instance.Place(ground);
            BroadcastGround(SM_UPDATE_ITEM_INFO.ItemDropped, ground);
            Logger.ShowInfo(string.Format("{0} spawned {1} ({2}) x {3}{4}.", Character.Name, item.Name, item.ID, node.Amount,
                loadout != null ? " with " + loadout + ", engine " + engine : ""));
            if (loadout != null)
            {
                return string.Format("You are spawning {0} with {1} and a {2}.", item.Name, loadout, EngineTemplates.Name(engine) ?? engine.ToString());
            }
            if (engine > 0)
            {
                return string.Format("You are spawning {0} with a {1}.", item.Name, EngineTemplates.Name(engine) ?? engine.ToString());
            }
            return string.Format("You are spawning {0}{1}.", item.Name, node.Amount > 1 ? " x " + node.Amount : "");
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
        public static int BroadcastDistance
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
            var npc = Npc.IsNpcID(p.CharacterID) ? NpcManager.Instance.Get(p.CharacterID) : null;
            if (npc != null)
            {
                this.Network.SendPacket(new SM_SIMPLE_PLAYER_INFO(npc));
                return;
            }
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
            var npc = Npc.IsNpcID(p.CharacterID) ? NpcManager.Instance.Get(p.CharacterID) : null;
            if (npc != null)
            {
                this.Network.SendPacket(new SM_PLAYER_LOOKS(npc.ID, npc.TemplateID, npc.Armaments, SM_PLAYER_LOOKS.NpcCounter, 0));
                return;
            }
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
                if (reservedCluster != 0 && !departed)
                {
                    Depart();
                }
                else if (!departed)
                {
                    Save();
                }
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
        public void Save()
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
        /// Writes the player's items back to the container table (with the vehicle they are piloting), and
        /// their money when it changed. Vehicles they left on the ground are saved with the ground.
        /// </summary>
        public void SaveItems()
        {
            try
            {
                CharacterDatabase.Instance.SaveItems(Character, Inventory.ToRows());
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
