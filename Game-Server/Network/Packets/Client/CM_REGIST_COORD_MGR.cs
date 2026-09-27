using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;
using TitansUC.GameServer.World;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x00: the client registers its position record (see <see cref="CoordData"/>) once it is in the world.
    /// Java reference: RequestRegistCoordMgr.java.
    /// </summary>
    public class CM_REGIST_COORD_MGR : UCPacket<GSOpcode>
    {
        public CM_REGIST_COORD_MGR()
        {
            this.ID = GSOpcode.CM_REGIST_COORD_MGR;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_REGIST_COORD_MGR();
        }

        public CoordData Coord { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            Read();

            ((UCGameSession)client).OnRegistCoordMgr(this);
        }

        public void Read()
        {
            Coord = this.Remaining >= CoordData.Size ? CoordData.Read(this) : null;
        }
    }
}
