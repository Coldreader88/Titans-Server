using System;
using System.Collections.Generic;
using System.Linq;
using Common.Database;
using Common.Network.Packets;
using SmartEngine.Core;
using SmartEngine.Network;
using TitansUC.CmsServer.Commands;
using TitansUC.CmsServer.Database;
using TitansUC.CmsServer.Network.Packets;
using TitansUC.CmsServer.Network.Packets.Client;
using TitansUC.CmsServer.Network.Packets.Server;
using TitansUC.CmsServer.World;

namespace TitansUC.CmsServer.Network.Client
{
    /// <summary>
    /// One client on the CMS server: chat, friends, teams and group chat for the character in the game.
    ///
    /// Order of a login, from the official captures (Login GM.pcap, Zoning in, EFF.pcap): 0x01 login,
    /// 0x20 friend list, 0x24 friends told I'm online, 0x0B which friends are online, 0x14 team told I'm
    /// online, 0x0E team info, 0x0A which team members are online, 0x13 chat card. Then 0x03 chat and 0x04
    /// keep-alives, and 0x07 to pass invitations between players.
    /// Java reference: mina_cmsserver net/packets/incoming.
    /// </summary>
    public class UCCmsSession : Session<CMSOpcode>
    {
        /// <summary>
        /// Team changes are read-modify-write on the database, so they run one at a time.
        /// </summary>
        private static readonly object TeamSync = new object();

        /// <summary>
        /// Relayed opcodes the official client sends through 0x07.
        /// </summary>
        private static readonly HashSet<CMSOpcode> RelayOpcodes = new HashSet<CMSOpcode>
        {
            CMSOpcode.RELAY_TEAM_INVITE,
            CMSOpcode.RELAY_TEAM_INVITE_CANCEL,
            CMSOpcode.RELAY_TEAM_INVITE_ANSWER,
            CMSOpcode.RELAY_GROUP_CHAT_INVITE,
            CMSOpcode.RELAY_GROUP_CHAT_INVITE_CANCEL,
            CMSOpcode.RELAY_GROUP_CHAT_INVITE_ANSWER,
            CMSOpcode.RELAY_FRIEND_REQUEST,
            CMSOpcode.RELAY_FRIEND_REQUEST_CANCEL,
            CMSOpcode.RELAY_FRIEND_REQUEST_ANSWER,
        };

        private bool welcomed;

        /// <summary>
        /// The logged in character, null until 0x01 succeeds.
        /// </summary>
        public Member Member { get; private set; }

        public uint CharacterID { get { return Member != null ? Member.ClientID : 0; } }

        public string Name { get { return Member != null ? Member.Name : "?"; } }

        public bool LoggedIn { get { return Member != null; } }

        /// <summary>
        /// GM rank (see <see cref="AccessLevel"/>).
        /// </summary>
        public int Level { get { return Member != null ? AccessLevel.Of(Member.Access) : AccessLevel.Player; } }

        public void Send(Packet<CMSOpcode> p)
        {
            var network = this.Network;
            if (network != null && !network.Disconnected)
            {
                network.SendPacket(p);
            }
        }

        public override void OnDisconnect()
        {
            if (Member == null || !CmsWorld.Instance.Remove(this))
            {
                // Not logged in, or replaced by a newer login of the same character.
                return;
            }

            try
            {
                foreach (uint chatID in CmsWorld.Instance.GroupChatsOf(CharacterID))
                {
                    LeaveGroupChat(chatID, false);
                }

                foreach (uint id in TeamMateIDs())
                {
                    CmsWorld.Instance.SendTo(id, () => new SM_TEAM_ONLINE(CharacterID, false));
                }
                foreach (uint id in CmsDatabase.Instance.LoadFriendOf(CharacterID))
                {
                    CmsWorld.Instance.SendTo(id, () => new SM_FRIEND_ONLINE(CharacterID, false));
                }
            }
            catch (Exception ex)
            {
                Logger.ShowError(ex);
            }

            Logger.ShowInfo(string.Format("{0} left the CMS server ({1} online).", Name, CmsWorld.Instance.Count));
        }

