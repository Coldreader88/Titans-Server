using Common.Network.Packets;
using TitansUC.GameServer.World;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x8032: the result of mining: the request's first 35 bytes with the result code in bytes 2-3 and the
    /// weapon durability used in byte 34, then UC size items (UC_ItemIDCount: uint32 BE unique id, format,
    /// template id, int64 BE count, uint32 BE status 0).
    ///
    /// The client (UCC_Eternal::NotifyExcavation, uc.exe 0x7cc948) takes the durability off the weapon
    /// whatever the code; on code 2 it adds each count to the item with that unique id, or makes a new item in
    /// the cargo of the vehicle named in the reply. Codes: 2 success, 0x0A/0x0C failed, 0x2E "better change
    /// place", 0x2F not enough skill for what is left, 0x30 container full, anything else "Mine item error".
    /// </summary>
    public class SM_EXCAVATION : UCPacket<GSOpcode>
    {
        public SM_EXCAVATION(byte[] request, ushort code, ItemNode item, int count)
        {
            this.ID = GSOpcode.SM_EXCAVATION;

            var head = Bytes.Slice(request, 0, CM_EXCAVATION.HeaderLength);
            head[2] = (byte)(code >> 8);
            head[3] = (byte)code;
            head[34] = 1;
            this.PutBytes(head);
            if (item == null)
            {
                this.PutSize(0);
                return;
            }
            this.PutSize(1);
            this.PutUIntBE(item.UniqueID);
            this.PutIntBE(item.Format);
            this.PutIntBE(item.StaticID);
            this.PutIntBE(0);
            this.PutIntBE(count);
            this.PutIntBE(0);
        }
    }
}
