using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x18: pays a repair shop to repair a vehicle.
    ///
    /// <code>
    /// uint16 BE   5, uint16 BE 0
    /// uint32 BE   character id
    /// uint32 BE   vehicle unique id, format
    /// uint32 BE   container unique id, format
    /// ...        shop details
    /// </code>
    /// Java reference: RequestPayRepair.java; layout from Geting_Repaired.pcap.
    /// </summary>
    public class CM_PAY_REPAIR : UCPacket<GSOpcode>
    {
        public CM_PAY_REPAIR()
        {
            this.ID = GSOpcode.CM_PAY_REPAIR;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_PAY_REPAIR();
        }

        public int Service { get; private set; }
        public uint CharacterID { get; private set; }
        public uint VehicleUniqueID { get; private set; }
        public int VehicleFormat { get; private set; }
        public uint ContainerUniqueID { get; private set; }
        public int ContainerFormat { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            if (this.Remaining < 24)
            {
                return;
            }
            var body = this.GetBytes((ushort)this.Remaining);
            Service = Bytes.U16(body, 0);
            CharacterID = Bytes.U32(body, 4);
            VehicleUniqueID = Bytes.U32(body, 8);
            VehicleFormat = (int)Bytes.U32(body, 12);
            ContainerUniqueID = Bytes.U32(body, 16);
            ContainerFormat = (int)Bytes.U32(body, 20);

            ((UCGameSession)client).OnPayRepair(this);
        }
    }
}
