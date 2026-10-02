using System;
using System.Collections.Generic;
using System.Linq;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// A random loadout for an MS or MA a GM spawns without naming an engine, or for a spawned NPC: every
    /// armament slot gets something the client lets that vehicle carry there (<see cref="VehicleEquipment"/>):
    /// a gun in the first slot that takes one, a shield in the first that takes shields, a melee weapon in the
    /// next free one that takes one, and a gun of the slot's own kind (head vulcan, shoulder cannon) in the
    /// rest. Guns come with 5 magazines of the ammunition their template names; the engine is
    /// <see cref="VehicleEngines.ForLoadout"/>.
    /// </summary>
    public static class Loadouts
    {
        private static readonly Random random = new Random();

        private static readonly string[] Excluded = { "tank/fighter", "bb ", "tool kit", "drill", "throwing device", "flag" };
        private static readonly string[] Melee = { "saber", "hawk", "sword", "claw", "punch", "grapple", "heat rod", "naginata", "hammer" };

        /// <summary>
        /// Whether #spawn gives this vehicle a random loadout: mobile suits and mobile armors.
        /// </summary>
        public static bool Applies(ItemTemplate vehicle)
        {
            return vehicle != null && (vehicle.Category == "ms" || vehicle.Category == "ma" || vehicle.Category == "eventms");
        }

        /// <summary>
        /// The vehicle's container.child value (see PlayerInventory.NewVehicle): "@slot-weapon" for the
        /// armaments and "item-amount" for the ammo in its inventory. <paramref name="summary"/> names what it
        /// got, for the GM.
        /// </summary>
        public static string Random(ItemTemplate vehicle, out string summary)
        {
            var slots = Pick(vehicle);
            var entries = new List<string>();
            var names = new List<string>();
            var ammo = new Dictionary<int, int>();
            for (int i = 0; i < slots.Length; i++)
            {
                var t = slots[i];
                if (t == null)
                {
                    continue;
                }
                entries.Add("@" + i + "-" + t.ID);
                names.Add(t.Name);
                if (t.IsWeapon && t.AmmoID > 0 && t.Magazine > 0)
                {
                    int amount;
                    ammo.TryGetValue(t.AmmoID, out amount);
                    ammo[t.AmmoID] = amount + Math.Max(t.Magazine, 20) * 5;
                }
            }
            foreach (var a in ammo)
            {
                entries.Add(a.Key + "-" + a.Value);
                var ammoTemplate = ItemTemplates.Get(a.Key);
                names.Add(a.Value + " " + (ammoTemplate != null ? ammoTemplate.Name : a.Key.ToString()));
            }
            summary = names.Count > 0 ? string.Join(", ", names) : "no weapons";
            return string.Join(" ", entries);
        }

        /// <summary>
        /// A random loadout for an NPC: the template in each of its armament slots (at least 4; -1 for an empty
        /// slot), as <see cref="Random(ItemTemplate, out string)"/> picks them.
        /// </summary>
        public static int[] RandomArmaments(ItemTemplate vehicle, out string summary)
        {
            var slots = Pick(vehicle);
            var names = slots.Where(t => t != null).Select(t => t.Name).ToList();
            summary = names.Count > 0 ? string.Join(", ", names) : "no weapons";
            var result = slots.Select(t => t != null ? t.ID : -1).ToList();
            while (result.Count < 4)
            {
                result.Add(-1);
            }
            return result.ToArray();
        }

        /// <summary>
        /// A random mobile suit (template) for an NPC, of any model the vehicle tables know.
        /// </summary>
        public static ItemTemplate RandomMobileSuit()
        {
            var suits = ItemTemplates.InCategory("ms").Where(t => t.IsVehicle && VehicleTemplates.Get(t.ID) != null &&
                VehicleEquipment.SlotCount(t.ID) > 0 && !Excluded.Any(x => t.Name.ToLowerInvariant().Contains(x))).ToList();
            lock (random)
            {
                return Pick(suits);
            }
        }

        /// <summary>
        /// What goes in each armament slot of the vehicle (null = nothing).
        /// </summary>
        private static ItemTemplate[] Pick(ItemTemplate vehicle)
        {
            int count = VehicleEquipment.SlotCount(vehicle.ID);
            var slots = new ItemTemplate[count];
            var candidates = ItemTemplates.InCategory("weapon").Concat(ItemTemplates.InCategory("shield"))
                .Where(t => t.ForSale && Usable(t)).ToList();
            Func<int, Func<ItemTemplate, bool>, List<ItemTemplate>> fitting = (slot, what) =>
                candidates.Where(t => what(t) && VehicleEquipment.Fits(vehicle.ID, slot, t.Kind)).ToList();
            Func<ItemTemplate, bool> gun = t => t.IsWeapon && IsGun(t);
            Func<ItemTemplate, bool> shield = t => t.IsShield;
            Func<ItemTemplate, bool> blade = t => t.IsWeapon && !IsGun(t);

            lock (random)
            {
                foreach (var role in new[] { gun, shield, blade })
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (slots[i] == null)
                        {
                            var list = fitting(i, role);
                            if (list.Count > 0)
                            {
                                slots[i] = Pick(list);
                                break;
                            }
                        }
                    }
                }
                for (int i = 0; i < count; i++)
                {
                    if (slots[i] == null)
                    {
                        slots[i] = Pick(fitting(i, gun));
                    }
                }
            }
            return slots;
        }

        private static ItemTemplate Pick(List<ItemTemplate> list)
        {
            return list.Count > 0 ? list[random.Next(list.Count)] : null;
        }

        private static bool IsGun(ItemTemplate t)
        {
            return t.Magazine > 0 && t.AmmoID > 0;
        }

        public static bool IsMelee(string name)
        {
            var n = name.ToLowerInvariant();
            return Melee.Any(m => n.Contains(m));
        }

        /// <summary>
        /// Weapons and shields a spawned loadout may use: not the tank/fighter and battleship ones (no MS takes
        /// them anyway), tool kits, drills, throwing devices, flags or special editions.
        /// </summary>
        private static bool Usable(ItemTemplate t)
        {
            var n = t.Name.ToLowerInvariant();
            return t.Kind > 0 && !Excluded.Any(x => n.Contains(x)) && !n.StartsWith("sp ");
        }
    }
}
