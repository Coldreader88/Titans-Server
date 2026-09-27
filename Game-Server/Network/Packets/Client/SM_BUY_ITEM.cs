using Common.Network.Packets;
using TitansUC.GameServer.World;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x8021: confirms a purchase (see <see cref="CM_BUY_ITEM"/>).
    ///
    /// <code>
    /// byte       2 = a new item, 1 = added to a stack already in the container
    /// byte       the request's service
    /// uint16 BE   2
    /// uint32 BE   character id
    /// uint32 BE   item unique id, format (the stack's when added)
    /// uint32 BE   container unique id, format
    /// uint32 BE   0
    /// uint32 BE   price paid (all of them)
    /// uint32 BE   item static id, 1, amount bought
    /// UC size    then the new item's description as in 0x8016 (nothing when added)
    /// </code>
    /// Checked against the official 0x8021s: 79 bytes for a new stack, 45 for one added to
    /// (Alumina (410).pcap), 205 for a vehicle (BATTLE_1.pcap).
    /// </summary>
    public class SM_BUY_ITEM : UCPacket<GSOpcode>
    {
        public SM_BUY_ITEM(CM_BUY_ITEM request, BuyResult result, int price)
        {
            this.ID = GSOpcode.SM_BUY_ITEM;

            this.PutByte(result.Added ? (byte)1 : (byte)2);
            this.PutByte((byte)request.Service);
            this.PutUShortBE(0x0002);
            this.PutUIntBE(request.CharacterID);
            this.PutUIntBE(result.Item.UniqueID);
            this.PutIntBE(result.Item.Format);
            this.PutUIntBE(request.DestUniqueID);
            this.PutIntBE(request.DestFormat);
            this.PutIntBE(0);
            this.PutIntBE(price);
            this.PutIntBE(request.StaticID);
            this.PutIntBE(1);
            this.PutIntBE(request.Amount);
            if (result.Added)
            {
                this.PutSize(0);
            }
            else
            {
                var tail = result.Item.BuildTail();
                this.PutSize(tail.Length);
                this.PutBytes(tail);
            }
        }
    }
}
