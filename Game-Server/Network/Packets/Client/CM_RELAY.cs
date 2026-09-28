using System.Collections.Generic;
using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x39: send a packet to the listed players (lock on: 0x8010; trade uses it too): uint32 BE opcode to send,
    /// UC size count, uint32 BE character id each, then the body, sent unchanged. No reply to the sender.
    /// Layout from the official captures (BATTLE_1.pcap and the trade captures).
    /// </summary>
    public class CM_RELAY : UCPacket<GSOpcode>
    {
        public CM_RELAY()
        {
            this.ID = GSOpcode.CM_RELAY;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_RELAY();
        }

        public uint Opcode { get; private set; }
        public List<uint> Receivers { get; private set; }
        public byte[] Payload { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            if (this.Remaining < 5)
            {
                return;
            }
            Opcode = this.GetUIntBE();
            int count = this.GetSize();
            Receivers = new List<uint>();
            for (int i = 0; i < count && this.Remaining >= 4; i++)
            {
                Receivers.Add(this.GetUIntBE());
            }
            Payload = this.GetBytes((ushort)this.Remaining);

            ((UCGameSession)client).OnRelay(this);
        }
    }
}