        #region Login

        /// <summary>
        /// 0x01: load the character and answer with its name, team and rank.
        /// </summary>
        public void OnLogin(CM_LOGIN_CMS p)
        {
            if (LoggedIn)
            {
                Logger.ShowWarning(string.Format("{0} sent a second CMS login, ignoring it.", Name));
                return;
            }

            try
            {
                var member = CmsDatabase.Instance.LoadMember(p.CharacterID);
                if (member == null)
                {
                    Refuse(string.Format("character {0} does not exist", p.CharacterID));
                    return;
                }
                if (Configuration.Instance.CheckLoginSession && !LoginSessionDatabase.Instance.IsSelected(p.CharacterID))
                {
                    Refuse(string.Format("{0} (character {1}) was not taken to the game through the Lobby", member.Name, p.CharacterID));
                    return;
                }
                if (!string.Equals(member.Name, p.Name, StringComparison.Ordinal))
                {
                    Logger.ShowWarning(string.Format("CMS login for character {0} says its name is {1}, but it is {2}.",
                        p.CharacterID, p.Name, member.Name));
                }
                if (member.TeamID != -1 && CmsDatabase.Instance.LoadTeam(member.TeamID) == null)
                {
                    member.TeamID = -1;
                }

                this.Member = member;

                var previous = CmsWorld.Instance.Add(this);
                if (previous != null)
                {
                    Logger.ShowWarning(string.Format("{0} logged in to the CMS server again, dropping the older connection.", member.Name));
                    previous.Network.Disconnect();
                }

                Logger.ShowInfo(string.Format("{0} (character {1}) logged in to the CMS server ({2} online).",
                    member.Name, member.ClientID, CmsWorld.Instance.Count));

                Send(new SM_LOGIN_CMS(member.Name, member.TeamID, member.Rank, CmsWorld.Instance.NextLoginNumber()));
            }
            catch (Exception ex)
            {
                Logger.ShowError(ex);
                Refuse("an error occurred");
            }
        }

        /// <summary>
        /// 0x02: confirm the logout; the client closes the connection.
        /// </summary>
        public void OnLogout(CM_LOGOUT_CMS p)
        {
            Send(new SM_LOGOUT_CMS());
        }

        /// <summary>
        /// 0x04: keep-alive.
        /// </summary>
        public void OnHeartbeat(CM_HEARTBEAT p)
        {
            Send(new SM_HEARTBEAT());
            Send(new SM_PING());
        }

        /// <summary>
        /// 0x13: send the player's chat card to them and their team, then the welcome message.
        /// </summary>
        public void OnChatInfo(CM_CHAT_INFO p)
        {
            if (!CheckLoggedIn("Chat info"))
            {
                return;
            }

            var card = Member.Copy();
            Send(new SM_CHAT_INFO(card));
            foreach (uint id in TeamMateIDs())
            {
                CmsWorld.Instance.SendTo(id, () => new SM_CHAT_INFO(card));
            }

            if (!welcomed)
            {
                welcomed = true;
                if (!string.IsNullOrEmpty(Configuration.Instance.Welcome))
                {
                    CmsWorld.Instance.SystemMessage(this, Configuration.Instance.Welcome);
                }
            }
        }

        /// <summary>
        /// 0x14: tell the player and their team that they are online.
        /// </summary>
        public void OnTeamRegisterOnline(CM_TEAM_REGISTER_ONLINE p)
        {
            if (!CheckLoggedIn("Team online"))
            {
                return;
            }

            Send(new SM_TEAM_ONLINE(CharacterID, true));
            foreach (uint id in TeamMateIDs())
            {
                CmsWorld.Instance.SendTo(id, () => new SM_TEAM_ONLINE(CharacterID, true));
            }
        }

