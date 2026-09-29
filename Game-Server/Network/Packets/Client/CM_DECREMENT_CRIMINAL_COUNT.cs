using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x08 DecrementCriminalCount (UC_IDMsg): FF FF FF FF, uint32 BE character id, byte 1. While its criminal
    /// count is above 0 the client sends it every 30 seconds (count 1) or 180 seconds (more), then waits for
    /// 0x8008 before it times the next one. Layout from the client (0x7c0c44); no capture has one.
    /// </summary>
    public class CM_DECREMENT_CRIMINAL_COUNT : UCPacket<GSOpcode>
    {
        public CM_DECREMENT_CRIMINAL_COUNT()
        {
            this.ID = GSOpcode.CM_DECREMENT_CRIMINAL_COUNT;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_DECREMENT_CRIMINAL_COUNT();
        }

        public uint CharacterID { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            if (this.Remaining >= 8)
            {
                this.GetUIntBE();
                CharacterID = this.GetUIntBE();
            }
            ((UCGameSession)client).OnDecrementCriminalCount(this);
        }
    }
}
