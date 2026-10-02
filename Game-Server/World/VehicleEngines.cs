using System;
using System.Collections.Generic;
using System.Linq;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// Which engines a vehicle can have. The client has no engine slot: a vehicle's engine is chosen when it
    /// is built, and VEHICLEPRODUCTIONTEMPLATE.DAT names the engine kinds its recipe accepts (the ingredient
    /// of range 29; <see cref="Ingredient.Matches"/>). Engine kinds are the int32 after the engine template's
    /// id (<see cref="ItemTemplate.Kind"/>): 1 MS/MA rocket, 2 tank, 3 fighter jet, 4 spaceboat rocket,
    /// 5 MS/MA hydro jet, 6 MS/MA hybrid, 7 general vehicle, 8 MS/MA jet, 9-12 battleship, 13 cargo vehicle.
    /// So a GM or ZAKU II takes a rocket or a jet engine, a Gundam or Gelgoog a hybrid one, a Z'Gok or Acguy a
    /// hydro jet one, cars a general one, tanks and the ThunderGoliath a tank one.
    ///
    /// Vehicles that cannot be built (mobile armors other than the Ball and Oggo, event units, battleships,
    /// shuttles) take the kind of their default engine (the vehicle template's engine id). The default engine
    /// always fits, also for the three vehicles whose recipe names another kind (410052 GM, 410056 GUNTANK2,
    /// 460005 ZAKUTANK).
    /// </summary>
    public static class VehicleEngines
    {
        private static readonly Dictionary<int, string> KindNames = new Dictionary<int, string>
        {
            { 1, "MS/MA rocket" }, { 2, "tank" }, { 3, "fighter jet" }, { 4, "spaceboat rocket" },
            { 5, "MS/MA hydro jet" }, { 6, "MS/MA hybrid" }, { 7, "general vehicle" }, { 8, "MS/MA jet" },
            { 9, "BB rocket" }, { 10, "BB jet" }, { 11, "BB hydro jet" }, { 12, "BB hybrid" }, { 13, "cargo vehicle" },
        };

        // Engine kinds a random loadout prefers: rockets in Space, jets on the Earth, then whatever fits.
        private static readonly int[] SpaceOrder = { 1, 6, 4, 13, 9, 12, 8, 5, 3, 2, 7, 10, 11 };
        private static readonly int[] EarthOrder = { 8, 6, 5, 3, 2, 7, 13, 10, 11, 12, 1, 4, 9 };

        /// <summary>
        /// The engine kinds the vehicle takes (empty when it is not a vehicle or its engine is unknown).
        /// </summary>
        public static List<int> Kinds(int vehicleTemplateID)
        {
            var kinds = new List<int>();
            var recipe = Production.Get(vehicleTemplateID);
            if (recipe != null && recipe.Kind == Recipe.Kinds.Vehicle)
            {
                foreach (var input in recipe.Inputs.Where(i => i.Range == 29 && i.TemplateID < 0))
                {
                    kinds.AddRange(input.Kinds);
                }
            }
            var standard = ItemTemplates.Get(ItemTemplates.EngineOf(vehicleTemplateID));
            if (standard != null)
            {
                kinds.Add(standard.Kind);
            }
            return kinds.Distinct().ToList();
        }

        /// <summary>
        /// Whether the vehicle can have this engine. The vehicle's default engine always fits; so does any engine
        /// of a vehicle whose engines are unknown.
        /// </summary>
        public static bool Fits(int vehicleTemplateID, int engineID)
        {
            if (engineID == ItemTemplates.EngineOf(vehicleTemplateID))
            {
                return true;
            }
            var engine = ItemTemplates.Get(engineID);
            if (engine == null || engineID / 10000 != 29)
            {
                return false;
            }
            var kinds = Kinds(vehicleTemplateID);
            return kinds.Count == 0 || kinds.Contains(engine.Kind);
        }

        /// <summary>
        /// Every engine the vehicle can have, by id.
        /// </summary>
        public static List<ItemTemplate> Fitting(int vehicleTemplateID)
        {
            var kinds = Kinds(vehicleTemplateID);
            return ItemTemplates.InCategory("engine").Where(e => e.ID / 10000 == 29 && kinds.Contains(e.Kind)).ToList();
        }

        /// <summary>
        /// The kinds the vehicle takes, for a GM: "MS/MA rocket or MS/MA jet".
        /// </summary>
        public static string Describe(int vehicleTemplateID)
        {
            var names = Kinds(vehicleTemplateID).Select(k => KindNames.ContainsKey(k) ? KindNames[k] : "kind " + k).ToList();
            return names.Count > 0 ? string.Join(" or ", names) : "unknown";
        }

        /// <summary>
        /// The engine a random loadout gets: typeA lv.3 of the first kind the vehicle takes in the zone's order
        /// (rocket in Space, jet on the Earth), else the vehicle's default engine.
        /// </summary>
        public static int ForLoadout(int vehicleTemplateID, ushort zone)
        {
            var kinds = Kinds(vehicleTemplateID);
            foreach (var kind in zone == 2 ? SpaceOrder : EarthOrder)
            {
                if (!kinds.Contains(kind))
                {
                    continue;
                }
                var engines = ItemTemplates.InCategory("engine").Where(e => e.ID / 10000 == 29 && e.Kind == kind).ToList();
                var pick = engines.FirstOrDefault(e => e.Name.IndexOf("typeA lv.3", StringComparison.OrdinalIgnoreCase) >= 0)
                    ?? engines.FirstOrDefault(e => e.Name.IndexOf("typeA", StringComparison.OrdinalIgnoreCase) >= 0)
                    ?? engines.FirstOrDefault();
                if (pick != null)
                {
                    return pick.ID;
                }
            }
            return ItemTemplates.EngineOf(vehicleTemplateID);
        }

        /// <summary>
        /// An engine by id or name for this vehicle: one that fits when the name matches several (a GM asked for
        /// "jet engine typeA lv.3" gets the MS/MA jet engine, not the fighter one); 0 when none matches at all.
        /// </summary>
        public static int Find(int vehicleTemplateID, string idOrName)
        {
            int id;
            if (int.TryParse(idOrName, out id))
            {
                return ItemTemplates.Get(id) != null && id / 10000 == 29 ? id : 0;
            }
            var text = idOrName.Trim();
            var named = ItemTemplates.InCategory("engine").Where(e => e.ID / 10000 == 29).ToList();
            var exact = named.Where(e => string.Equals(e.Name, text, StringComparison.OrdinalIgnoreCase)).ToList();
            var matches = exact.Count > 0 ? exact : named.Where(e => e.Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            var fitting = matches.FirstOrDefault(e => Fits(vehicleTemplateID, e.ID));
            return fitting != null ? fitting.ID : matches.Count > 0 ? matches[0].ID : 0;
        }
    }
}