        /// <summary>
        /// 0x24: tell the player and everyone who has them as a friend that they are online.
        /// </summary>
        public void OnFriendsRegisterOnline(CM_FRIENDS_REGISTER_ONLINE p)
        {
            if (!CheckLoggedIn("Friends online"))
            {
                return;
            }

            Send(new SM_FRIEND_ONLINE(CharacterID, true));
            foreach (uint id in CmsDatabase.Instance.LoadFriendOf(CharacterID))
            {
                if (id != CharacterID)
                {
                    CmsWorld.Instance.SendTo(id, () => new SM_FRIEND_ONLINE(CharacterID, true));
                }
            }
        }

        #endregion

        #region Chat

        /// <summary>
        /// 0x03: pass the message to the recipients the client picked and echo it to the sender, or run a
        /// GM command (#name::arg::arg).
        /// </summary>
        public void OnChatMsg(CM_CHAT_MSG p)
        {
            if (!CheckLoggedIn("Chat message"))
            {
                return;
            }

            if (p.SenderID != CharacterID)
            {
                Logger.ShowWarning(string.Format("{0} sent a chat message as character {1}; sending it as {0}.", Name, p.SenderID));
            }

            string message = p.Message ?? string.Empty;
            if (message.StartsWith(CommandMap.Prefix, StringComparison.Ordinal))
            {
                CommandMap.Instance.Execute(this, message.Substring(CommandMap.Prefix.Length));
                return;
            }

            uint type = p.ChatType;
            if (type == ChatType.System && Level < AccessLevel.GM)
            {
                Logger.ShowWarning(string.Format("{0} tried to send a system message, ignoring it.", Name));
                return;
            }

            Logger.ShowTrace(string.Format("[chat {0}] {1}: {2}", type, Name, message));

            uint sender = CharacterID;
            Send(new SM_CHAT_MSG(sender, message, type, sender));
            foreach (uint id in p.Recipients.Distinct())
            {
                if (id != sender)
                {
                    CmsWorld.Instance.SendTo(id, () => new SM_CHAT_MSG(sender, message, type, id));
                }
            }
        }

        /// <summary>
        /// 0x07: pass an invitation or an answer on to the players it is for. When someone accepts a
        /// friend request, the requester goes on their friend list (the requester adds them with 0x21).
        /// </summary>
        public void OnRelay(CM_RELAY p)
        {
            if (!CheckLoggedIn("Relay"))
            {
                return;
            }

            var opcode = (CMSOpcode)p.Opcode;
            if (!RelayOpcodes.Contains(opcode))
            {
                Logger.ShowWarning(string.Format("{0} asked to pass on packet 0x{1:X5}, which is not an invitation; ignoring it.", Name, p.Opcode));
                return;
            }

            var body = p.Body;
            foreach (uint id in p.Recipients.Distinct())
            {
                if (id == CharacterID)
                {
                    continue;
                }
                if (!CmsWorld.Instance.SendTo(id, () => new SM_RELAY(opcode, body)))
                {
                    Logger.ShowWarning(string.Format("{0} sent 0x{1:X5} to character {2}, who is not online.", Name, p.Opcode, id));
                    continue;
                }

                if (opcode == CMSOpcode.RELAY_FRIEND_REQUEST_ANSWER && IsYes(body))
                {
                    AddFriend(id);
                }
            }
        }

        /// <summary>
        /// The result word of a 28 byte record (see <see cref="CmsPacket"/>) says yes.
        /// </summary>
        private static bool IsYes(byte[] record)
        {
            return record.Length >= 4 && ((record[2] << 8) | record[3]) == CmsPacket.ResultYes;
        }

        #endregion

        #region Friends

