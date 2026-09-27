using Common.Network.Packets;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x8070: who holds the occupation cities Richmond (0x3A) and Newman (0x3B).
    ///
    /// <code>
    /// 0x82, then per city:
    /// uint32 BE city id, uint16 BE occupying faction, uint32 BE status (0 = peace), uint32 BE Unix time,
    /// UC size + uint16 BE faction per trophy, uint32 BE counter
    /// </code>
    /// Java reference: NotifyOccupationCityInfoList.java / OccupationCity.java. Follows UCGOZone-Login.pcap: both cities
    /// held by the Federation, five Federation trophies each.
    /// </summary>
    public class SM_OCCUPATION_CITY_INFO_LIST : UCPacket<GSOpcode>
    {
        public SM_OCCUPATION_CITY_INFO_LIST(int unixTime)
        {
            this.ID = GSOpcode.SM_OCCUPATION_CITY_INFO_LIST;

            this.PutSize(2);
            foreach (var city in new[] { 0x3A, 0x3B })
            {
                this.PutIntBE(city);
                this.PutUShortBE(1);
                this.PutIntBE(0);
                this.PutIntBE(unixTime);
                this.PutSize(5);
                for (int i = 0; i < 5; i++)
                {
                    this.PutUShortBE(1);
                }
                this.PutIntBE(0x7B0);
            }
        }
    }
}
