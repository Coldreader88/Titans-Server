using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x0A: asks how a player or NPC it sees looks: uint32 BE 0, uint32 BE id, byte 5.
    /// Java reference: RequestPlayerLooksBuff.java / PlayerLooksBuff.java.
    /// </summary>
    public class CM_PLAYER_LOOKS : UCPacket<GSOpcode>
    {
        public CM_PLAYER_LOOKS()
        {
            this.ID = GSOpcode.CM_PLAYER_LOOKS;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_PLAYER_LOOKS();
        }

        public uint CharacterID { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            Read();

            ((UCGameSession)client).OnPlayerLooks(this);
        }

        public void Read()
        {
            this.GetUIntBE();
            CharacterID = this.GetUIntBE();
        }
    }
}
