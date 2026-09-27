using Common.Network.Packets;
using TitansUC.GameServer.World;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x8023: confirms a drop (see <see cref="CM_SPACE_PLACED_ITEM"/>): the request with 0x0002 in bytes 2-3.
    /// A dropped item gets a new unique id, which replaces the item's in bytes 8-15, and the item's own unique
    /// id and format go in bytes 24-31. A vehicle keeps its id.
    /// Checked against the official 0x8023s (Alumina (11).pcap, Backpack Drop 11222 EF.pcap, BATTLE_1.pcap).
    /// </summary>
    public class SM_SPACE_PLACED_ITEM : UCPacket<GSOpcode>
    {
        public SM_SPACE_PLACED_ITEM(CM_SPACE_PLACED_ITEM request, ItemNode placed)
        {
            this.ID = GSOpcode.SM_SPACE_PLACED_ITEM;

            var body = (byte[])request.Body.Clone();
            body[2] = 0;
            body[3] = 2;
            if (placed.UniqueID != request.ItemUniqueID)
            {
                System.Array.Copy(request.Body, 8, body, 24, 8);
                Bytes.PutU32(body, 8, placed.UniqueID);
                Bytes.PutU32(body, 12, (uint)placed.Format);
            }
            this.PutBytes(body);
        }
    }
}
