using Common.Network.Packets;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x8018: confirms a repair: uint16 BE service (5), uint16 BE 2, character id, vehicle unique id and
    /// format, container unique id and format, 0, price, FFFFFFFF, 1, 0, UC size 0.
    /// Layout from the official replies (Geting_Repaired.pcap, Repair.pcap).
    /// </summary>
    public class SM_PAY_REPAIR : UCPacket<GSOpcode>
    {
        public SM_PAY_REPAIR(CM_PAY_REPAIR request, int price)
        {
            this.ID = GSOpcode.SM_PAY_REPAIR;

            this.PutUShortBE((ushort)request.Service);
            this.PutUShortBE(0x0002);
            this.PutUIntBE(request.CharacterID);
            this.PutUIntBE(request.VehicleUniqueID);
            this.PutIntBE(request.VehicleFormat);
            this.PutUIntBE(request.ContainerUniqueID);
            this.PutIntBE(request.ContainerFormat);
            this.PutIntBE(0);
            this.PutIntBE(price);
            this.PutIntBE(-1);
            this.PutIntBE(1);
            this.PutIntBE(0);
            this.PutSize(0);
        }
    }
}
