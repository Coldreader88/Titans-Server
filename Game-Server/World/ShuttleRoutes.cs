using System;
using Common.Characters;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// The shuttle routes between Earth and Space, from the client's DAT_0002 LAUNCH_EF/LAUNCH_ZEON/REENTRY_EF/
    /// REENTRY_ZEON.LST (from town, to town) and the towns' positions in PARTLIST.ADF (map units x 4000):
    /// <code>
    /// EF:   Perth Spaceport (47)  -> ISAEO 29 (48)    and back
    /// Zeon: Darwin Spaceport (46) -> ISAEO 28Z (49)   and back
    /// </code>
    /// The client picks the destination from these lists and sends it when it buys its shuttle (0x21 service 3,
    /// bytes 52-59: 1 = launch to Space or 0 = re-entry to Earth, then the destination town); the other server
    /// hands both back in 0x805F, destination first, and the client lands there (Earth To Space.pcap: a Zeon
    /// pilot launched from Darwin with 1, 0x31; Space to Earth.pcap: re-entry from ISAEO 28Z with 0, 0x2E).
    /// </summary>
    public static class ShuttleRoutes
    {
        public class Port
        {
            public int Town;
            public string Name;
            public Zone Zone;
            public int X, Y, Z;
        }

        public static readonly Port Darwin = new Port { Town = 46, Name = "Darwin Spaceport", Zone = Zone.EARTH, X = 62640000, Y = -49056000, Z = 0 };
        public static readonly Port Perth = new Port { Town = 47, Name = "Perth Spaceport", Zone = Zone.EARTH, X = 55456000, Y = -58372000, Z = 0 };
        public static readonly Port Isaeo29 = new Port { Town = 48, Name = "ISAEO 29", Zone = Zone.SPACE, X = -3424000, Y = 5688000, Z = 0 };
        public static readonly Port Isaeo28Z = new Port { Town = 49, Name = "ISAEO 28Z", Zone = Zone.SPACE, X = 6268000, Y = -2816000, Z = 0 };

        /// <summary>
        /// Where a pilot of the faction takes off from, and lands, going to <paramref name="to"/>.
        /// </summary>
        public static void Route(Faction faction, Zone to, out Port from, out Port destination)
        {
            bool zeon = faction == Faction.ZEON;
            Port ground = zeon ? Darwin : Perth;
            Port space = zeon ? Isaeo28Z : Isaeo29;
            from = to == Zone.SPACE ? ground : space;
            destination = to == Zone.SPACE ? space : ground;
        }

        /// <summary>
        /// How far (position units, flat) the point is from the port.
        /// </summary>
        public static double Distance(Port port, int x, int y)
        {
            return Math.Sqrt(Math.Pow((double)port.X - x, 2) + Math.Pow((double)port.Y - y, 2));
        }
    }
}
