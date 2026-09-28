using System;
using System.Collections.Generic;
using Common.Account;
using Common.Characters;
using Common.Database;
using Common.Network.Packets;
using SmartEngine.Core;
using SmartEngine.Network;
using TitansUC.LobbyServer.Database;
using TitansUC.LobbyServer.Network.Packets.Client;

namespace TitansUC.LobbyServer.Network.Client
{
    public partial class UCLobbySession : Session<LSOpcode>
    {

        /// <summary>
        /// Starting stat points a new character may spread over strength, spirit and luck. The official
        /// server accepted 170 (UCGOCharCreation.pcap); the Java server did not check.
        /// </summary>
        public const int StartingStatPoints = 170;

        public Account account { get; set; }

        /// <summary>
        /// The account's characters as last sent in the character list.
        /// </summary>
        private List<Character> characters = new List<Character>();

        public override void OnConnect()
        {
            base.OnConnect();

            this.account = new Account();
        }

        public override void OnDisconnect()
        {
            Logger.ShowInfo(string.Format("User[{0}] disconnected.", account != null ? account.UserName : "UNKNOWN"));
        }

        /// <summary>
        /// 0x30000: check the client version and credentials, then send the result.
        /// </summary>
        public void OnRequestLogin(CM_REQUEST_LOGIN p)
        {
            try
            {
                this.characters = new List<Character>();
                this.account = new Account
                {
                    UserName = p.User.Name,
                    Password = p.User.Password,
                    Version = p.User.Version,
                    LastLoginIP = RemoteAddress,
                    LastLoginTime = DateTime.Now,
                };

                Logger.ShowInfo(string.Format("Login request: user {0}, client version {1}", p.User.Name, p.User.Version));

                int requiredVersion = Configuration.Instance.RequiredVersion;
                if (requiredVersion > 0 && p.User.Version != requiredVersion)
                {
                    Logger.ShowInfo(string.Format("Login refused for {0}: client version {1}, expected {2}.",
                        p.User.Name, p.User.Version, requiredVersion));
                    this.account.Status = Account.AuthenticationStatus.BAD_CLIENT;
                }
                else
                {
                    AccountDatabase.Instance.Authenticate(this.account, Configuration.Instance.AutoCreateAccounts, RemoteAddress);
                }

                // The plain password is only needed to check it.
                this.account.Password = null;

                if (this.account.Authenticated)
                {
                    try
                    {
                        this.account.SessionKey = LoginSessionDatabase.Instance.Begin(this.account.AccountID);
                    }
                    catch (Exception ex)
                    {
                        Logger.ShowError(ex);
                        this.account.Status = Account.AuthenticationStatus.WRONG_INPUT;
                    }
                }

                if (this.account.Authenticated)
                {
                    Logger.ShowInfo(string.Format("User {0} logged in (account {1}).", account.UserName, account.AccountID));
                }

                this.Network.SendPacket(new SM_REQUEST_LOGIN(this));
            }
            catch (Exception ex)
            {
                Logger.ShowError(ex);
            }
        }

        /// <summary>
        /// 0x30001: load the account's characters and send the list.
        /// </summary>
        public void OnRequestCharacterList(CM_REQUEST_CHARACTER_LIST p)
        {
            if (!CheckAuthenticated("Character list"))
            {
                return;
            }

            try
            {
                this.characters = CharacterDatabase.Instance.LoadCharacters(this.account.AccountID);
            }
            catch (Exception ex)
            {
                Logger.ShowError(ex);
                this.characters = new List<Character>();
            }

            this.Network.SendPacket(new SM_CHARACTER_LIST(this, this.characters));
        }

        /// <summary>
        /// 0x30002: send one character from the list.
        /// </summary>
        public void OnRequestPlayerInfo(CM_REQUEST_PLAYER_INFO p)
        {
            if (!CheckAuthenticated("Player info"))
            {
                return;
            }

            var character = this.characters.Find(c => c.ClientID == p.CharacterID);
            if (character == null)
            {
                Logger.ShowWarning(string.Format("User {0} asked for character {1}, which is not in their list.",
                    account.UserName, p.CharacterID));
                return;
            }

            this.Network.SendPacket(new SM_PLAYER_INFO(this.account.AccountID, character));
        }

