using Common.Network.Packets;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x8040: the game server of the other side.
    ///
    /// <code>
    /// uint32 BE   0x00020000
    /// uint32 BE   character id
    /// uint16 BE   cluster (1 Earth, 2 Space)
    /// uint16 BE   unknown: 0x17BF on the way to Space, 0x17D0 on the way to Earth
    /// UC size    then the server's IP address in ASCII
    /// uint16 BE   port
    /// </code>
    /// Layout from Earth_To_Space.pcap and Space_to_Earth.pcap.
    /// </summary>
    public class SM_RESERVE_ANOTHER_GAME_FE : UCPacket<GSOpcode>
    {
        public SM_RESERVE_ANOTHER_GAME_FE(uint characterID, ushort cluster, string host, int port)
        {
            this.ID = GSOpcode.SM_RESERVE_ANOTHER_GAME_FE;

            this.PutUIntBE(0x00020000);
            this.PutUIntBE(characterID);
            this.PutUShortBE(cluster);
            this.PutUShortBE(cluster == 2 ? (ushort)0x17BF : (ushort)0x17D0);
            var ip = System.Text.Encoding.ASCII.GetBytes(host);
            this.PutSize(ip.Length);
            this.PutBytes(ip);
            this.PutUShortBE((ushort)port);
        }
    }
}
