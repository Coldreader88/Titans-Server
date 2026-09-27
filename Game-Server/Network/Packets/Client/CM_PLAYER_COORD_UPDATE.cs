using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;
using TitansUC.GameServer.World;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x02: the player moved or changed state; the body is a <see cref="CoordData"/> record. No reply.
    /// Java reference: RequestPlayerCoordUpdate.java / CoordUpdate.java.
    /// </summary>
    public class CM_PLAYER_COORD_UPDATE : UCPacket<GSOpcode>
    {
        public CM_PLAYER_COORD_UPDATE()
        {
            this.ID = GSOpcode.CM_PLAYER_COORD_UPDATE;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_PLAYER_COORD_UPDATE();
        }

        public CoordData Coord { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            Read();

            ((UCGameSession)client).OnPlayerCoordUpdate(this);
        }

        public void Read()
        {
            Coord = this.Remaining >= CoordData.Size ? CoordData.Read(this) : null;
        }
    }
}
