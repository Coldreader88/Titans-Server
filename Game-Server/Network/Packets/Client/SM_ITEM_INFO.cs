using Common.Network.Packets;
using TitansUC.GameServer.World;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x8016: describes one container or item (see <see cref="ItemNode.BuildTail"/>).
    ///
    /// <code>
    /// uint32 BE   0x00020002
    /// uint32 BE   character id
    /// 6 x uint32 BE the request's unique id, format, parent unique id, parent format, static id, parent static id
    /// uint32 BE   0x0B
    /// UC size    length of the description, then the description
    /// byte       0xFF
    /// </code>
    /// Java reference: NotifyItemInfo.java; matches the official replies in UCGOZone-Login.pcap.
    /// </summary>
    public class SM_ITEM_INFO : UCPacket<GSOpcode>
    {
        public SM_ITEM_INFO(CM_ITEM_INFO request, ItemNode node)
        {
            this.ID = GSOpcode.SM_ITEM_INFO;

            var tail = node.BuildTail();

            this.PutUIntBE(0x00020002);
            this.PutUIntBE(request.CharacterID);
            this.PutUIntBE(request.UniqueID);
            this.PutIntBE(request.Format);
            this.PutUIntBE(request.ParentUniqueID);
            this.PutIntBE(request.ParentFormat);
            this.PutIntBE(request.StaticID);
            this.PutIntBE(request.ParentStaticID);
            this.PutUIntBE(0x0B);
            this.PutSize(tail.Length);
            this.PutBytes(tail);
            this.PutByte(0xFF);
        }
    }
}