        /// <summary>
        /// 0x20: the friend list.
        /// </summary>
        public void OnCommunityList(CM_COMMUNITY_LIST p)
        {
            if (!CheckLoggedIn("Friend list"))
            {
                return;
            }

            var ids = CmsDatabase.Instance.LoadFriendIDs(CharacterID);
            var friends = CmsDatabase.Instance.LoadMembers(ids).OrderBy(m => ids.IndexOf(m.ClientID)).ToList();
            Send(new SM_COMMUNITY_LIST(friends));
        }

        /// <summary>
        /// 0x0B: which of the listed friends are online.
        /// </summary>
        public void OnFriendStatus(CM_FRIEND_STATUS p)
        {
            if (!CheckLoggedIn("Friend status"))
            {
                return;
            }

            Send(new SM_ONLINE_STATUS(CMSOpcode.SM_FRIEND_STATUS, OnlineStatus(p.IDs)));
        }

        /// <summary>
        /// 0x21: add a friend.
        /// </summary>
        public void OnAddFriend(CM_ADD_FRIEND p)
        {
            if (!CheckLoggedIn("Add friend"))
            {
                return;
            }

            if (!AddFriend(p.FriendID))
            {
                Send(new SM_FRIEND_CHANGE(CMSOpcode.SM_FRIEND_ADDED, CmsPacket.ResultNo, p.FriendID));
            }
        }

        /// <summary>
        /// 0x22: remove a friend.
        /// </summary>
        public void OnDeleteFriend(CM_DELETE_FRIEND p)
        {
            if (!CheckLoggedIn("Delete friend"))
            {
                return;
            }

            CmsDatabase.Instance.DeleteFriend(CharacterID, p.FriendID);
            Send(new SM_FRIEND_CHANGE(CMSOpcode.SM_FRIEND_DELETED, CmsPacket.ResultYes, p.FriendID));
        }

        /// <summary>
        /// Puts a character on the friend list and tells the client. Returns false when there is no such
        /// character.
        /// </summary>
        private bool AddFriend(uint friendID)
        {
            if (friendID == CharacterID || CmsDatabase.Instance.LoadMember(friendID) == null)
            {
                Logger.ShowWarning(string.Format("{0} cannot add character {1} as a friend.", Name, friendID));
                return false;
            }

            CmsDatabase.Instance.AddFriend(CharacterID, friendID);
            Send(new SM_FRIEND_CHANGE(CMSOpcode.SM_FRIEND_ADDED, CmsPacket.ResultYes, friendID));
            return true;
        }

        #endregion

        #region Teams

        /// <summary>
        /// 0x0D: create a team led by the player.
        /// </summary>
        public void OnCreateTeam(CM_CREATE_TEAM p)
        {
            if (!CheckLoggedIn("Create team"))
            {
                return;
            }

            string name = (p.Name ?? string.Empty).Trim();
            lock (TeamSync)
            {
                string problem = null;
                if (name.Length == 0 || name.Length > 40)
                {
                    problem = "the name is empty or too long";
                }
                else if (Member.TeamID != -1 && CmsDatabase.Instance.LoadTeam(Member.TeamID) != null)
                {
                    problem = "they are already in a team";
                }
                else if (CmsDatabase.Instance.TeamNameExists(name))
                {
                    problem = "the name is taken";
                }

                if (problem != null)
                {
                    Logger.ShowWarning(string.Format("{0} cannot create team {1}: {2}.", Name, name, problem));
                    Send(new SM_CREATE_TEAM(CmsPacket.ResultNo, -1, 0, name));
                    return;
                }

                var team = CmsDatabase.Instance.CreateTeam(name, Member, CmsServer.UnixTime());
                Member.TeamID = team.ID;
                Logger.ShowInfo(string.Format("{0} created team {1} ({2}).", Name, team.Name, team.ID));
                Send(new SM_CREATE_TEAM(CmsPacket.ResultYes, team.ID, team.Created, team.Name));
            }
        }

