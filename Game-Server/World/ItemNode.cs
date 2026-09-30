using System.Collections.Generic;
using Common.Network.Packets;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// A container or an item as the item info packets (0x16 / 0x8016) describe it. Containers and items
    /// share one shape: the character's containers (backpack, hangar, ...) hold items, and some items
    /// (vehicles) hold containers of their own.
    ///
    /// The client walks the tree: it asks for each top-level container from the player info (0x38002),
    /// then for every child the replies list.
    /// </summary>
    public class ItemNode
    {
        /// <summary>
        /// 0x14: a container holding a list of children. 0x13: a single item or amount (money).
        /// </summary>
        public const int Multi = 0x14;
        public const int Singleton = 0x13;

        /// <summary>
        /// The creation time the official server sent for a character's containers
        /// (Wed, 26 Jul 2006 06:01:57 GMT; Java: NotifyItemInfo).
        /// </summary>
        public const int ContainerTimestamp = 0x44C70556;

        public ItemNode(uint uniqueID, int format, int staticID)
        {
            UniqueID = uniqueID;
            Format = format;
            StaticID = staticID;
            Amount = format == Multi ? 1 : 0;
            Created = ContainerTimestamp;
            Modified = -1;
            Colour = -1;
            Children = new List<ItemNode>();
        }

        /// <summary>
        /// For clothes: the colour, a slot of the template's colour list (<see cref="ClothesColours"/>); -1 for
        /// items that have none. Sent as the fourth option, as the official server did for every piece of clothing.
        /// </summary>
        public int Colour { get; set; }

        /// <summary>
        /// The item option that holds a clothes item's colour.
        /// </summary>
        public const int ColourOption = 3;

        /// <summary>
        /// Unique id of this container or item; the client asks for it by this id.
        /// </summary>
        public uint UniqueID { get; set; }

        public int Format { get; private set; }

        /// <summary>
        /// The container's static id (see Common.Characters.PlayerContainers) or the item's template id.
        /// </summary>
        public int StaticID { get; private set; }

        /// <summary>
        /// 1 for a list container, the money for the money container, the stack size for an item.
        /// </summary>
        public int Amount { get; set; }

        public int Created { get; set; }
        public int Modified { get; set; }

        /// <summary>
        /// Optional fields after the timestamps: Format - 14 of them (5 for an item, 6 for a list).
        /// Each entry is written as is, so it carries its own UC size; null writes an empty field (0x80).
        /// A vehicle keeps its stats in the sixth field.
        /// </summary>
        public byte[][] Options { get; set; }

        public ItemNode Parent { get; private set; }

        public List<ItemNode> Children { get; private set; }

        public string Name { get; set; }

        /// <summary>
        /// An empty slot of a container with fixed slots (weared): unique id 0, format 0 and the given
        /// static id (-1 for the vehicle slot, 0 for a clothing slot, as the official server sent them).
        /// The client never asks for it.
        /// </summary>
        public static ItemNode EmptySlot(int staticID)
        {
            return new ItemNode(0, 0, staticID);
        }

        /// <summary>
        /// For a vehicle: its engine id, as stored in the container table's item_amount.
        /// </summary>
        public int EngineID { get; set; }

        /// <summary>
        /// For a vehicle: its current and maximum health (also in its stats field), for the ground item records.
        /// </summary>
        public int Health { get; set; }
        public int MaxHealth { get; set; }

        /// <summary>
        /// For a vehicle: its upgrade levels, packed as in its stats field (see <see cref="Improvements"/>).
        /// </summary>
        public int Improvement { get; set; }

        /// <summary>
        /// For a weapon or shield: its stats list, which its description carries instead of children (7 ints:
        /// durability, max durability, power, a rate-like value, range, 0, 1000; a shield: durability, max
        /// durability, 0, 0, 0, 0, 1000), and the rounds loaded in it (its fifth option). Null for other items.
        /// Layout from the official 0x8016 / 0x8021 descriptions of weapons.
        /// </summary>
        public int[] Stats { get; set; }
        public int Loaded { get; set; }

        public bool IsEquipment { get { return Stats != null; } }

        /// <summary>
        /// This node and everything under it.
        /// </summary>
        public IEnumerable<ItemNode> Descendants()
        {
            yield return this;
            foreach (var child in Children)
            {
                foreach (var node in child.Descendants())
                {
                    yield return node;
                }
            }
        }

        /// <summary>
        /// True for an <see cref="EmptySlot"/>.
        /// </summary>
        public bool IsEmptySlot { get { return UniqueID == 0; } }

        public ItemNode Add(ItemNode child)
        {
            child.Parent = this;
            Children.Add(child);
            return child;
        }

        public bool Remove(ItemNode child)
        {
            if (!Children.Remove(child))
            {
                return false;
            }
            child.Parent = null;
            return true;
        }

        /// <summary>
        /// Puts <paramref name="child"/> in slot <paramref name="index"/> of a container with fixed slots
        /// (weared), replacing what was there. Returns the node it replaced.
        /// </summary>
        public ItemNode SetSlot(int index, ItemNode child)
        {
            var old = Children[index];
            old.Parent = null;
            child.Parent = this;
            Children[index] = child;
            return old;
        }

        /// <summary>
        /// Writes the unique id, format and static id, as containers list their children.
        /// </summary>
        public void WriteReference<T>(UCPacket<T> p)
        {
            p.PutUIntBE(UniqueID);
            p.PutIntBE(Format);
            p.PutIntBE(StaticID);
        }

        /// <summary>
        /// Writes the description the client asked for: the node, its options and its children.
        /// Java reference: InitializeContainerInfo.createTail. Matches the official 0x8016 replies in
        /// UCGOZone-Login.pcap.
        /// </summary>
        public byte[] BuildTail()
        {
            var p = new UCPacket<int>();
            p.PutUIntBE(UniqueID);
            p.PutIntBE(Format);
            p.PutIntBE(0);
            p.PutIntBE(Amount);
            p.PutIntBE(StaticID);
            p.PutIntBE(Created);
            p.PutIntBE(Modified);

            int optionCount = Format - 14;
            if (Stats != null)
            {
                for (int i = 0; i < optionCount - 1; i++)
                {
                    p.PutSize(0);
                }
                p.PutSize(1);
                p.PutIntBE(Loaded);
                p.PutSize(Stats.Length);
                foreach (var value in Stats)
                {
                    p.PutIntBE(value);
                }
                return p.ToArray();
            }

            for (int i = 0; i < optionCount; i++)
            {
                var option = Options != null && i < Options.Length ? Options[i] : null;
                if (i == ColourOption && Colour >= 0 && Format == Singleton)
                {
                    p.PutSize(1);
                    p.PutByte((byte)Colour);
                }
                else if (option == null)
                {
                    p.PutSize(0);
                }
                else
                {
                    p.PutBytes(option);
                }
            }

            p.PutSize(Children.Count);
            foreach (var child in Children)
            {
                child.WriteReference(p);
            }

            return p.ToArray();
        }
    }
}
