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
            Children = new List<ItemNode>();
        }

        /// <summary>
        /// Unique id of this container or item; the client asks for it by this id.
        /// </summary>
        public uint UniqueID { get; private set; }

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

        public ItemNode Add(ItemNode child)
        {
            child.Parent = this;
            Children.Add(child);
            return child;
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
            for (int i = 0; i < optionCount; i++)
            {
                var option = Options != null && i < Options.Length ? Options[i] : null;
                if (option == null)
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
