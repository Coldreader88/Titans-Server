using System.Collections.Generic;
using System.Threading;
using Common.Characters;
using SmartEngine.Core;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// A character's containers and items, built from the database when the character enters the game.
    ///
    /// The top-level containers use the unique ids the Lobby sent in the player info
    /// (<see cref="PlayerContainers.GetUniqueID"/>); items get unique ids from a server-wide counter.
    /// Worn clothes (garments table) go in the fixed slots of "weared"; rows of the
    /// container table go in their container, and anything in the hangar is treated as a vehicle.
    /// </summary>
    public class PlayerInventory
    {
        /// <summary>
        /// Static ids of the two containers inside a vehicle (Java: Vehicle.write; seen in
        /// UCGOZone-Login.pcap): its armaments and its inventory.
        /// </summary>
        public const int VehicleArmaments = 120002;
        public const int VehicleInventory = 110010;

        /// <summary>
        /// Weared slot holding the vehicle being piloted (Java: Player.pilot "crowns" it there).
        /// </summary>
        public const int PilotSlot = 0;

        /// <summary>
        /// The clothing slots of weared after the vehicle slot, in order. The official capture has the
        /// combat uniform (dress) in slot 1, gloves in slot 6 and the cap (hat) in slot 7; glasses have
        /// no slot.
        /// </summary>
        public static readonly ApparelType[] WearedSlots =
        {
            ApparelType.DRESS, ApparelType.TOP, ApparelType.COAT, ApparelType.BOTTOM,
            ApparelType.SHOES, ApparelType.GLOVES, ApparelType.HAT,
        };

        private static int nextUniqueID = 0x00400000;

        private readonly object sync = new object();
        private readonly Dictionary<uint, ItemNode> nodes = new Dictionary<uint, ItemNode>();

        /// <summary>
        /// Container table rows this class does not load (unknown containers, weared); saved back unchanged.
        /// </summary>
        private readonly List<CharacterItem> kept = new List<CharacterItem>();

        public List<ItemNode> Containers { get; private set; }

        /// <summary>
        /// The vehicle the player is piloting, or null on foot.
        /// </summary>
        public ItemNode Piloting { get; private set; }

        public PlayerInventory(Character character, IEnumerable<CharacterItem> items)
        {
            Containers = new List<ItemNode>();

            foreach (var container in PlayerContainers.PlayerInfoList)
            {
                var node = new ItemNode(PlayerContainers.GetUniqueID(character.ClientID, container), container.Item3, container.Item2)
                {
                    Name = container.Item1,
                };
                if (container.Item1 == "money")
                {
                    node.Amount = character.Money;
                }
                Containers.Add(node);
                Register(node);
            }

            // Weared has fixed slots (UCGOZone-Login.pcap): slot 0 holds the vehicle being piloted
            // (empty: static id -1), then one slot per piece of clothing. The client reads the slots by
            // position, so empty ones are sent too.
            var weared = Find(PlayerContainers.Weared);
            weared.Add(ItemNode.EmptySlot(-1));
            foreach (var type in WearedSlots)
            {
                var apparel = character.GetApparel(type);
                if (apparel.ItemID > 0)
                {
                    Register(weared.Add(NewItem(apparel.ItemID, 1, type.ToString().ToLowerInvariant())));
                }
                else
                {
                    weared.Add(ItemNode.EmptySlot(0));
                }
            }

            // The official swap pack held a trade pack container, which the client asks for.
            var swapPack = Find(PlayerContainers.SwapPack);
            if (swapPack != null)
            {
                Register(swapPack.Add(new ItemNode(NewUniqueID(), ItemNode.Multi, PlayerContainers.TradePack) { Name = "tradepack" }));
            }

            foreach (var item in items)
            {
                var container = Find(item.ContainerID);
                if (container == null || item.ItemID <= 0 || item.ContainerID == PlayerContainers.Weared)
                {
                    Logger.ShowWarning(string.Format("{0}: skipping item {1} ({2}) in unknown container {3}.",
                        character.Name, item.ItemID, item.Name, item.ContainerID));
                    kept.Add(item);
                    continue;
                }

                if (item.ContainerID == PlayerContainers.Hangar)
                {
                    AddVehicle(container, item);
                }
                else
                {
                    Register(container.Add(NewItem(item.ItemID, item.Amount > 0 ? item.Amount : 1, item.Name)));
                }
            }
        }

        /// <summary>
        /// The container or item with this unique id, or null.
        /// </summary>
        public ItemNode Get(uint uniqueID)
        {
            lock (sync)
            {
                ItemNode node;
                nodes.TryGetValue(uniqueID, out node);
                return node;
            }
        }

        /// <summary>
        /// The top-level container with this static id, or null.
        /// </summary>
        public ItemNode Find(int staticID)
        {
            return Containers.Find(c => c.StaticID == staticID);
        }

        /// <summary>
        /// Takes a vehicle out of the hangar and puts the player in it (0x17 section 8; Java:
        /// MoveItem.hangarRideLogic). Returns null when it cannot.
        /// </summary>
        public ItemNode Ride(uint vehicleUID, uint hangarUID)
        {
            lock (sync)
            {
                var vehicle = GetLocked(vehicleUID);
                var hangar = GetLocked(hangarUID);
                if (Piloting != null || vehicle == null || hangar == null || vehicle.Parent != hangar ||
                    hangar.StaticID != PlayerContainers.Hangar)
                {
                    return null;
                }

                hangar.Remove(vehicle);
                Find(PlayerContainers.Weared).SetSlot(PilotSlot, vehicle);
                Piloting = vehicle;
                return vehicle;
            }
        }

        /// <summary>
        /// Puts the piloted vehicle back in the hangar (0x17 section 9; Java: MoveItem.hangarPutLogic).
        /// Returns null when it cannot.
        /// </summary>
        public ItemNode PutBack(uint vehicleUID, uint hangarUID)
        {
            lock (sync)
            {
                var hangar = GetLocked(hangarUID);
                var vehicle = Piloting;
                if (vehicle == null || vehicle.UniqueID != vehicleUID || hangar == null ||
                    hangar.StaticID != PlayerContainers.Hangar)
                {
                    return null;
                }

                Find(PlayerContainers.Weared).SetSlot(PilotSlot, ItemNode.EmptySlot(-1));
                hangar.Add(vehicle);
                Piloting = null;
                return vehicle;
            }
        }

        /// <summary>
        /// Moves <paramref name="amount"/> of an item between two of the player's containers (0x17 section 7;
        /// Java: MoveItem.bankLogic). Stacks of the same item merge, as the official server did.
        /// </summary>
        public MoveResult Move(uint itemUID, uint sourceUID, uint destUID, int amount)
        {
            lock (sync)
            {
                var item = GetLocked(itemUID);
                var source = GetLocked(sourceUID);
                var dest = GetLocked(destUID);
                if (item == null || source == null || dest == null || item.Parent != source || source == dest ||
                    dest.Format != ItemNode.Multi || !CanHoldItems(source) || !CanHoldItems(dest) ||
                    item.Format != ItemNode.Singleton || amount <= 0 || amount > item.Amount)
                {
                    return null;
                }

                bool movingAll = amount >= item.Amount;
                var stack = dest.Children.Find(c => c.StaticID == item.StaticID && c.Format == ItemNode.Singleton);
                var result = new MoveResult { Item = item };

                if (stack != null)
                {
                    // Official: 0x0301 when the whole stack joins the one there, 0x0401 for part of it.
                    stack.Amount += amount;
                    Touch(stack);
                    result.Target = stack;
                    if (movingAll)
                    {
                        source.Remove(item);
                        Unregister(item);
                        result.Kind = MoveKind.Merged;
                    }
                    else
                    {
                        item.Amount -= amount;
                        Touch(item);
                        result.Kind = MoveKind.AddedToStack;
                    }
                }
                else if (movingAll)
                {
                    source.Remove(item);
                    dest.Add(item);
                    Touch(item);
                    result.Kind = MoveKind.Moved;
                }
                else
                {
                    // Official 0x0201: part of a stack becomes a new item in the destination.
                    item.Amount -= amount;
                    Touch(item);
                    var created = NewItem(item.StaticID, amount, item.Name);
                    Register(dest.Add(created));
                    result.Target = created;
                    result.Kind = MoveKind.Split;
                }

                return result;
            }
        }

        /// <summary>
        /// The rows to write back to the container table: every item in the top-level containers except
        /// weared (clothes are in the garments table). A piloted vehicle is saved in the hangar. Items inside
        /// vehicles are not saved yet.
        /// </summary>
        public List<CharacterItem> ToRows()
        {
            var rows = new List<CharacterItem>();
            lock (sync)
            {
                foreach (var container in Containers)
                {
                    if (container.StaticID == PlayerContainers.Weared || container.Format != ItemNode.Multi)
                    {
                        continue;
                    }
                    foreach (var item in container.Children)
                    {
                        if (item.StaticID == PlayerContainers.TradePack && container.StaticID == PlayerContainers.SwapPack)
                        {
                            continue;
                        }
                        rows.Add(ToRow(container.StaticID, item));
                    }
                }

                if (Piloting != null)
                {
                    rows.Add(ToRow(PlayerContainers.Hangar, Piloting));
                }
                rows.AddRange(kept);
            }
            return rows;
        }

        private static CharacterItem ToRow(int containerID, ItemNode item)
        {
            return new CharacterItem
            {
                ContainerID = containerID,
                ItemID = item.StaticID,
                Name = item.Name,
                Amount = containerID == PlayerContainers.Hangar ? item.EngineID : item.Amount,
            };
        }

        /// <summary>
        /// Containers items can be moved in and out of with section 7: the player's own top-level list
        /// containers other than weared and the hangar (those have their own sections). Containers inside
        /// others (trade pack, vehicle inventories) are not saved yet, so moves into them are refused.
        /// </summary>
        private static bool CanHoldItems(ItemNode container)
        {
            return container.Parent == null && container.Format == ItemNode.Multi &&
                container.StaticID != PlayerContainers.Weared && container.StaticID != PlayerContainers.Hangar;
        }

        private void AddVehicle(ItemNode hangar, CharacterItem item)
        {
            var template = VehicleTemplates.Get(item.ItemID);
            int health = template != null ? template.Health : VehicleTemplates.DefaultHealth;
            int engine = item.Amount > 0 ? item.Amount : (template != null ? template.EngineID : VehicleTemplates.DefaultEngine);

            var vehicle = hangar.Add(new ItemNode(NewUniqueID(), ItemNode.Multi, item.ItemID)
            {
                Name = !string.IsNullOrEmpty(item.Name) ? item.Name : (template != null ? template.Name : null),
                EngineID = item.Amount > 0 ? item.Amount : -1,
            });
            vehicle.Created = UnixNow();
            vehicle.Options = new byte[6][];
            vehicle.Options[5] = VehicleStats(health, health, engine);
            Register(vehicle);

            Register(vehicle.Add(new ItemNode(NewUniqueID(), ItemNode.Multi, VehicleArmaments) { Name = "armaments", Modified = -1, Created = -1 }));
            Register(vehicle.Add(new ItemNode(NewUniqueID(), ItemNode.Multi, VehicleInventory) { Name = "inventory", Modified = -1, Created = -1 }));
        }

        /// <summary>
        /// The vehicle's stats field: 19 ints. The official TGM-79 had 3220 of 3300 health and its engine id
        /// in the 18th (UCGOZone-Login.pcap); the other values are the Java server's constants
        /// (Vehicle.write), which it had not decoded.
        /// </summary>
        private static byte[] VehicleStats(int health, int maxHealth, int engineID)
        {
            var values = new[]
            {
                health, 0x7D0, 0x9C4, 0x9C4, 0xBB8, 0xBB8,
                maxHealth, 0x7D0, 0x9C4, 0x9C4, 0xBB8, 0xBB8,
                0x4B, 0x3E8, 0x19, 0x14, 0, engineID, 0,
            };

            var bytes = new byte[1 + values.Length * 4];
            bytes[0] = (byte)(0x80 | values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                bytes[1 + i * 4] = (byte)(values[i] >> 24);
                bytes[2 + i * 4] = (byte)(values[i] >> 16);
                bytes[3 + i * 4] = (byte)(values[i] >> 8);
                bytes[4 + i * 4] = (byte)values[i];
            }
            return bytes;
        }

        private static ItemNode NewItem(int templateID, int amount, string name)
        {
            int now = UnixNow();
            return new ItemNode(NewUniqueID(), ItemNode.Singleton, templateID)
            {
                Amount = amount,
                Created = now,
                Modified = now,
                Name = name,
            };
        }

        private static void Touch(ItemNode item)
        {
            item.Modified = UnixNow();
        }

        private ItemNode GetLocked(uint uniqueID)
        {
            ItemNode node;
            nodes.TryGetValue(uniqueID, out node);
            return node;
        }

        private void Register(ItemNode node)
        {
            lock (sync)
            {
                nodes[node.UniqueID] = node;
            }
        }

        private void Unregister(ItemNode node)
        {
            nodes.Remove(node.UniqueID);
        }

        private static uint NewUniqueID()
        {
            return (uint)Interlocked.Increment(ref nextUniqueID);
        }

        private static int UnixNow()
        {
            return GameWorld.UnixTime();
        }
    }

    public enum MoveKind
    {
        /// <summary>The item changed containers (official reply 0x0101).</summary>
        Moved,

        /// <summary>Part of a stack became a new item in the destination (0x0201).</summary>
        Split,

        /// <summary>The whole stack joined a stack of the same item (0x0301).</summary>
        Merged,

        /// <summary>Part of a stack joined a stack of the same item (0x0401).</summary>
        AddedToStack,
    }

    public class MoveResult
    {
        public MoveKind Kind { get; set; }

        /// <summary>The item that was moved (for Merged it no longer exists).</summary>
        public ItemNode Item { get; set; }

        /// <summary>The destination stack (Merged, AddedToStack) or the new item (Split).</summary>
        public ItemNode Target { get; set; }
    }
}
