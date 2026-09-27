using System;
using System.Collections.Generic;
using System.Linq;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// The players in the game. Java reference: mina_gameserver GameWorld.java (without NPCs, ground
    /// items and the spatial index yet).
    /// </summary>
    public class GameWorld
    {
        static readonly GameWorld instance = new GameWorld();

        public static GameWorld Instance { get { return instance; } }

        private readonly object sync = new object();
        private readonly Dictionary<uint, UCGameSession> players = new Dictionary<uint, UCGameSession>();

        /// <summary>
        /// Set while the server is closed for maintenance (#shutdown on the CMS server); logins are refused.
        /// </summary>
        public bool Closed { get; set; }

        /// <summary>
        /// Adds a player. Returns the session that was already playing this character, if any, so the
        /// caller can disconnect it.
        /// </summary>
        public UCGameSession Add(UCGameSession session)
        {
            lock (sync)
            {
                UCGameSession previous;
                players.TryGetValue(session.CharacterID, out previous);
                players[session.CharacterID] = session;
                return previous == session ? null : previous;
            }
        }

        /// <summary>
        /// Removes a player; does nothing when another session has taken over the character.
        /// </summary>
        public bool Remove(UCGameSession session)
        {
            lock (sync)
            {
                UCGameSession current;
                if (players.TryGetValue(session.CharacterID, out current) && current == session)
                {
                    players.Remove(session.CharacterID);
                    return true;
                }
                return false;
            }
        }

        public UCGameSession Get(uint characterID)
        {
            lock (sync)
            {
                UCGameSession session;
                players.TryGetValue(characterID, out session);
                return session;
            }
        }

        public List<UCGameSession> Players
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
        /// The other players <paramref name="viewer"/> can see: same zone, within <paramref name="radius"/>.
        /// </summary>
        public List<UCGameSession> Visible(UCGameSession viewer, int radius)
        {
            var coord = viewer.Coord;
            return Players.Where(p => p != viewer && p.Coord != null &&
                p.Coord.ClusterID == coord.ClusterID && coord.IsNear(p.Coord, radius)).ToList();
        }

        /// <summary>
        /// When the occupation cities' current state ends, one Unix time per city in
        /// <see cref="Network.Packets.Client.SM_OCCUPATION_CITY_INFO_LIST"/> order. The official server sent fixed
        /// times 26 and 48 minutes ahead of its clock (UCGOZone-Login.pcap), the same in every 0x8070. Sending the
        /// current time instead makes the value move with the server clock, which the client may compare with
        /// its own; so these are set once and only move forward (by an hour) once they have passed.
        /// </summary>
        private static readonly int[] occupationTimes = { UnixTime() + 26 * 60, UnixTime() + 48 * 60 };

        public static int[] OccupationTimes()
        {
            int now = UnixTime();
            lock (occupationTimes)
            {
                for (int i = 0; i < occupationTimes.Length; i++)
                {
                    while (occupationTimes[i] <= now)
                    {
                        occupationTimes[i] += 60 * 60;
                    }
                }
                return (int[])occupationTimes.Clone();
            }
        }

        public static int UnixTime()
        {
            return (int)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }
    }
}
