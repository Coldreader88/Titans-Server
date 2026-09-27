using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x40: the player takes off in a shuttle for the other side (Earth to Space or back) and asks where to
    /// connect: uint32 BE session key, uint32 BE character id, uint16 BE cluster to go to (1 Earth, 2 Space).
    /// Java reference: RequestReserveAnotherGameFE.java; layout from Earth_To_Space.pcap and Space_to_Earth.pcap.
    /// </summary>
    public class CM_RESERVE_ANOTHER_GAME_FE : UCPacket<GSOpcode>
    {
        public CM_RESERVE_ANOTHER_GAME_FE()
        {
            this.ID = GSOpcode.CM_RESERVE_ANOTHER_GAME_FE;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_RESERVE_ANOTHER_GAME_FE();
        }

        public uint SessionKey { get; private set; }
        public uint CharacterID { get; private set; }
        public ushort Cluster { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            SessionKey = this.GetUIntBE();
            CharacterID = this.GetUIntBE();
            Cluster = this.GetUShortBE();

            ((UCGameSession)client).OnReserveAnotherGameFE(this);
        }
    }
}
