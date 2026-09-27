using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x70: asks who holds the occupation cities (body: 0, character id, 0xFF).
    /// Java reference: RequestOccupationCityInfoList.java.
    /// </summary>
    public class CM_OCCUPATION_CITY_INFO_LIST : UCPacket<GSOpcode>
    {
        public CM_OCCUPATION_CITY_INFO_LIST()
        {
            this.ID = GSOpcode.CM_OCCUPATION_CITY_INFO_LIST;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_OCCUPATION_CITY_INFO_LIST();
        }

        public override void OnProcess(Session<GSOpcode> client)
        {
            ((UCGameSession)client).OnOccupationCityInfoList(this);
        }
    }
}
