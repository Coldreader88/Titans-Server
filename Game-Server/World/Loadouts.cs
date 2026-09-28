using System;
using System.Collections.Generic;
using System.Linq;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// A random loadout for an MS or MA a GM spawns without naming an engine: a ranged weapon with ammo, a
    /// melee weapon, a shield that fits the model, and an MS/MA typeA lv.3 engine (rocket in Space, jet on
    /// Earth). Weapons and shields named for a model ("Shield(RX-78)", "ZAKU bazooka(MS-06)") are only
    /// given to that model; the rest (MS head vulcan, Heat hawk, Hyper bazooka) go on anything. The client
    /// data has no weapon-to-ammo table, so ammo is picked from the weapon's name.
    /// </summary>
    public static class Loadouts
    {
        public const int RocketEngineTypeALv3 = 290003;
        public const int JetEngineTypeALv3 = 290033;

        private static readonly Random random = new Random();

        private static readonly string[] Excluded = { "tank/fighter", "bb ", "tool kit", "drill", "magella", "ball cannon", "throwing device" };
        private static readonly string[] Melee = { "saber", "hawk", "sword", "claw", "punch", "grapple", "heat rod", "naginata", "hammer" };

        /// <summary>
        /// Whether #spawn gives this vehicle a random loadout: mobile suits and mobile armors.
        /// </summary>
        public static bool Applies(ItemTemplate vehicle)
        {
            return vehicle != null && (vehicle.Category == "ms" || vehicle.Category == "ma" || vehicle.Category == "eventms");
        }

        public static int Engine(ushort zone)
        {
            return zone == 2 ? RocketEngineTypeALv3 : JetEngineTypeALv3;
        }

        /// <summary>
        /// The vehicle's container.child value (see PlayerInventory.NewVehicle): "@slot-weapon" for the
        /// armaments (0 ranged, 1 shield, 2 melee) and "item-amount" for the ammo in its inventory.
        /// <paramref name="summary"/> names what it got, for the GM.
        /// </summary>
        public static string Random(ItemTemplate vehicle, out string summary)
        {
            var template = VehicleTemplates.Get(vehicle.ID);
            string model = Normalize(template != null ? template.Model : vehicle.Name);
            bool armor = vehicle.Category == "ma";

            var weapons = ItemTemplates.InCategory("weapon").Where(w => w.ForSale && Fits(w.Name, model, armor)).ToList();
            var ranged = weapons.Where(w => !IsMelee(w.Name) && AmmoFor(w.Name) > 0).ToList();
            var melee = weapons.Where(w => IsMelee(w.Name)).ToList();
            var shields = armor ? new List<ItemTemplate>()
                : ItemTemplates.InCategory("shield").Where(s => s.ForSale && Code(s.Name) != null && Fits(s.Name, model, false)).ToList();

            var entries = new List<string>();
            var names = new List<string>();
            lock (random)
            {
                var gun = Pick(ranged);
                if (gun != null)
                {
                    int ammo = AmmoFor(gun.Name);
                    int amount = Math.Max(gun.Magazine, 20) * 5;
                    entries.Add("@0-" + gun.ID);
                    entries.Add(ammo + "-" + amount);
                    var ammoTemplate = ItemTemplates.Get(ammo);
                    names.Add(gun.Name + " with " + amount + " " + (ammoTemplate != null ? ammoTemplate.Name : ammo.ToString()));
                }
                var shield = Pick(shields);
                if (shield != null)
                {
                    entries.Add("@1-" + shield.ID);
                    names.Add(shield.Name);
                }
                var blade = Pick(melee);
                if (blade != null)
                {
                    entries.Add("@2-" + blade.ID);
                    names.Add(blade.Name);
                }
            }
            summary = names.Count > 0 ? string.Join(", ", names) : "no weapons";
            return string.Join(" ", entries);
        }

        private static ItemTemplate Pick(List<ItemTemplate> list)
        {
            return list.Count > 0 ? list[random.Next(list.Count)] : null;
        }

        private static bool IsMelee(string name)
        {
            var n = name.ToLowerInvariant();
            return Melee.Any(m => n.Contains(m));
        }

        private static bool Fits(string name, string model, bool armor)
        {
            var n = name.ToLowerInvariant();
            // "MA ..." weapons only go on mobile armors; "SP ..." are special editions.
            if (Excluded.Any(x => n.Contains(x)) || n.StartsWith("sp ") || (n.StartsWith("ma ") && !armor))
            {
                return false;
            }
            var code = Code(name);
            return code == null || model.Contains(code);
        }

        /// <summary>
        /// The model a weapon or shield is made for, from the end of its name: "Shield(RGM-79G)" is "rgm-79g".
        /// </summary>
        private static string Code(string name)
        {
            int open = name.LastIndexOf('('), close = name.LastIndexOf(')');
            return open >= 0 && close > open + 1 ? Normalize(name.Substring(open + 1, close - open - 1)) : null;
        }

        private static string Normalize(string s)
        {
            return (s ?? "").ToLowerInvariant().Replace("(", "").Replace(")", "").Replace(" ", "");
        }

        /// <summary>
        /// The ammunition (54xxxx) a weapon fires, from its name; 0 for none known.
        /// </summary>
        public static int AmmoFor(string name)
        {
            var n = name.ToLowerInvariant();
            if (n.Contains("beam") || n.Contains("mega particle"))
            {
                return 540002;
            }
            if (n.Contains("vulcan"))
            {
                return 540001;
            }
            if (n.Contains("rocket launcher"))
            {
                return 540011;
            }
            if (n.Contains("bazooka"))
            {
                return 540009;
            }
            if (n.Contains("boomerang"))
            {
                return 540010;
            }
            if (n.Contains("missile") || n.Contains("torpedo"))
            {
                return 540007;
            }
            if (n.Contains("cannon") || n.Contains("launcher"))
            {
                return 540004;
            }
            if (n.Contains("machine gun") || n.Contains("rifle") || n.Contains("zmp") || n.Contains("mmp") || n.Contains("gmg") || n.Contains("br.g"))
            {
                return 540000;
            }
            return 0;
        }
    }
}
