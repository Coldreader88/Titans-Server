using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x13: asks for the server time (body: 0, -1, 0). Java reference: RequestServerTime.java.
    /// </summary>
    public class CM_SERVER_TIME : UCPacket<GSOpcode>
    {
        public CM_SERVER_TIME()
        {
            this.ID = GSOpcode.CM_SERVER_TIME;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_SERVER_TIME();
        }

        public override void OnProcess(Session<GSOpcode> client)
        {
            ((UCGameSession)client).OnServerTime(this);
        }
    }
}