        /// <summary>
        /// 0x0E: a team's name, leader and members.
        /// </summary>
        public void OnTeamInfo(CM_TEAM_INFO p)
        {
            if (!CheckLoggedIn("Team info"))
            {
                return;
            }

            var team = CmsDatabase.Instance.LoadTeam(p.TeamID);
            if (team == null)
            {
                Logger.ShowWarning(string.Format("{0} asked about team {1}, which does not exist.", Name, p.TeamID));
                return;
            }
            Send(new SM_TEAM_INFO(team, CmsDatabase.Instance.LoadTeamMembers(team.ID)));
        }

        /// <summary>
        /// 0x0A: which of the listed team members are online.
        /// </summary>
        public void OnTeamMemberStatus(CM_TEAM_MEMBER_STATUS p)
        {
            if (!CheckLoggedIn("Team member status"))
            {
                return;
            }

            Send(new SM_ONLINE_STATUS(CMSOpcode.SM_TEAM_MEMBER_STATUS, OnlineStatus(p.IDs)));
        }

        /// <summary>
        /// 0x0F: a member invited someone who accepted; add them and tell the team.
        /// </summary>
        public void OnTeamAddMember(CM_TEAM_ADD_MEMBER p)
        {
            if (!CheckLoggedIn("Add team member"))
            {
                return;
            }

            lock (TeamSync)
            {
                var team = CmsDatabase.Instance.LoadTeam(p.TeamID);
                var newMember = CmsDatabase.Instance.LoadMember(p.MemberID);
                string problem = null;
                if (team == null || Member.TeamID != team.ID)
                {
                    problem = "the inviter is not in that team";
                }
                else if (newMember == null)
                {
                    problem = "there is no such character";
                }
                else if (newMember.TeamID != -1 && CmsDatabase.Instance.LoadTeam(newMember.TeamID) != null)
                {
                    problem = "they are already in a team";
                }

                if (problem != null)
                {
                    Logger.ShowWarning(string.Format("{0} cannot add character {1} to team {2}: {3}.", Name, p.MemberID, p.TeamID, problem));
                    return;
                }

                CmsDatabase.Instance.SetTeam(newMember.ClientID, team.ID);
                SetOnlineTeam(newMember.ClientID, team.ID);
                Logger.ShowInfo(string.Format("{0} joined team {1}.", newMember.Name, team.Name));

                SendToTeam(team.ID, () => new SM_TEAM_MEMBER(CMSOpcode.SM_TEAM_MEMBER_JOINED, team.ID, newMember.ClientID, newMember.Name));
            }
        }

        /// <summary>
        /// 0x10: leave the team. A team whose leader leaves gets a new leader; an empty team is deleted.
        /// </summary>
        public void OnTeamLeave(CM_TEAM_LEAVE p)
        {
            if (!CheckLoggedIn("Leave team"))
            {
                return;
            }

            lock (TeamSync)
            {
                var team = CmsDatabase.Instance.LoadTeam(p.TeamID);
                if (team == null || Member.TeamID != team.ID)
                {
                    Logger.ShowWarning(string.Format("{0} tried to leave team {1}, which they are not in.", Name, p.TeamID));
                    return;
                }

                SendToTeam(team.ID, () => new SM_TEAM_MEMBER(CMSOpcode.SM_TEAM_MEMBER_LEFT, team.ID, CharacterID, Name));
                RemoveFromTeam(team, CharacterID);
                Logger.ShowInfo(string.Format("{0} left team {1}.", Name, team.Name));
            }
        }

