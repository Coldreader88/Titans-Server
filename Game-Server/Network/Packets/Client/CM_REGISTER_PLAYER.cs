using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x38: sent right after the game login: uint32 BE 0, uint32 BE character id, byte 1.
    /// Java reference: RequestRegisterPlayer.java.
    /// </summary>
    public class CM_REGISTER_PLAYER : UCPacket<GSOpcode>
    {
        public CM_REGISTER_PLAYER()
        {
            this.ID = GSOpcode.CM_REGISTER_PLAYER;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_REGISTER_PLAYER();
        }

        public uint CharacterID { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            Read();

            ((UCGameSession)client).OnRegisterPlayer(this);
        }

        public void Read()
        {
            this.GetUIntBE();
            CharacterID = this.GetUIntBE();
        }
    }
}
