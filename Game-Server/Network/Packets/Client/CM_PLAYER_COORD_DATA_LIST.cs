using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x03: the client polls for everyone it can see, about once a second.
    ///
    /// <code>
    /// uint32 BE   account id
    /// uint32 BE   character id
    /// uint16 BE   zone, 6 x 00
    /// int32 BE    x, y, z
    /// float BE    view radius (8000.0 on foot in UCGOZone-Login.pcap)
    /// </code>
    /// Java reference: RequestPlayerCoordDataList.java.
    /// </summary>
    public class CM_PLAYER_COORD_DATA_LIST : UCPacket<GSOpcode>
    {
        public CM_PLAYER_COORD_DATA_LIST()
        {
            this.ID = GSOpcode.CM_PLAYER_COORD_DATA_LIST;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_PLAYER_COORD_DATA_LIST();
        }

        public uint AccountID { get; private set; }

        public uint CharacterID { get; private set; }

        public int X { get; private set; }

        public int Y { get; private set; }

        public int Z { get; private set; }

        public float Radius { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            Read();

            ((UCGameSession)client).OnPlayerCoordDataList(this);
        }

        public void Read()
        {
            AccountID = this.GetUIntBE();
            CharacterID = this.GetUIntBE();
            this.GetUShortBE();
            this.GetBytes(6);
            X = this.GetIntBE();
            Y = this.GetIntBE();
            Z = this.GetIntBE();
            Radius = this.Remaining >= 4 ? ToFloat(this.GetUIntBE()) : 0;
        }

        private static float ToFloat(uint bits)
        {
            return System.BitConverter.ToSingle(System.BitConverter.GetBytes(bits), 0);
        }
    }
}