        /// <summary>
        /// 0x12: the leader removes a member.
        /// </summary>
        public void OnTeamKick(CM_TEAM_KICK p)
        {
            if (!CheckLoggedIn("Kick team member"))
            {
                return;
            }

            lock (TeamSync)
            {
                var team = CmsDatabase.Instance.LoadTeam(p.TeamID);
                var kicked = CmsDatabase.Instance.LoadMember(p.MemberID);
                if (team == null || team.LeaderID != CharacterID || kicked == null || kicked.TeamID != team.ID || kicked.ClientID == CharacterID)
                {
                    Logger.ShowWarning(string.Format("{0} cannot remove character {1} from team {2}.", Name, p.MemberID, p.TeamID));
                    return;
                }

                SendToTeam(team.ID, () => new SM_TEAM_MEMBER(CMSOpcode.SM_TEAM_MEMBER_KICKED, team.ID, kicked.ClientID, kicked.Name));
                RemoveFromTeam(team, kicked.ClientID);
                Logger.ShowInfo(string.Format("{0} removed {1} from team {2}.", Name, kicked.Name, team.Name));
            }
        }

        /// <summary>
        /// 0x08: the name of a player team or NPC squad.
        /// </summary>
        public void OnTeamName(CM_TEAM_NAME p)
        {
            if (!CheckLoggedIn("Team name"))
            {
                return;
            }

            string name = CmsDatabase.Instance.LoadTeamName(p.TeamID);
            if (name == null)
            {
                // Java: ids starting with 4 (IDAccessChain.SQUAD) are generated NPC squads.
                name = p.TeamID.ToString().StartsWith("4", StringComparison.Ordinal) ? "NPC" : string.Empty;
            }
            Send(new SM_TEAM_NAME(name, p.TeamID));
        }

        /// <summary>
        /// Takes a character out of a team (call with <see cref="TeamSync"/> held).
        /// </summary>
        private static void RemoveFromTeam(Team team, uint characterID)
        {
            CmsDatabase.Instance.SetTeam(characterID, -1);
            SetOnlineTeam(characterID, -1);

            var rest = CmsDatabase.Instance.LoadTeamMembers(team.ID);
            if (rest.Count == 0)
            {
                CmsDatabase.Instance.DeleteTeam(team.ID);
                Logger.ShowInfo(string.Format("Team {0} is empty and was deleted.", team.Name));
            }
            else if (team.LeaderID == characterID)
            {
                CmsDatabase.Instance.SetTeamLeader(team.ID, rest[0]);
                Logger.ShowInfo(string.Format("{0} now leads team {1}.", rest[0].Name, team.Name));
            }
        }

        private static void SetOnlineTeam(uint characterID, int teamID)
        {
            var session = CmsWorld.Instance.Get(characterID);
            if (session != null && session.Member != null)
            {
                session.Member.TeamID = teamID;
            }
        }

        private static void SendToTeam(int teamID, Func<Packet<CMSOpcode>> make)
        {
            foreach (var m in CmsDatabase.Instance.LoadTeamMembers(teamID))
            {
                CmsWorld.Instance.SendTo(m.ClientID, make);
            }
        }

        /// <summary>
        /// The other members of the player's team.
        /// </summary>
        private List<uint> TeamMateIDs()
        {
            if (Member == null || Member.TeamID == -1)
            {
                return new List<uint>();
            }
            return CmsDatabase.Instance.LoadTeamMembers(Member.TeamID)
                .Select(m => m.ClientID).Where(id => id != CharacterID).ToList();
        }

        #endregion

        #region Group chat

        /// <summary>
        /// 0x17: open a group chat with the player in it (the client then invites someone through 0x07).
        /// </summary>
        public void OnGroupChatCreate(CM_GROUP_CHAT_CREATE p)
        {
            if (!CheckLoggedIn("Create group chat"))
            {
                return;
            }

            var chat = CmsWorld.Instance.CreateGroupChat(Member, CmsServer.UnixTime());
            Send(new SM_GROUP_CHAT_CREATE(chat.ID, chat.Created));
        }

        /// <summary>
        /// 0x18: who is in a group chat.
        /// </summary>
        public void OnGroupChatMembers(CM_GROUP_CHAT_MEMBERS p)
        {
            if (!CheckLoggedIn("Group chat members"))
            {
                return;
            }

            if (!CmsWorld.Instance.WithGroupChat(p.ChatID, chat => Send(new SM_GROUP_CHAT_MEMBERS(chat))))
            {
                Logger.ShowWarning(string.Format("{0} asked about group chat {1}, which does not exist.", Name, p.ChatID));
            }
        }

