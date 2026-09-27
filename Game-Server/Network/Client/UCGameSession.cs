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
        /// 0x05: ground items near the player. None yet.
        /// </summary>
        public void OnSpaceCircuitItem(CM_SPACE_CIRCUIT_ITEM p)
        {
            this.Network.SendPacket(new SM_SPACE_CIRCUIT_ITEM(p.List));
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
            this.Network.SendPacket(new SM_PLAYER_LOOKS(other.Character));
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
        /// Writes the player's position back to the database.
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