        /// <summary>
        /// 0x30003: create a character in the first free slot, then confirm with its id.
        /// Java reference: RequestCharacterCreation.java. As in the Java server, nothing is sent back when
        /// the request is refused.
        /// </summary>
        public void OnRequestCreateCharacter(CM_REQUEST_CREATE_CHARACTER p)
        {
            if (!CheckAuthenticated("Character creation"))
            {
                return;
            }

            try
            {
                var name = (p.Name ?? string.Empty).Trim();

                Logger.ShowInfo(string.Format(
                    "Create character request from {0}: name {1}, gender {2}, faction {3}, face {4}, hair {5}/{6}, skin {7}, city {8}, stats {9}/{10}/{11}",
                    account.UserName, name, p.Gender, p.Faction, p.Face, p.HairStyle, p.HairColor, p.Skin, p.City,
                    p.Strength, p.Spirit, p.Luck));

                string refusal = CheckCreateRequest(p, name);
                if (refusal != null)
                {
                    Logger.ShowWarning(string.Format("Refused to create character {0} for {1}: {2}", name, account.UserName, refusal));
                    return;
                }

                this.characters = CharacterDatabase.Instance.LoadCharacters(this.account.AccountID);

                int slot = 0;
                for (int i = 1; i <= Character.MaxSlots; i++)
                {
                    if (!this.characters.Exists(c => c.Slot == i))
                    {
                        slot = i;
                        break;
                    }
                }

                if (slot == 0)
                {
                    Logger.ShowWarning(string.Format("Refused to create character {0} for {1}: no free slot.", name, account.UserName));
                    return;
                }

                if (CharacterDatabase.Instance.NameExists(name))
                {
                    Logger.ShowWarning(string.Format("Refused to create character {0} for {1}: the name is taken.", name, account.UserName));
                    return;
                }

                var spawn = CharacterData.GetSpawn((City)p.City);
                var character = new Character
                {
                    AccountID = this.account.AccountID,
                    Name = name,
                    Slot = slot,
                    Money = Configuration.Instance.DefaultMoney,
                    Access = this.account.GMLevel,
                    Zone = Zone.EARTH,
                    X = spawn[0],
                    Y = spawn[1],
                    Z = spawn[2],
                    Created = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds,
                    TeamID = -1,
                    Gender = (Gender)p.Gender,
                    Faction = (Faction)p.Faction,
                    Face = p.Face,
                    Skin = p.Skin,
                    HairStyle = p.HairStyle,
                    HairColor = p.HairColor,
                    Rank = 0,
                };
                character.SetSkill(Skill.STRENGTH, p.Strength);
                character.SetSkill(Skill.SPIRIT, p.Spirit);
                character.SetSkill(Skill.LUCK, p.Luck);
                StarterKit.Apply(character);

                int hangarItem;
                string hangarItemName;
                StarterKit.GetHangarItem(character.Faction, out hangarItem, out hangarItemName);

                CharacterDatabase.Instance.Create(character, hangarItem, hangarItemName);
                this.characters = CharacterDatabase.Instance.LoadCharacters(this.account.AccountID);

                Logger.ShowInfo(string.Format("Created character {0} (id {1}, slot {2}) for {3}.",
                    character.Name, character.ID, character.Slot, account.UserName));

                this.Network.SendPacket(new SM_CREATE_CHARACTER(character.ClientID));
            }
            catch (Exception ex)
            {
                Logger.ShowError(ex);
            }
        }

        /// <summary>
        /// 0x30004: delete one of the account's characters, then confirm.
        /// Java reference: RequestDeleteCharacter.java. Nothing is sent back when the request is refused.
        /// </summary>
        public void OnRequestDeleteCharacter(CM_REQUEST_DELETE_CHARACTER p)
        {
            if (!CheckAuthenticated("Character deletion"))
            {
                return;
            }

            try
            {
                var character = this.characters.Find(c => c.ClientID == p.CharacterID);
                if (character == null || p.AccountID != this.account.AccountID)
                {
                    Logger.ShowWarning(string.Format("Refused to delete character {0} for {1}: not one of their characters.",
                        p.CharacterID, account.UserName));
                    return;
                }

                if (!CharacterDatabase.Instance.Delete(this.account.AccountID, character))
                {
                    Logger.ShowWarning(string.Format("Refused to delete character {0} for {1}: it is no longer in the database.",
                        character.Name, account.UserName));
                    return;
                }

                this.characters = CharacterDatabase.Instance.LoadCharacters(this.account.AccountID);

                Logger.ShowInfo(string.Format("Deleted character {0} (id {1}) of {2}.", character.Name, character.ID, account.UserName));

                this.Network.SendPacket(new SM_DELETE_CHARACTER());
            }
            catch (Exception ex)
            {
                Logger.ShowError(ex);
            }
        }

