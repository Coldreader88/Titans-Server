using Common.Network.Packets;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x801C: result of using an ER kit: uint16 BE 6, uint16 BE 2 (success) or 0x0C (failure), uint32 BE
    /// character id, the repaired vehicle's unique id and format, the request's bytes 8-31, then on success
    /// UC size 6 and the health repaired (total, then 5 parts, 0 here), on failure UC size 0.
    /// Layout from MS_ER_1_Success.pcap and MS_ER_1_Fail.pcap.
    /// </summary>
    public class SM_USE_ITEM_BUFF : UCPacket<GSOpcode>
    {
        public SM_USE_ITEM_BUFF(CM_USE_ITEM_BUFF request, uint vehicleUID, int repaired, bool success)
        {
            this.ID = GSOpcode.SM_USE_ITEM_BUFF;

            this.PutUShortBE(6);
            this.PutUShortBE(success ? (ushort)2 : (ushort)0x0C);
            this.PutUIntBE(request.CharacterID);
            this.PutUIntBE(vehicleUID);
            this.PutIntBE(0x14);
            var rest = new byte[24];
            System.Array.Copy(request.Body, 8, rest, 0, 24);
            this.PutBytes(rest);
            if (success)
            {
                this.PutSize(6);
                this.PutIntBE(repaired);
                for (int i = 0; i < 5; i++)
                {
                    this.PutIntBE(0);
                }
            }
            else
            {
                this.PutSize(0);
            }
        }
    }
}
