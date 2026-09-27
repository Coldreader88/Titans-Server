using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x06: asks for the name and faction of a player or NPC it sees: uint32 BE 1, uint32 BE id, byte 1.
    /// Java reference: RequestSimplePlayerInfo.java.
    /// </summary>
    public class CM_SIMPLE_PLAYER_INFO : UCPacket<GSOpcode>
    {
        public CM_SIMPLE_PLAYER_INFO()
        {
            this.ID = GSOpcode.CM_SIMPLE_PLAYER_INFO;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_SIMPLE_PLAYER_INFO();
        }

        public uint CharacterID { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            Read();

            ((UCGameSession)client).OnSimplePlayerInfo(this);
        }

        public void Read()
        {
            this.GetUIntBE();
            CharacterID = this.GetUIntBE();
        }
    }
}
