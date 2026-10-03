using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Common.Characters;
using SmartEngine.Core;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// #spawnfromlist: puts the players seen in the official packet captures back in the world as NPCs, from
    /// DB/Npcs/captured_players.csv (made by Tools/CapturedPlayers/extract_players.py from UCGO Packet Logs.zip).
    ///
    /// Each line is one player as last seen in the capture that shows them most: name, faction, position, rank, action,
    /// team, vehicle and its weapons, or on foot their looks (gender, skin, face, hair, clothes). The file is read again
    /// on every use, so it can be edited while the server runs.
    ///
    /// Spawned players hold their spots (like #spawn attributes) and do not come back once destroyed. Players of one
    /// captured team are one squad. A vehicle whose weapons the captures never showed gets random ones, a player on foot
    /// whose looks they never showed borrows a captured look of the same faction, and a player without a captured name a
    /// random one (DB/Npcs/names.txt). People on foot cannot be attacked and NPCs leave them alone.
    /// </summary>
    public static class CapturedPlayers
    {
        public const string FileName = "captured_players.csv";

        private class Record
        {
            public int ID;
            public string Name;
            public byte Faction;
            public bool Criminal;
            public ushort Zone;
            public int X, Y, Z;
            public short Tilt, Roll, Direction;
            public byte Rank, Action;
            public int Team;
            public int Vehicle;
            public int[] Armaments;
            public NpcLooks Looks;
        }

        public static List<string> GmCommand(UCGameSession gm, IList<string> args)
        {
            if (args.Count > 0 && args[0].Equals("clear", StringComparison.OrdinalIgnoreCase))
            {
                int removed = NpcManager.Instance.Remove(n => n.FromList);
                return new List<string> { string.Format("Took {0} captured players out of the world.", removed) };
            }
            if (args.Count > 0)
            {
                return new List<string> { "Usage: #spawnfromlist (spawns every player in DB/Npcs/" + FileName + ") | #spawnfromlist clear" };
            }

            List<Record> records;
            try
            {
                records = Load();
            }
            catch (Exception ex)
            {
                return new List<string> { "Cannot read DB/Npcs/" + FileName + ": " + ex.Message };
            }

            ushort zone = TitansUC.GameServer.Configuration.Instance.Zone;
            var here = records.Where(r => r.Zone == zone).ToList();
            // Again: the list replaces the players it spawned before.
            int removedBefore = NpcManager.Instance.Remove(n => n.FromList);

            var names = NpcNames.Pick(here.Count(r => r.Name.Length == 0));
            int nextName = 0;
            var squads = new Dictionary<int, int>();
            int randomWeapons = 0, borrowedLooks = 0;
            foreach (var r in here)
            {
                int squad;
                if (r.Team == -1 || !squads.TryGetValue(r.Team, out squad))
                {
                    squad = NpcManager.Instance.NewSquad();
                    if (r.Team != -1)
                    {
                        squads[r.Team] = squad;
                    }
                }

                int[] armaments = r.Armaments;
                NpcLooks looks = null;
                if (r.Vehicle < 0)
                {
                    armaments = new[] { -1, -1, -1, -1 };
                    looks = r.Looks ?? BorrowLooks(here, r);
                    borrowedLooks += r.Looks == null && looks != null ? 1 : 0;
                }
                else if (armaments == null)
                {
                    var suit = ItemTemplates.Get(r.Vehicle);
                    string weapons;
                    armaments = suit != null && VehicleEquipment.SlotCount(suit.ID) > 0 ? Loadouts.RandomArmaments(suit, out weapons)
                        : new[] { -1, -1, -1, -1 };
                    randomWeapons++;
                }

                string name = r.Name.Length > 0 ? r.Name : names[nextName++];
                var npc = NpcManager.Instance.Spawn(r.Vehicle, r.Faction, r.Zone, r.X, r.Y, r.Z, r.Direction, armaments, name, squad, r.Rank);
                lock (npc)
                {
                    npc.Tilt = r.Tilt;
                    npc.Roll = r.Roll;
                    npc.Action = npc.BaseAction = r.Action;
                    npc.Criminal = r.Criminal;
                    npc.Looks = looks;
                    npc.Stationary = true;
                    npc.FromList = true;
                }
            }

            int elsewhere = records.Count - here.Count;
            Logger.ShowInfo(string.Format("{0} spawned {1} captured players from {2} ({3} on foot, {4} given random weapons, {5} borrowed looks).",
                gm != null ? gm.Character.Name : "The console", here.Count, FileName, here.Count(r => r.Vehicle < 0), randomWeapons, borrowedLooks));
            var lines = new List<string>
            {
                string.Format("Spawned {0} captured players where the captures saw them: {1} in vehicles, {2} on foot.", here.Count,
                    here.Count(r => r.Vehicle >= 0), here.Count(r => r.Vehicle < 0)),
            };
            if (randomWeapons > 0 || borrowedLooks > 0)
            {
                lines.Add(string.Format("The captures never showed the weapons of {0} of them (they got random ones) or the clothes of {1} on foot " +
                    "(they wear another captured player's).", randomWeapons, borrowedLooks));
            }
            if (removedBefore > 0)
            {
                lines.Add(string.Format("They replace the {0} spawned from the list before.", removedBefore));
            }
            if (elsewhere > 0)
            {
                lines.Add(string.Format("{0} more are in {1}; use #spawnfromlist on that server.", elsewhere, zone == (ushort)Common.Characters.Zone.EARTH ? "space" : "on Earth"));
            }
            lines.Add("They hold their spots; #spawnfromlist clear takes them away again.");
            return lines;
        }

        /// <summary>
        /// The looks of a captured player of the same faction on foot (the same one for the same player each time).
        /// </summary>
        private static NpcLooks BorrowLooks(List<Record> records, Record r)
        {
            var donors = records.Where(d => d.Looks != null && d.Faction == r.Faction).ToList();
            if (donors.Count == 0)
            {
                donors = records.Where(d => d.Looks != null).ToList();
            }
            return donors.Count > 0 ? donors[Math.Abs(r.ID) % donors.Count].Looks : null;
        }

        private static List<Record> Load()
        {
            var path = CharacterData.FindFile("Npcs", FileName);
            var result = new List<Record>();
            foreach (var line in File.ReadAllLines(path))
            {
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("id,"))
                {
                    continue;
                }
                try
                {
                    result.Add(Parse(line.Split(',')));
                }
                catch (Exception ex)
                {
                    Logger.ShowWarning(string.Format("{0}: cannot read \"{1}\": {2}", FileName, line, ex.Message));
                }
            }
            return result;
        }

        /// <summary>
        /// id,name,faction,criminal,zone,x,y,z,tilt,roll,dir,rank,action,team,vehicle,armaments,gender,skin,face,hair,haircolour,clothes,capture
        /// </summary>
        private static Record Parse(string[] f)
        {
            Func<int, int> i = k => int.Parse(f[k], CultureInfo.InvariantCulture);
            var r = new Record
            {
                ID = i(0),
                Name = f[1].Trim(),
                Faction = (byte)i(2),
                Criminal = i(3) != 0,
                Zone = (ushort)i(4),
                X = i(5),
                Y = i(6),
                Z = i(7),
                Tilt = (short)i(8),
                Roll = (short)i(9),
                Direction = (short)i(10),
                Rank = (byte)i(11),
                Action = (byte)i(12),
                Team = i(13),
                Vehicle = i(14),
            };
            if (f[15].Trim().Length > 0)
            {
                var slots = f[15].Split('/').Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToList();
                while (slots.Count < 4)
                {
                    slots.Add(-1);
                }
                r.Armaments = slots.ToArray();
            }
            if (f.Length > 21 && f[21].Trim().Length > 0)
            {
                var looks = new NpcLooks
                {
                    Gender = (byte)i(16),
                    Skin = (byte)i(17),
                    Face = (byte)i(18),
                    Hair = (byte)i(19),
                    HairColour = (byte)i(20),
                };
                var clothes = f[21].Split('/');
                for (int k = 0; k < 8 && k < clothes.Length; k++)
                {
                    var parts = clothes[k].Split(':');
                    looks.Wear[k] = short.Parse(parts[0], CultureInfo.InvariantCulture);
                    looks.Styles[k] = parts.Length > 1 ? byte.Parse(parts[1], CultureInfo.InvariantCulture) : (byte)0;
                }
                r.Looks = looks;
            }
            return r;
        }
    }
}
