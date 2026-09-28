using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x3A: send a packet to everyone within a radius (weapon fire effects, gestures): uint32 BE account id,
    /// character id; uint16 BE zone; 6 bytes 0; int32 BE x, y, z; float BE radius; uint32 BE opcode to send
    /// (0x803B); then the body, sent unchanged to everyone in the radius, the sender included.
    /// Layout from the official captures (BATTLE_1.pcap and others).
    /// </summary>
    public class CM_BROADCAST : UCPacket<GSOpcode>
    {
        public CM_BROADCAST()
        {
            this.ID = GSOpcode.CM_BROADCAST;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_BROADCAST();
        }

        public uint CharacterID { get; private set; }
        public int X { get; private set; }
        public int Y { get; private set; }
        public float Radius { get; private set; }
        public uint Opcode { get; private set; }
        public byte[] Payload { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            if (this.Remaining < 36)
            {
                return;
            }
            this.GetUIntBE();
            CharacterID = this.GetUIntBE();
            this.GetBytes(8);
            X = this.GetIntBE();
            Y = this.GetIntBE();
            this.GetIntBE();
            Radius = System.BitConverter.ToSingle(System.BitConverter.GetBytes(this.GetUIntBE()), 0);
            Opcode = this.GetUIntBE();
            Payload = this.GetBytes((ushort)this.Remaining);

            ((UCGameSession)client).OnBroadcast(this);
        }
    }
}
