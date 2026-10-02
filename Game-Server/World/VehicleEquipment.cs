using System;
using System.Collections.Generic;
using System.IO;
using Common.Characters;
using SmartEngine.Core;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// Which weapons and shields fit which armament slot of a vehicle, from the client's
    /// DB/Templates/VEHICLEEQUIPMENTTEMPLATE.DAT.
    ///
    /// <code>
    /// uint32 BE 0, UC size = number of records, then per record:
    /// int32 BE   equipment group (= the vehicle template's <see cref="VehicleTemplate.EquipGroup"/>)
    /// UC size    + int32 BE per armament slot (a slot code, meaning unknown)
    /// UC size    + per slot: UC size + one byte per item kind (1 = an item of that kind fits the slot)
    /// </code>
    /// The client's CanEquip (uc.exe 0x7f07dc) takes the vehicle template, the armament slot and the item
    /// template, and allows it only when masks[slot][item kind] is set; the kind is the int32 after the item
    /// template's id (<see cref="ItemTemplate.Kind"/>: 1 beam gun, 2 shield, 3 beam saber, 4 head vulcan, ...).
    /// The GM, for example, has slot 0 guns and beam sabers, slot 1 shields, slot 2 like slot 0, slot 3 the head
    /// vulcan. The weapon names' model codes ("Beam saber(RX-78)") play no part.
    /// </summary>
    public static class VehicleEquipment
    {
        private static readonly object loadLock = new object();
        private static Dictionary<int, byte[][]> groups;

        /// <summary>
        /// Whether the equipment table was found; without it every weapon fits everywhere, as before.
        /// </summary>
        public static bool Loaded
        {
            get
            {
                EnsureLoaded();
                return groups.Count > 0;
            }
        }

        /// <summary>
        /// Number of armament slots the vehicle has (0 for one that carries no weapons).
        /// </summary>
        public static int SlotCount(int vehicleTemplateID)
        {
            var masks = Masks(vehicleTemplateID);
            return masks != null ? masks.Length : 0;
        }

        /// <summary>
        /// Whether an item of kind <paramref name="kind"/> fits armament slot <paramref name="slot"/>.
        /// </summary>
        public static bool Fits(int vehicleTemplateID, int slot, int kind)
        {
            var masks = Masks(vehicleTemplateID);
            return masks != null && slot >= 0 && slot < masks.Length && kind >= 0 && kind < masks[slot].Length &&
                masks[slot][kind] != 0;
        }

        /// <summary>
        /// Whether the weapon or shield <paramref name="itemTemplateID"/> may go in armament slot
        /// <paramref name="slot"/> of the vehicle, as the client decides it. True for everything when the table is
        /// missing.
        /// </summary>
        public static bool CanEquip(int vehicleTemplateID, int slot, int itemTemplateID)
        {
            if (!Loaded)
            {
                return true;
            }
            var item = ItemTemplates.Get(itemTemplateID);
            return item != null && (item.IsWeapon || item.IsShield) && Fits(vehicleTemplateID, slot, item.Kind);
        }

        private static byte[][] Masks(int vehicleTemplateID)
        {
            EnsureLoaded();
            var vehicle = VehicleTemplates.Get(vehicleTemplateID);
            byte[][] masks;
            return vehicle != null && groups.TryGetValue(vehicle.EquipGroup, out masks) ? masks : null;
        }

        private static void EnsureLoaded()
        {
            if (groups != null)
            {
                return;
            }
            lock (loadLock)
            {
                if (groups != null)
                {
                    return;
                }
                var result = new Dictionary<int, byte[][]>();
                try
                {
                    Load(File.ReadAllBytes(CharacterData.FindFile("Templates", "VEHICLEEQUIPMENTTEMPLATE.DAT")), result);
                    Logger.ShowInfo(string.Format("Loaded {0} vehicle equipment groups.", result.Count));
                }
                catch (Exception ex)
                {
                    Logger.ShowWarning("Could not load VEHICLEEQUIPMENTTEMPLATE.DAT, weapons fit every slot: " + ex.Message);
                    result.Clear();
                }
                groups = result;
            }
        }

        private static void Load(byte[] d, Dictionary<int, byte[][]> result)
        {
            var r = new Production.Reader(d, 4);
            int count = r.Size();
            for (int i = 0; i < count; i++)
            {
                int group = r.Int();
                int slots = r.Size();
                for (int k = 0; k < slots; k++)
                {
                    r.Int();
                }
                var masks = new byte[r.Size()][];
                for (int k = 0; k < masks.Length; k++)
                {
                    masks[k] = new byte[r.Size()];
                    for (int m = 0; m < masks[k].Length; m++)
                    {
                        masks[k][m] = r.Byte();
                    }
                }
                result[group] = masks;
            }
        }
    }
}
