using System;
using System.Collections.Generic;
using System.Linq;
using Common.Network.Packets;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// The players in the game and the items and vehicles lying on the ground. Java reference:
    /// mina_gameserver GameWorld.java (without NPCs and the spatial index yet).
    /// </summary>
    public class GameWorld
    {
        static readonly GameWorld instance = new GameWorld();

        public static GameWorld Instance { get { return instance; } }

        private readonly object sync = new object();
        private readonly Dictionary<uint, UCGameSession> players = new Dictionary<uint, UCGameSession>();
        private readonly Dictionary<uint, GroundItem> ground = new Dictionary<uint, GroundItem>();
        private ushort groundCounter;
        private readonly Dictionary<uint, Flight> flights = new Dictionary<uint, Flight>();

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
        /// Puts an item or vehicle on the ground and raises the ground item counter.
        /// </summary>
        public void Place(GroundItem item)
        {
            lock (sync)
            {
                item.Counter = ++groundCounter;
                ground[item.UniqueID] = item;
            }
        }

        /// <summary>
        /// Takes an item or vehicle off the ground; null when it is not there (any more).
        /// <paramref name="canTake"/> decides, under the lock, whether this player may take it.
        /// </summary>
        public GroundItem Take(uint uniqueID, Func<GroundItem, bool> canTake)
        {
            lock (sync)
            {
                GroundItem item;
                if (!ground.TryGetValue(uniqueID, out item) || IsExpired(item) || !canTake(item))
                {
                    return null;
                }
                ground.Remove(uniqueID);
                item.Counter = ++groundCounter;
                return item;
            }
        }

        public GroundItem GetGround(uint uniqueID)
        {
            lock (sync)
            {
                GroundItem item;
                ground.TryGetValue(uniqueID, out item);
                return item;
            }
        }

        /// <summary>
        /// Items and vehicles of one list lying within <paramref name="radius"/> of a point. Expired items are
        /// removed first.
        /// </summary>
        public List<GroundItem> GroundNear(ushort clusterID, int x, int y, int radius, byte list)
        {
            lock (sync)
            {
                RemoveExpired();
                return ground.Values.Where(g => g.ClusterID == clusterID && g.List == list &&
                    Math.Abs((long)g.X - x) <= radius && Math.Abs((long)g.Y - y) <= radius).ToList();
            }
        }

        /// <summary>
        /// The vehicles a character left on the ground (saved as hangar rows).
        /// </summary>
        public List<ItemNode> GroundVehicles(uint ownerID)
        {
            lock (sync)
            {
                return ground.Values.Where(g => g.IsVehicle && !g.IsWreck && g.OwnerID == ownerID).Select(g => g.Node).ToList();
            }
        }

        /// <summary>
        /// Takes a character's vehicles off the ground (they are back in the hangar when they log in again).
        /// </summary>
        public List<GroundItem> TakeGroundVehicles(uint ownerID)
        {
            lock (sync)
            {
                var taken = ground.Values.Where(g => g.IsVehicle && !g.IsWreck && g.OwnerID == ownerID).ToList();
                foreach (var item in taken)
                {
                    ground.Remove(item.UniqueID);
                    item.Counter = ++groundCounter;
                }
                return taken;
            }
        }

        /// <summary>
        /// Sends a packet built by <paramref name="build"/> to every player in the cluster within
        /// <paramref name="radius"/> of a point (one packet each).
        /// </summary>
        public void SendNear(ushort clusterID, int x, int y, int radius, Func<UCPacket<GSOpcode>> build)
        {
            foreach (var player in Players)
            {
                var coord = player.Coord;
                if (coord != null && coord.ClusterID == clusterID &&
                    Math.Abs((long)coord.X - x) <= radius && Math.Abs((long)coord.Y - y) <= radius)
                {
                    player.Network.SendPacket(build());
                }
            }
        }

        /// <summary>
        /// Records a shuttle flight between Earth and Space until the character logs in on the other side.
        /// </summary>
        public void StartFlight(uint characterID, Flight flight)
        {
            lock (sync)
            {
                flights[characterID] = flight;
            }
        }

        /// <summary>
        /// The character's flight, if they are on one (it stays until <see cref="EndFlight"/>).
        /// </summary>
        public Flight GetFlight(uint characterID)
        {
            lock (sync)
            {
                Flight flight;
                flights.TryGetValue(characterID, out flight);
                return flight;
            }
        }

        public void EndFlight(uint characterID)
        {
            lock (sync)
            {
                flights.Remove(characterID);
            }
        }

        private static bool IsExpired(GroundItem item)
        {
            return item.CanExpire && item.Expires <= UnixTime();
        }

        private void RemoveExpired()
        {
            foreach (var item in ground.Values.Where(IsExpired).ToList())
            {
                ground.Remove(item.UniqueID);
            }
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
