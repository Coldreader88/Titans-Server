using System;
using System.Collections.Generic;
using System.Linq;
using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.CmsServer.Network.Client;
using TitansUC.CmsServer.Network.Packets.Server;

namespace TitansUC.CmsServer.World
{
    /// <summary>
    /// Who is logged in to the CMS server, and the open group chats.
    /// Java reference: mina_cmsserver model/CMSWorld.java and model/chat/ChannelMap.java.
    /// </summary>
    public class CmsWorld
    {
        static readonly CmsWorld instance = new CmsWorld();

        public static CmsWorld Instance { get { return instance; } }

        private readonly object sync = new object();
        private readonly Dictionary<uint, UCCmsSession> players = new Dictionary<uint, UCCmsSession>();
        private readonly Dictionary<uint, GroupChat> groupChats = new Dictionary<uint, GroupChat>();
        private uint nextGroupChatID = 1;
        private uint loginCounter = (uint)new Random().Next(0x100, 0x7000);

        /// <summary>
        /// Adds a player. Returns the session that was already logged in with this character, if any, so
        /// the caller can disconnect it.
        /// </summary>
        public UCCmsSession Add(UCCmsSession session)
        {
            lock (sync)
            {
                UCCmsSession previous;
                players.TryGetValue(session.CharacterID, out previous);
                players[session.CharacterID] = session;
                return previous == session ? null : previous;
            }
        }

        /// <summary>
        /// Removes a player; does nothing when another session has taken over the character.
        /// </summary>
        public bool Remove(UCCmsSession session)
        {
            lock (sync)
            {
                UCCmsSession current;
                if (players.TryGetValue(session.CharacterID, out current) && current == session)
                {
                    players.Remove(session.CharacterID);
                    return true;
                }
                return false;
            }
        }

        public UCCmsSession Get(uint characterID)
        {
            lock (sync)
            {
                UCCmsSession session;
                players.TryGetValue(characterID, out session);
                return session;
            }
        }

        public bool IsOnline(uint characterID)
        {
            return Get(characterID) != null;
        }

        public List<UCCmsSession> Players
        {
            get
            {
                lock (sync)
                {
                    return players.Values.ToList();
                }
            }
        }

        public int Count
        {
            get
            {
                lock (sync)
                {
                    return players.Count;
                }
            }
        }

        /// <summary>
        /// The number the login reply (0x8001) carries. The official server sent a different small
        /// number on each login; what it means is unknown.
        /// </summary>
        public uint NextLoginNumber()
        {
            lock (sync)
            {
                return ++loginCounter;
            }
        }

        /// <summary>
        /// Sends a packet built by <paramref name="make"/> to a player if they are online. A new packet
        /// is built for every recipient. Returns false when they are not online.
        /// </summary>
        public bool SendTo(uint characterID, Func<Packet<CMSOpcode>> make)
        {
            var session = Get(characterID);
            if (session == null)
            {
                return false;
            }
            session.Send(make());
            return true;
        }

        public void SendToAll(Func<Packet<CMSOpcode>> make)
        {
            foreach (var session in Players)
            {
                session.Send(make());
            }
        }

        /// <summary>
        /// A system message (chat type 7) to one player.
        /// </summary>
        public void SystemMessage(UCCmsSession session, string message)
        {
            foreach (var line in SplitLines(message))
            {
                session.Send(new SM_CHAT_MSG(0, line, ChatType.System));
            }
        }

        /// <summary>
        /// A system message (chat type 7) to everyone.
        /// </summary>
        public void SystemMessage(string message)
        {
            foreach (var line in SplitLines(message))
            {
                SendToAll(() => new SM_CHAT_MSG(0, line, ChatType.System));
            }
        }

        private static IEnumerable<string> SplitLines(string message)
        {
            return (message ?? string.Empty).Replace("\r", string.Empty).Split('\n');
        }

        #region Group chats

        /// <summary>
        /// Opens a group chat with <paramref name="creator"/> in it.
        /// </summary>
        public GroupChat CreateGroupChat(Member creator, uint now)
        {
            lock (sync)
            {
                while (groupChats.ContainsKey(nextGroupChatID) || nextGroupChatID == 0)
                {
                    nextGroupChatID++;
                }
                var chat = new GroupChat(nextGroupChatID++, now);
                chat.Members.Add(creator.Copy());
                groupChats[chat.ID] = chat;
                return chat;
            }
        }

        /// <summary>
        /// Runs <paramref name="action"/> on a group chat while holding the lock, so it can read and change
        /// the members and build packets from them. Returns false when there is no such chat.
        /// </summary>
        public bool WithGroupChat(uint chatID, Action<GroupChat> action)
        {
            lock (sync)
            {
                GroupChat chat;
                if (!groupChats.TryGetValue(chatID, out chat))
                {
                    return false;
                }
                action(chat);
                if (chat.Members.Count == 0)
                {
                    groupChats.Remove(chatID);
                }
                return true;
            }
        }

        /// <summary>
        /// The ids of the group chats a player is in.
        /// </summary>
        public List<uint> GroupChatsOf(uint characterID)
        {
            lock (sync)
            {
                return groupChats.Values.Where(c => c.Contains(characterID)).Select(c => c.ID).ToList();
            }
        }

        #endregion
    }
}
