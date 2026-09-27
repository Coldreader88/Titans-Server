using Common.Characters;
using Common.Network.Packets;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x805F: the player info, as the Lobby sends it in 0x38002 (see <see cref="PlayerInfoWriter"/>). After a
    /// flight it names the shuttle as the vehicle, the position is unknown (the client places the arrival) and
    /// the transport fields say where it took off (Earth_To_Space.pcap, Space_to_Earth.pcap).
    /// </summary>
    public class SM_GC_PLAYER_INFO : UCPacket<GSOpcode>
    {
        public SM_GC_PLAYER_INFO(uint accountID, Character c, int vehicleTemplateID, Transport transport)
        {
            this.ID = GSOpcode.SM_GC_PLAYER_INFO;

            PlayerInfoWriter.Write(this, accountID, c, vehicleTemplateID, transport);
        }
    }
}
