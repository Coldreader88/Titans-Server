using Common.Network.Packets;
using TitansUC.GameServer.World;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x803C: moves the player to a position: uint32 BE character id, int32 BE x, y, z, int32 BE 0,
    /// int16 BE direction. Java reference: NotifyCompulsionMove.java.
    /// </summary>
    public class SM_COMPULSION_MOVE : UCPacket<GSOpcode>
    {
        public SM_COMPULSION_MOVE(CoordData coord)
        {
            this.ID = GSOpcode.SM_COMPULSION_MOVE;

            this.PutUIntBE(coord.CharacterID);
            this.PutIntBE(coord.X);
            this.PutIntBE(coord.Y);
            this.PutIntBE(coord.Z);
            this.PutIntBE(0);
            this.PutShortBE(coord.Direction);
        }
    }
}
