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
        /// Vehicle health used until MS templates are loaded (Java: Vehicle.write constants).
        /// </summary>
        public const int DefaultVehicleHealth = 2000;

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

        private readonly Dictionary<uint, ItemNode> nodes = new Dictionary<uint, ItemNode>();

        public List<ItemNode> Containers { get; private set; }

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
                if (container == null || item.ItemID <= 0)
                {
                    Logger.ShowWarning(string.Format("{0}: skipping item {1} ({2}) in unknown container {3}.",
                        character.Name, item.ItemID, item.Name, item.ContainerID));
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
            ItemNode node;
            nodes.TryGetValue(uniqueID, out node);
            return node;
        }

        /// <summary>
        /// The top-level container with this static id, or null.
        /// </summary>
        public ItemNode Find(int staticID)
        {
            return Containers.Find(c => c.StaticID == staticID);
        }

        private void AddVehicle(ItemNode hangar, CharacterItem item)
        {
            var vehicle = hangar.Add(new ItemNode(NewUniqueID(), ItemNode.Multi, item.ItemID) { Name = item.Name });
            vehicle.Created = UnixNow();
            vehicle.Options = new byte[6][];
            vehicle.Options[5] = VehicleStats(DefaultVehicleHealth, DefaultVehicleHealth, item.Amount > 0 ? item.Amount : 0);
            Register(vehicle);

            Register(vehicle.Add(new ItemNode(NewUniqueID(), ItemNode.Multi, VehicleArmaments) { Name = "armaments", Modified = -1, Created = -1 }));
            Register(vehicle.Add(new ItemNode(NewUniqueID(), ItemNode.Multi, VehicleInventory) { Name = "inventory", Modified = -1, Created = -1 }));
        }

        /// <summary>
        /// The vehicle's stats field: 19 ints. Java reference: Vehicle.write (health, then constants
        /// the Java authors had not decoded, then the engine id).
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

        private void Register(ItemNode node)
        {
            nodes[node.UniqueID] = node;
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
}
