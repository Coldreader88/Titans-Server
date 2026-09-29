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

        /// <summary>
        /// Mini ops 2 and 4: the item joined <paramref name="stack"/>. Official (Backpack_Pickup_11222_EF.pcap):
        /// the stack's unique id replaces the ground item's in bytes 8-11, the ground item's unique id and format
        /// go in bytes 40-47, and the description is the stack's, with its new amount.
        /// </summary>
        public SM_SPACE_PICKUP_ITEM(CM_SPACE_PICKUP_ITEM request, ItemNode stack, uint groundUniqueID, int groundFormat)
        {
            this.ID = GSOpcode.SM_SPACE_PICKUP_ITEM;

            var body = (byte[])request.Body.Clone();
            body[2] = 0;
            body[3] = 2;
            Bytes.PutU32(body, 8, stack.UniqueID);
            if (body.Length >= 48)
            {
                Bytes.PutU32(body, 40, groundUniqueID);
                Bytes.PutU32(body, 44, (uint)groundFormat);
            }
            this.PutBytes(body);
            this.PutBytes(stack.BuildTail());
        }
    }
}
