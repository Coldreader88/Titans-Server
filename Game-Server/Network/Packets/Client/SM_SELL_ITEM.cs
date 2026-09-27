using Common.Network.Packets;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x8022: confirms a sale: the request with 2 in bytes 2-3, the price paid in bytes 52-55 and 8 in bytes
    /// 8-11 (9 when part of the stack is left; the official server sent 9 for most partial sales of 101 and
    /// 121, 8 when the whole stack went).
    /// </summary>
    public class SM_SELL_ITEM : UCPacket<GSOpcode>
    {
        public SM_SELL_ITEM(CM_SELL_ITEM request, bool soldAll, int price)
        {
            this.ID = GSOpcode.SM_SELL_ITEM;

            var body = (byte[])request.Body.Clone();
            body[2] = 0;
            body[3] = 2;
            Bytes.PutU32(body, CM_SELL_ITEM.ResultOffset, soldAll ? 8u : 9u);
            Bytes.PutU32(body, CM_SELL_ITEM.PriceOffset, (uint)price);
            this.PutBytes(body);
        }
    }
}
