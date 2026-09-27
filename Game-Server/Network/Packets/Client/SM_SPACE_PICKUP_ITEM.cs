using Common.Network.Packets;
using TitansUC.GameServer.World;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x8024: confirms a pick-up (see <see cref="CM_SPACE_PICKUP_ITEM"/>): the request with 0x0002 in bytes
    /// 2-3, then the item's description as in 0x8016, with no size before it. The item keeps its unique id.
    /// Checked against the official 0x8024s in BATTLE_1.pcap (an item and several vehicles).
    /// </summary>
    public class SM_SPACE_PICKUP_ITEM : UCPacket<GSOpcode>
    {
        public SM_SPACE_PICKUP_ITEM(CM_SPACE_PICKUP_ITEM request, ItemNode item)
        {
            this.ID = GSOpcode.SM_SPACE_PICKUP_ITEM;

            var body = (byte[])request.Body.Clone();
            body[2] = 0;
            body[3] = 2;
            this.PutBytes(body);
            this.PutBytes(item.BuildTail());
        }
    }
}
