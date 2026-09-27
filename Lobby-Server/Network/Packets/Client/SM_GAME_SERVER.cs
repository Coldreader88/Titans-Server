using System.Text;
using Common.Network.Packets;

namespace TitansUC.LobbyServer.Network.Packets.Client
{
    /// <summary>
    /// 0x38005: where the client connects for the game.
    ///
    /// <code>
    /// uint32 BE   1 = go ahead, 5 = refused
    /// UC size     address length, then the address as ASCII (for example "127.0.0.1")
    /// uint16 BE   port
    /// uint32 BE   0
    /// </code>
    /// Java reference: mina_loginserver NotifyGameServerIP.java. Matches the official reply in
    /// UCGOZone-Login.pcap ("202.228.206.65", port 24010).
    /// </summary>
    public class SM_GAME_SERVER : UCPacket<LSOpcode>
    {
        public const uint Allow = 0x1;
        public const uint Refuse = 0x5;

        public SM_GAME_SERVER(uint response, string address, int port)
        {
            this.ID = LSOpcode.SM_GAME_SERVER;

            var ascii = Encoding.ASCII.GetBytes(address ?? string.Empty);

            this.PutUIntBE(response);
            this.PutSize(ascii.Length);
            this.PutBytes(ascii);
            this.PutUShortBE((ushort)port);
            this.PutUIntBE(0);
        }
    }
}
