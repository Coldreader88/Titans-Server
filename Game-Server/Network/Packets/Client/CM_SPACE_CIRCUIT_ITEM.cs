using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x05: asks for the items lying around a point.
    ///
    /// <code>
    /// int32 BE    x, y, z
    /// float BE    radius
    /// uint32 BE   list (0, 1 or 2; the client asks for all three)
    /// </code>
    /// Java reference: RequestSpaceCircuitItem.java / SpaceCircuitItem.java; layout from UCGOZone-Login.pcap.
    /// </summary>
    public class CM_SPACE_CIRCUIT_ITEM : UCPacket<GSOpcode>
    {
        public CM_SPACE_CIRCUIT_ITEM()
        {
            this.ID = GSOpcode.CM_SPACE_CIRCUIT_ITEM;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_SPACE_CIRCUIT_ITEM();
        }

        public int X { get; private set; }
        public int Y { get; private set; }
        public int Z { get; private set; }
        public float Radius { get; private set; }
        public byte List { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            Read();

            ((UCGameSession)client).OnSpaceCircuitItem(this);
        }

        public void Read()
        {
            X = this.GetIntBE();
            Y = this.GetIntBE();
            Z = this.GetIntBE();
            Radius = System.BitConverter.ToSingle(System.BitConverter.GetBytes(this.GetUIntBE()), 0);
            List = (byte)this.GetUIntBE();
        }
    }
}