        /// <summary>
        /// 0x30005: the player entered the game with a character; send the game server address.
        /// Java reference: RequestGameServerIP.java. As there, the character's zone picks the Earth game server
        /// (LobbyServer.xml GameServerPort) or the Space one (the next port).
        /// </summary>
        public void OnRequestGameServer(CM_REQUEST_GAME_SERVER p)
        {
            if (!CheckAuthenticated("Game server address"))
            {
                return;
            }

            try
            {
                var character = this.characters.Find(c => c.ClientID == p.CharacterID);
                string refusal = null;
                if (character == null || p.AccountID != this.account.AccountID)
                {
                    refusal = "not one of their characters";
                }
                else if (p.SessionKey != this.account.SessionKey ||
                    !LoginSessionDatabase.Instance.SelectCharacter(this.account.AccountID, p.SessionKey, character.ClientID))
                {
                    refusal = "wrong session key";
                }

                if (refusal != null)
                {
                    Logger.ShowWarning(string.Format("Refused game server handoff of character {0} for {1}: {2}.",
                        p.CharacterID, account.UserName, refusal));
                    this.Network.SendPacket(new SM_GAME_SERVER(SM_GAME_SERVER.Refuse, string.Empty, 0));
                    return;
                }

                // Earth and Space are separate game servers: Space listens on the Earth port + 1.
                bool space = (int)character.Zone == 2;
                string address = Configuration.Instance.GameServerIP;
                int port = Configuration.Instance.GameServerPort + (space ? 1 : 0);

                Logger.ShowInfo(string.Format("Sending {0} with character {1} to the {2} game server at {3}:{4}.",
                    account.UserName, character.Name, space ? "Space" : "Earth", address, port));

                this.Network.SendPacket(new SM_GAME_SERVER(SM_GAME_SERVER.Allow, address, port));
            }
            catch (Exception ex)
            {
                Logger.ShowError(ex);
                this.Network.SendPacket(new SM_GAME_SERVER(SM_GAME_SERVER.Refuse, string.Empty, 0));
            }
        }

        /// <summary>
        /// Returns why a creation request is invalid, or null when it is fine.
        /// </summary>
        private string CheckCreateRequest(CM_REQUEST_CREATE_CHARACTER p, string name)
        {
            if (name.Length == 0 || name.Length > 20)
            {
                return "the name must be 1 to 20 characters.";
            }
            if (p.Gender != (byte)Gender.MALE && p.Gender != (byte)Gender.FEMALE)
            {
                return "unknown gender " + p.Gender + ".";
            }
            if (p.Faction != (byte)Faction.FEDERATION && p.Faction != (byte)Faction.ZEON)
            {
                return "unknown faction " + p.Faction + ".";
            }
            if (!Enum.IsDefined(typeof(City), p.City))
            {
                return "unknown city " + p.City + ".";
            }
            if (p.Strength < 0 || p.Spirit < 0 || p.Luck < 0 ||
                (long)p.Strength + p.Spirit + p.Luck > StartingStatPoints)
            {
                return string.Format("stats {0}/{1}/{2} are out of range.", p.Strength, p.Spirit, p.Luck);
            }
            return null;
        }

        private bool CheckAuthenticated(string what)
        {
            if (this.account == null || !this.account.Authenticated)
            {
                Logger.ShowWarning(what + " requested before a successful login, disconnecting.");
                this.Network.Disconnect();
                return false;
            }
            return true;
        }

        private string RemoteAddress
        {
            get
            {
                try
                {
                    return ((System.Net.IPEndPoint)this.Network.Socket.RemoteEndPoint).Address.ToString();
                }
                catch (Exception)
                {
                    return "0.0.0.0";
                }
            }
        }
    }
}
