using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Common.Characters;
using Common.Database;
using MySql.Data.MySqlClient;
using SmartEngine.Core;
using TitansUC.GameServer.Network.Client;
using TitansUC.GameServer.Network.Packets.Client;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// The battle towns Richmond (58, Zeon at first) and Newman (59, Federation at first), which the factions fight
    /// over. From the client exe, its data tables and the official 0x8070 / 0x8076 captures:
    /// <list type="number">
    /// <item>Peace (status 0) until <c>Time</c>, then the attack window opens (status 1, event 4).</item>
    /// <item>A player of the other faction hits one of the town's three laser communication towers (LCT, 340004):
    /// the client sends 0x71 and the war starts (status 2, event 0) for <see cref="WarSeconds"/>.</item>
    /// <item>During the war the client registers its player when in the town area (0x74; 0x75 on leaving). A
    /// registered player who stays by one of the five integrated control facilities (ICF) for 180 s under
    /// "/occupation" sends 0x73 and the facility is theirs (event 2). Defenders can take them back.</item>
    /// <item>The attackers win when they hold all five; at the deadline the owner keeps the town. Either way event
    /// 1 names the owner, every ICF becomes theirs and peace lasts <see cref="PeaceSeconds"/>.</item>
    /// </list>
    /// The client ignores an event whose serial is not newer than what it has, so one counter numbers every city
    /// record and event. Medal points (Medal of Richmond / Newman; our amounts): +<see cref="MedalJoin"/> for
    /// joining a war, +<see cref="MedalCapture"/> per ICF taken, +<see cref="MedalWin"/> for the winners who took
    /// part, sent in 0x8034's medal list. Only the Earth server runs the towns; the Space server reports their state
    /// from the occupation_city table.
    /// </summary>
    public static class Occupation
    {
        public const int StatusPeace = 0;
        public const int StatusWindow = 1;
        public const int StatusWar = 2;

        public const uint EventWarStarted = 0;
        public const uint EventWarOver = 1;
        public const uint EventCaptured = 2;
        public const uint EventWindowOpen = 4;

        public const int IcfCount = 5;
        public const int TowerTemplate = 340004;
        public const int TowerHealth = 500;

        /// <summary>
        /// Half the side of the town area (TOWNINFOTEMPLATE, x4): registering needs the outer box, capturing the inner.
        /// </summary>
        public const int OuterHalf = 24000;
        public const int InnerHalf = 16000;

        /// <summary>
        /// The client's capture time (FLAGPOINTTEMPLATE, 180 s): a player's captures must be at least this far apart,
        /// less a tenth for lag, and they must have been registered that long (GameServer.xml
        /// OccupationCaptureSeconds; lower it only for testing, the client still takes 180 s).
        /// </summary>
        public static int CaptureSeconds = 180;

        /// <summary>
        /// GameServer.xml OccupationWarMinutes (60, as the official wars) and OccupationPeaceMinutes (180; the
        /// captures show 3 hours, once 1 hour).
        /// </summary>
        public static int WarSeconds = 3600;
        public static int PeaceSeconds = 3 * 3600;

        /// <summary>
        /// GameServer.xml OccupationEnabled.
        /// </summary>
        public static bool Enabled = true;

        public static int MedalJoin = 1;
        public static int MedalCapture = 5;
        public static int MedalWin = 10;

        private static readonly List<OccupationCity> cities = new List<OccupationCity>
        {
            new OccupationCity(58, "Richmond", 2, 68932000, -52864000, new[]
            {
                new[] { 68937311, -52858792, 1672, 0 }, new[] { 68939768, -52867574, 1672, 0 }, new[] { 68923160, -52877066, 1868, 0 },
            }),
            new OccupationCity(59, "Newman", 1, 56932000, -54464000, new[]
            {
                new[] { 56944253, -54458061, 2260, 28581 }, new[] { 56924237, -54463208, 2260, 195 }, new[] { 56931839, -54473088, 2260, -18209 },
            }),
        };

        private static int serial = GameWorld.UnixTime();
        private static Timer timer;
        private static bool running;

        public static IList<OccupationCity> Cities { get { return cities; } }

        public static OccupationCity Get(int cityID)
        {
            return cities.FirstOrDefault(c => c.ID == cityID);
        }

        public static uint NextSerial()
        {
            return (uint)Interlocked.Increment(ref serial);
        }

        /// <summary>
        /// The Earth server runs the towns: reads their state, puts up the towers and checks the clock every second.
        /// </summary>
        public static void Start()
        {
            if (!Enabled)
            {
                return;
            }
            try
            {
                Load();
            }
            catch (Exception ex)
            {
                Logger.ShowError("Cannot read the battle towns' state: " + ex.Message);
            }
            foreach (var city in cities)
            {
                lock (city)
                {
                    PlaceTowers(city);
                }
                Logger.ShowInfo(string.Format("{0}: {1}, {2} until {3:u}.", city.Name, FactionName(city.Owner), StatusName(city.Status),
                    FromUnix(city.Time)));
            }
            running = true;
            timer = new Timer(_ => Tick(), null, 1000, 1000);
        }

        /// <summary>
        /// The cities as 0x8070 lists them. The Space server reads them from the database each time.
        /// </summary>
        public static List<OccupationCity> Snapshot()
        {
            if (!running)
            {
                try
                {
                    Load();
                }
                catch (Exception ex)
                {
                    Logger.ShowError("Cannot read the battle towns' state: " + ex.Message);
                }
            }
            return cities.Select(c =>
            {
                lock (c)
                {
                    return c.Copy();
                }
            }).ToList();
        }

        private static void Tick()
        {
            int now = GameWorld.UnixTime();
            foreach (var city in cities)
            {
                try
                {
                    lock (city)
                    {
                        if (city.Status == StatusPeace && now >= city.Time)
                        {
                            city.Status = StatusWindow;
                            Save(city);
                            Logger.ShowInfo(string.Format("{0}: the attack window is open.", city.Name));
                            Broadcast(EventWindowOpen, 0xFFFFFFFF, city, 0xFF, 3, city.Time);
                        }
                        else if (city.Status == StatusWar && now >= city.Time)
                        {
                            Logger.ShowInfo(string.Format("{0}: time is up, {1} keeps the town.", city.Name, FactionName(city.Owner)));
                            Finish(city, city.Owner);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.ShowError(ex);
                }
            }
        }

        /// <summary>
        /// Whether a player of <paramref name="faction"/> may shoot this tower: only the side that does not hold the
        /// town, and only while the window is open or the war is on.
        /// </summary>
        public static bool CanAttackTower(GroundItem tower, Faction faction)
        {
            var city = Get(tower.CityID);
            if (city == null)
            {
                return false;
            }
            lock (city)
            {
                return city.Status != StatusPeace && city.Owner != (ushort)faction;
            }
        }

        /// <summary>
        /// 0x71 (and a hit on a tower): starts the war when the window is open and the player is of the other side,
        /// in the town. Returns null when it started (or was already on), else why not.
        /// </summary>
        public static string StartWar(UCGameSession player, int cityID)
        {
            var city = Get(cityID);
            var coord = player.Coord;
            if (!running || city == null)
            {
                return "no such battle town here";
            }
            lock (city)
            {
                if (city.Status == StatusWar)
                {
                    return null;
                }
                if (city.Status != StatusWindow)
                {
                    return "the attack window is not open";
                }
                if ((ushort)player.Character.Faction == city.Owner)
                {
                    return "their faction holds the town";
                }
                if (coord == null || !city.Contains(coord, OuterHalf))
                {
                    return "not in the town";
                }
                city.Status = StatusWar;
                city.Time = GameWorld.UnixTime() + WarSeconds;
                city.Attacker = (ushort)player.Character.Faction;
                city.StartedBy = player.CharacterID;
                Save(city);
                Logger.ShowInfo(string.Format("{0} started the war for {1}; it ends at {2:u}.", player.Character.Name, city.Name, FromUnix(city.Time)));
                Broadcast(EventWarStarted, player.CharacterID, city, 0xFF, 3, city.Time);
                return null;
            }
        }

        /// <summary>
        /// 0x74: the player entered the town during the war. Null when registered.
        /// </summary>
        public static string Register(UCGameSession player, int cityID)
        {
            var city = Get(cityID);
            var coord = player.Coord;
            if (!running || city == null)
            {
                return "no such battle town here";
            }
            bool joined;
            lock (city)
            {
                if (city.Status != StatusWar)
                {
                    return "there is no war";
                }
                if (coord == null || !city.Contains(coord, OuterHalf))
                {
                    return "not in the town";
                }
                if (!city.Participants.ContainsKey(player.CharacterID))
                {
                    city.Participants[player.CharacterID] = DateTime.UtcNow;
                }
                joined = city.Joined.Add(player.CharacterID);
            }
            if (joined)
            {
                player.AddMedal(city.MedalIndex, MedalJoin);
            }
            return null;
        }

        /// <summary>
        /// 0x75, logout, a flight: the player leaves the war.
        /// </summary>
        public static bool Unregister(UCGameSession player, int cityID)
        {
            var city = Get(cityID);
            if (city == null)
            {
                return false;
            }
            lock (city)
            {
                return city.Participants.Remove(player.CharacterID);
            }
        }

        public static void Leave(UCGameSession player)
        {
            foreach (var city in cities)
            {
                Unregister(player, city.ID);
            }
        }

        /// <summary>
        /// 0x73: the player's 180 s at an ICF are over. Null when it is theirs now.
        /// </summary>
        public static string Capture(UCGameSession player, int cityID, int flag)
        {
            var city = Get(cityID);
            var coord = player.Coord;
            var faction = (ushort)player.Character.Faction;
            if (!running || city == null)
            {
                return "no such battle town here";
            }
            bool won;
            lock (city)
            {
                DateTime since, last;
                var now = DateTime.UtcNow;
                if (city.Status != StatusWar)
                {
                    return "there is no war";
                }
                if (flag < 0 || flag >= IcfCount)
                {
                    return "no ICF " + flag;
                }
                if (!city.Participants.TryGetValue(player.CharacterID, out since))
                {
                    return "not registered for the war";
                }
                if ((now - since).TotalSeconds < CaptureSeconds * 0.9)
                {
                    return "registered only " + (int)(now - since).TotalSeconds + " s ago";
                }
                if (city.LastCapture.TryGetValue(player.CharacterID, out last) && (now - last).TotalSeconds < CaptureSeconds * 0.9)
                {
                    return "captured another ICF " + (int)(now - last).TotalSeconds + " s ago";
                }
                if (coord == null || !city.Contains(coord, InnerHalf))
                {
                    return "not by the ICFs";
                }
                if (player.Character.IsCriminal)
                {
                    return "a criminal";
                }
                if (city.Icf[flag] == faction)
                {
                    return "their faction holds it already";
                }
                city.Icf[flag] = faction;
                city.LastCapture[player.CharacterID] = now;
                won = faction == city.Attacker && city.Icf.All(f => f == faction);
                Save(city);
                Logger.ShowInfo(string.Format("{0} captured ICF {1} of {2} for {3}.", player.Character.Name, flag + 1, city.Name, FactionName(faction)));
                Broadcast(EventCaptured, player.CharacterID, city, (byte)flag, faction, 0);
                if (won)
                {
                    Logger.ShowInfo(string.Format("{0} holds every ICF: {1} takes {2}.", FactionName(faction), FactionName(faction), city.Name));
                    Finish(city, faction);
                }
            }
            player.AddMedal(city.MedalIndex, MedalCapture);
            return null;
        }

        /// <summary>
        /// Ends the war: <paramref name="winner"/> holds the town and every ICF, peace until the next window, the
        /// towers stand again, the winners who took part get their medal points. Call with the city locked.
        /// </summary>
        private static void Finish(OccupationCity city, ushort winner)
        {
            var winners = city.Joined.Select(id => GameWorld.Instance.Get(id))
                .Where(p => p != null && p.InGame && (ushort)p.Character.Faction == winner).ToList();
            city.Owner = winner;
            for (int i = 0; i < IcfCount; i++)
            {
                city.Icf[i] = winner;
            }
            city.Status = StatusPeace;
            city.Time = GameWorld.UnixTime() + PeaceSeconds;
            city.Attacker = 0;
            city.StartedBy = 0;
            city.Participants.Clear();
            city.LastCapture.Clear();
            city.Joined.Clear();
            Save(city);
            PlaceTowers(city);
            Broadcast(EventWarOver, 0xFFFFFFFF, city, 0xFF, winner, city.Time);
            Logger.ShowInfo(string.Format("{0} belongs to {1}; the next attack window opens at {2:u}.", city.Name, FactionName(winner), FromUnix(city.Time)));
            foreach (var p in winners)
            {
                p.AddMedal(city.MedalIndex, MedalWin);
            }
        }

        /// <summary>
        /// Puts the town's three towers up at full health (replacing any still standing). Call with the city locked.
        /// </summary>
        private static void PlaceTowers(OccupationCity city)
        {
            var template = ItemTemplates.Get(TowerTemplate);
            for (int i = 0; i < city.Towers.Length; i++)
            {
                if (city.TowerIDs[i] != 0)
                {
                    var old = GameWorld.Instance.Take(city.TowerIDs[i], g => true);
                    if (old != null)
                    {
                        BroadcastGround(SM_UPDATE_ITEM_INFO.ItemPickedUp, old);
                    }
                }
                var t = city.Towers[i];
                var node = new ItemNode(PlayerInventory.NewUniqueID(), ItemNode.Multi, TowerTemplate)
                {
                    Name = template != null ? template.Name : "LCT",
                    Health = TowerHealth,
                    MaxHealth = TowerHealth,
                    Created = GameWorld.UnixTime(),
                    Modified = GameWorld.UnixTime(),
                };
                var rotation = new byte[] { 0, 0, 0, 0, (byte)(t[3] >> 8), (byte)t[3] };
                var tower = new GroundItem(node, (ushort)Zone.EARTH, t[0], t[1], t[2], rotation, 0xFFFFFFFF)
                {
                    CityID = city.ID,
                    TowerIndex = i,
                };
                GameWorld.Instance.Place(tower);
                city.TowerIDs[i] = tower.UniqueID;
                BroadcastGround(SM_UPDATE_ITEM_INFO.ItemDropped, tower);
            }
        }

        private static void BroadcastGround(uint action, GroundItem g)
        {
            GameWorld.Instance.SendNear(g.ClusterID, g.X, g.Y, UCGameSession.BroadcastDistance, () => new SM_UPDATE_ITEM_INFO(action, g, 0xFFFFFFFF));
        }

        /// <summary>
        /// 0x8076 to every player on the server (the client keeps both towns for its map and town list).
        /// </summary>
        private static void Broadcast(uint type, uint playerID, OccupationCity city, byte flag, ushort faction, int time)
        {
            uint n = NextSerial();
            foreach (var p in GameWorld.Instance.Players)
            {
                if (p.InGame)
                {
                    p.Network.SendPacket(new SM_OCCUPATION_EVENT(type, playerID, city.ID, flag, faction, time, n));
                }
            }
        }

        private const string Table =
            "CREATE TABLE IF NOT EXISTS occupation_city (" +
            " city_id INT NOT NULL PRIMARY KEY," +
            " owner SMALLINT NOT NULL," +
            " status INT NOT NULL," +
            " time INT NOT NULL," +
            " icf VARCHAR(32) NOT NULL," +
            " attacker SMALLINT NOT NULL DEFAULT 0," +
            " started_by INT UNSIGNED NOT NULL DEFAULT 0)";

        /// <summary>
        /// Reads the towns' state; a town without a row starts in peace with its first owner, its window opening
        /// after <see cref="PeaceSeconds"/>.
        /// </summary>
        private static void Load()
        {
            using (var connection = DatabaseConnection.Open())
            {
                using (var cmd = new MySqlCommand(Table, connection))
                {
                    cmd.ExecuteNonQuery();
                }
                var rows = new Dictionary<int, object[]>();
                using (var cmd = new MySqlCommand("SELECT city_id, owner, status, time, icf, attacker, started_by FROM occupation_city", connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var values = new object[7];
                        reader.GetValues(values);
                        rows[Convert.ToInt32(values[0])] = values;
                    }
                }
                foreach (var city in cities)
                {
                    lock (city)
                    {
                        object[] row;
                        if (!rows.TryGetValue(city.ID, out row))
                        {
                            if (!running && city.Time == 0)
                            {
                                city.Reset(GameWorld.UnixTime() + PeaceSeconds);
                                if (Configuration.Instance.Zone == (ushort)Zone.EARTH)
                                {
                                    Save(city, connection);
                                }
                            }
                            continue;
                        }
                        city.Owner = Convert.ToUInt16(row[1]);
                        city.Status = Convert.ToInt32(row[2]);
                        city.Time = Convert.ToInt32(row[3]);
                        var icf = Convert.ToString(row[4]).Split(',');
                        for (int i = 0; i < IcfCount; i++)
                        {
                            ushort f;
                            city.Icf[i] = i < icf.Length && ushort.TryParse(icf[i], out f) ? f : city.Owner;
                        }
                        city.Attacker = Convert.ToUInt16(row[5]);
                        city.StartedBy = Convert.ToUInt32(row[6]);
                    }
                }
            }
        }

        private static void Save(OccupationCity city)
        {
            try
            {
                using (var connection = DatabaseConnection.Open())
                {
                    Save(city, connection);
                }
            }
            catch (Exception ex)
            {
                Logger.ShowError(ex);
            }
        }

        private static void Save(OccupationCity city, MySqlConnection connection)
        {
            using (var cmd = new MySqlCommand(
                "REPLACE INTO occupation_city (city_id, owner, status, time, icf, attacker, started_by) " +
                "VALUES (@id, @owner, @status, @time, @icf, @attacker, @by)", connection))
            {
                cmd.Parameters.AddWithValue("@id", city.ID);
                cmd.Parameters.AddWithValue("@owner", city.Owner);
                cmd.Parameters.AddWithValue("@status", city.Status);
                cmd.Parameters.AddWithValue("@time", city.Time);
                cmd.Parameters.AddWithValue("@icf", string.Join(",", city.Icf));
                cmd.Parameters.AddWithValue("@attacker", city.Attacker);
                cmd.Parameters.AddWithValue("@by", city.StartedBy);
                cmd.ExecuteNonQuery();
            }
        }

        public static string FactionName(ushort faction)
        {
            return faction == (ushort)Faction.ZEON ? "Zeon" : faction == (ushort)Faction.FEDERATION ? "the Federation" : "nobody";
        }

        private static string StatusName(int status)
        {
            return status == StatusWar ? "at war" : status == StatusWindow ? "open to attack" : "at peace";
        }

        private static DateTime FromUnix(int time)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(time);
        }
    }

    public class OccupationCity
    {
        public OccupationCity(int id, string name, ushort firstOwner, int x, int y, int[][] towers)
        {
            ID = id;
            Name = name;
            FirstOwner = firstOwner;
            X = x;
            Y = y;
            Towers = towers;
            TowerIDs = new uint[towers.Length];
            Icf = new ushort[Occupation.IcfCount];
            Participants = new Dictionary<uint, DateTime>();
            LastCapture = new Dictionary<uint, DateTime>();
            Joined = new HashSet<uint>();
            Reset(0);
        }

        public int ID { get; private set; }
        public string Name { get; private set; }
        public ushort FirstOwner { get; private set; }
        public int X { get; private set; }
        public int Y { get; private set; }

        /// <summary>
        /// x, y, z and direction of each laser communication tower (FACILITYITEMARRANGEMENT).
        /// </summary>
        public int[][] Towers { get; private set; }
        public uint[] TowerIDs { get; private set; }

        public ushort Owner { get; set; }

        /// <summary>
        /// <see cref="Occupation.StatusPeace"/>, <see cref="Occupation.StatusWindow"/> or <see cref="Occupation.StatusWar"/>.
        /// </summary>
        public int Status { get; set; }

        /// <summary>
        /// Unix time: in peace when the window opens, in the window when it opened, at war the deadline.
        /// </summary>
        public int Time { get; set; }

        /// <summary>
        /// Who holds each ICF.
        /// </summary>
        public ushort[] Icf { get; private set; }

        public ushort Attacker { get; set; }
        public uint StartedBy { get; set; }

        /// <summary>
        /// Registered players (0x74) and when they registered.
        /// </summary>
        public Dictionary<uint, DateTime> Participants { get; private set; }
        public Dictionary<uint, DateTime> LastCapture { get; private set; }

        /// <summary>
        /// Everyone who took part in this war, for the medals.
        /// </summary>
        public HashSet<uint> Joined { get; private set; }

        /// <summary>
        /// The medal of the town (MEDALTYPEINFO): 0 Richmond, 1 Newman.
        /// </summary>
        public int MedalIndex { get { return ID == 58 ? 0 : 1; } }

        public void Reset(int time)
        {
            Owner = FirstOwner;
            Status = Occupation.StatusPeace;
            Time = time;
            for (int i = 0; i < Icf.Length; i++)
            {
                Icf[i] = FirstOwner;
            }
            Attacker = 0;
            StartedBy = 0;
        }

        public bool Contains(CoordData c, int half)
        {
            return c.ClusterID == (ushort)Zone.EARTH && Math.Abs((long)c.X - X) <= half && Math.Abs((long)c.Y - Y) <= half;
        }

        public OccupationCity Copy()
        {
            var copy = new OccupationCity(ID, Name, FirstOwner, X, Y, Towers)
            {
                Owner = Owner,
                Status = Status,
                Time = Time,
            };
            Array.Copy(Icf, copy.Icf, Icf.Length);
            return copy;
        }
    }
}