        /// <summary>
        /// 0x19: someone accepted the player's invitation; add them and tell everyone in the chat.
        /// </summary>
        public void OnGroupChatAddMember(CM_GROUP_CHAT_ADD_MEMBER p)
        {
            if (!CheckLoggedIn("Add group chat member"))
            {
                return;
            }

            var joining = CmsWorld.Instance.Get(p.MemberID);
            if (joining == null || joining.Member == null)
            {
                Logger.ShowWarning(string.Format("{0} tried to add character {1} to a group chat, but they are not online.", Name, p.MemberID));
                return;
            }

            bool found = CmsWorld.Instance.WithGroupChat(p.ChatID, chat =>
            {
                if (!chat.Contains(CharacterID))
                {
                    Logger.ShowWarning(string.Format("{0} tried to add someone to group chat {1}, which they are not in.", Name, chat.ID));
                    return;
                }
                if (!chat.Contains(joining.CharacterID))
                {
                    chat.Members.Add(joining.Member.Copy());
                }
                foreach (var m in chat.Members)
                {
                    CmsWorld.Instance.SendTo(m.ClientID, () => new SM_GROUP_CHAT_MEMBER(
                        CMSOpcode.SM_GROUP_CHAT_MEMBER_JOINED, chat.ID, joining.CharacterID, joining.Name));
                }
            });

            if (!found)
            {
                Logger.ShowWarning(string.Format("{0} tried to add someone to group chat {1}, which does not exist.", Name, p.ChatID));
            }
        }

        /// <summary>
        /// 0x1A: leave a group chat.
        /// </summary>
        public void OnGroupChatLeave(CM_GROUP_CHAT_LEAVE p)
        {
            if (!CheckLoggedIn("Leave group chat"))
            {
                return;
            }

            LeaveGroupChat(p.ChatID, true);
        }

        /// <summary>
        /// 0x1B: release a group chat channel; the same as leaving it.
        /// </summary>
        public void OnGroupChatRelease(CM_GROUP_CHAT_RELEASE p)
        {
            if (!CheckLoggedIn("Release group chat"))
            {
                return;
            }

            LeaveGroupChat(p.ChatID, true);
        }

        /// <summary>
        /// Takes the player out of a group chat and tells the others (and the player, when still connected).
        /// </summary>
        private void LeaveGroupChat(uint chatID, bool tellSelf)
        {
            uint self = CharacterID;
            string name = Name;
            CmsWorld.Instance.WithGroupChat(chatID, chat =>
            {
                if (!chat.Contains(self))
                {
                    return;
                }
                foreach (var m in chat.Members)
                {
                    if (m.ClientID != self || tellSelf)
                    {
                        CmsWorld.Instance.SendTo(m.ClientID, () => new SM_GROUP_CHAT_MEMBER(
                            CMSOpcode.SM_GROUP_CHAT_MEMBER_LEFT, chat.ID, self, name));
                    }
                }
                chat.Members.RemoveAll(m => m.ClientID == self);
            });
        }

        #endregion

        private static List<KeyValuePair<uint, bool>> OnlineStatus(IEnumerable<uint> ids)
        {
            return ids.Select(id => new KeyValuePair<uint, bool>(id, CmsWorld.Instance.IsOnline(id))).ToList();
        }

        private void Refuse(string reason)
        {
            Logger.ShowWarning("CMS login refused: " + reason + ".");
            this.Network.Disconnect();
        }

        private bool CheckLoggedIn(string what)
        {
            if (!LoggedIn)
            {
                Logger.ShowWarning(what + " requested before a CMS login, disconnecting.");
                this.Network.Disconnect();
                return false;
            }
            return true;
        }
    }
}
