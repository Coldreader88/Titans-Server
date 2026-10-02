using System;
using System.Collections.Generic;
using System.Linq;
using Common.Characters;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// Camps (34xxxx), from the client's CAMPTEMPLATE.DAT, SHOPOBJECTTEMPLATE.DAT and the exe (specs/re-unknown-packets.md
    /// section 5):
    /// <list type="bullet">
    /// <item>A camp is bought at the Logistics Center (shops 18 EF, 19 Zeon) and placed with 0x23 mini op 1. The client
    /// checks the rank (required_rank 8) and the ground first; the server checks the rank and the side again.</item>
    /// <item>It stands with 7000 health (max_hp) for <see cref="LifetimeSeconds"/> (the client's INSTITUTION_HBD rows: about
    /// 12 hours), or until it is destroyed; it leaves no wreck.</item>
    /// <item>Clicking a camp opens a facility of the camp towns 37 (EF) and 38 (Zeon): 340000 / 340002 the repair shop,
    /// 340001 / 340003 the weapon shop. The battle towns' base buildings (340005-340014) and some vendor trucks open
    /// the camp towns' facilities too (SHOPOBJECTTEMPLATE). The server lets a player use a camp town's facility only near
    /// such an object (<see cref="UseDistance"/>, OUR rule).</item>
    /// </list>
    /// </summary>
    public static class Camps
    {
        public const int Health = 7000;
        public const int RequiredRank = 8;
        public const int LifetimeSeconds = 12 * 60 * 60;

        /// <summary>
        /// How near a camp, base building or vendor truck a player must be to use the camp town's facility. OUR rule.
        /// </summary>
        public const int UseDistance = 10000;

        /// <summary>
        /// SHOPOBJECTTEMPLATE.DAT: object template -> (camp town, facility index).
        /// </summary>
        private static readonly int[][] ShopObjects =
        {
            new[] { 340000, 37, 0 }, new[] { 340009, 37, 0 }, new[] { 1000003, 37, 0 }, new[] { 340001, 37, 1 },
            new[] { 340007, 37, 1 }, new[] { 1000005, 37, 1 }, new[] { 1000007, 37, 2 }, new[] { 1000009, 37, 2 },
            new[] { 1000023, 37, 2 }, new[] { 1030008, 37, 2 }, new[] { 340006, 37, 3 }, new[] { 1000024, 37, 3 },
            new[] { 1030012, 37, 3 }, new[] { 340005, 37, 4 }, new[] { 1000010, 37, 4 }, new[] { 1000021, 37, 4 },
            new[] { 340008, 37, 5 }, new[] { 340002, 38, 0 }, new[] { 340014, 38, 0 }, new[] { 1000004, 38, 0 },
            new[] { 340003, 38, 1 }, new[] { 340012, 38, 1 }, new[] { 1000006, 38, 1 }, new[] { 1000008, 38, 2 },
            new[] { 1000025, 38, 2 }, new[] { 1030009, 38, 2 }, new[] { 1030010, 38, 2 }, new[] { 1030011, 38, 2 },
            new[] { 340011, 38, 3 }, new[] { 1000026, 38, 3 }, new[] { 1030013, 38, 3 }, new[] { 340010, 38, 4 },
            new[] { 1000022, 38, 4 }, new[] { 1030014, 38, 4 }, new[] { 340013, 38, 5 }, new[] { 1000015, 60, 0 },
            new[] { 1000014, 60, 1 }, new[] { 1000011, 60, 3 }, new[] { 1000012, 60, 4 }, new[] { 1000013, 60, 5 },
            new[] { 1000020, 61, 0 }, new[] { 1000019, 61, 1 }, new[] { 1000016, 61, 3 }, new[] { 1000017, 61, 4 },
            new[] { 1000018, 61, 5 },
        };

        private static readonly HashSet<int> ObjectTowns = new HashSet<int>(ShopObjects.Select(o => o[1]));

        /// <summary>
        /// A camp a player places (340000-340003).
        /// </summary>
        public static bool IsCamp(int templateID)
        {
            return templateID >= 340000 && templateID <= 340003;
        }

        /// <summary>
        /// A battle town's base building (340005-340014; 340004 is the laser communication tower).
        /// </summary>
        public static bool IsBaseBuilding(int templateID)
        {
            return templateID >= 340005 && templateID <= 340014;
        }

        /// <summary>
        /// The side a camp or base building belongs to (CAMPTEMPLATE faction): 340000-340003 by their names, the base
        /// buildings 340005-340009 EF, 340010-340014 Zeon.
        /// </summary>
        public static Faction FactionOf(int templateID)
        {
            if (IsCamp(templateID))
            {
                return templateID <= 340001 ? Faction.FEDERATION : Faction.ZEON;
            }
            return templateID <= 340009 ? Faction.FEDERATION : Faction.ZEON;
        }

        /// <summary>
        /// Health of a base building (CAMPTEMPLATE max_hp): hangars 250000, the rest 100000.
        /// </summary>
        public static int BaseHealth(int templateID)
        {
            return templateID == 340005 || templateID == 340010 ? 250000 : 100000;
        }

        /// <summary>
        /// Whether the town's facilities are opened by objects in the field (camps, base buildings, vendor trucks)
        /// rather than standing in a town.
        /// </summary>
        public static bool IsObjectTown(int town)
        {
            return ObjectTowns.Contains(town);
        }

        /// <summary>
        /// Why a player at that position cannot use facility <paramref name="index"/> of a camp town (-1: any of its
        /// facilities), or null: one of the objects that open it must be within <see cref="UseDistance"/>.
        /// </summary>
        public static string FarRefusal(int town, int index, ushort zone, int x, int y)
        {
            var templates = new HashSet<int>(ShopObjects.Where(o => o[1] == town && (index < 0 || o[2] == index)).Select(o => o[0]));
            Func<int, int, bool> near = (ox, oy) => Math.Abs((long)ox - x) <= UseDistance && Math.Abs((long)oy - y) <= UseDistance;
            if (GameWorld.Instance.AllGround().Any(g => g.ClusterID == zone && templates.Contains(g.Node.StaticID) &&
                g.Node.Health > 0 && near(g.X, g.Y)))
            {
                return null;
            }
            if (NpcManager.Instance != null && NpcManager.Instance.All.Any(n => n.Alive && n.Zone == zone &&
                templates.Contains(n.TemplateID) && near(n.X, n.Y)))
            {
                return null;
            }
            return "there is no camp near you that offers it";
        }
    }
}
