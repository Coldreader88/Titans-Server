using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x37: a message the client logs to the server: uint32 BE type, then a UC size and ASCII text.
    /// Java reference: ReceiveClientMSG.java.
    /// </summary>
    public class CM_CLIENT_MSG : UCPacket<GSOpcode>
    {
        public CM_CLIENT_MSG()
        {
            this.ID = GSOpcode.CM_CLIENT_MSG;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_CLIENT_MSG();
        }

        public uint Type { get; private set; }

        public string Text { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            Read();

            ((UCGameSession)client).OnClientMsg(this);
        }

        public void Read()
        {
            Type = this.GetUIntBE();
            Text = this.Remaining > 0 ? System.Text.Encoding.ASCII.GetString(this.GetUCBytes()) : string.Empty;
        }
    }
}
