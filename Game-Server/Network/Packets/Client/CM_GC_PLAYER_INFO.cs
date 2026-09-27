using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x5F: after a flight between Earth and Space the client did not come through the Lobby, so it asks the
    /// game server for the player info: uint32 BE 0, uint32 BE character id, byte 1.
    /// Java reference: RequestGCPlayerInfo.java; layout from Earth_To_Space.pcap.
    /// </summary>
    public class CM_GC_PLAYER_INFO : UCPacket<GSOpcode>
    {
        public CM_GC_PLAYER_INFO()
        {
            this.ID = GSOpcode.CM_GC_PLAYER_INFO;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_GC_PLAYER_INFO();
        }

        public uint CharacterID { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            this.GetUIntBE();
            CharacterID = this.GetUIntBE();

            ((UCGameSession)client).OnGCPlayerInfo(this);
        }
    }
}
