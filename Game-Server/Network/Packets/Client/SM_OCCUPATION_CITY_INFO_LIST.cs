using System.Collections.Generic;
using TitansUC.GameServer.World;
using Common.Network.Packets;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x8070 NotifyOccupationCityInfoList: the battle towns Richmond (58) and Newman (59).
    ///
    /// <code>
    /// UC size n, then per city:
    /// uint32 BE city id, uint16 BE owner (1 Federation, 2 Zeon), uint32 BE status (0 peace, 1 open to attack, 2 war),
    /// uint32 BE Unix time (peace: when the window opens; war: the deadline),
    /// UC size 5 + uint16 BE owner of each ICF, uint32 BE serial (the client keeps a record only if it is newer)
    /// </code>
    /// Layout from UCGOZone-Login.pcap and the client (0x791818). The Java server called the ICF owners
    /// "trophies" and sent a constant 0x7B0 for the serial.
    /// </summary>
    public class SM_OCCUPATION_CITY_INFO_LIST : UCPacket<GSOpcode>
    {
        public SM_OCCUPATION_CITY_INFO_LIST(IList<OccupationCity> cities)
        {
            this.ID = GSOpcode.SM_OCCUPATION_CITY_INFO_LIST;

            this.PutSize(cities.Count);
            foreach (var c in cities)
            {
                this.PutIntBE(c.ID);
                this.PutUShortBE(c.Owner);
                this.PutIntBE(c.Status);
                this.PutIntBE(c.Time);
                this.PutSize(c.Icf.Length);
                foreach (var f in c.Icf)
                {
                    this.PutUShortBE(f);
                }
                this.PutUIntBE(Occupation.NextSerial());
            }
        }
    }

    /// <summary>
    /// 0x8076 NotifyOccupationEvent (23 bytes): uint32 BE type (4 window open, 0 war started, 2 ICF captured,
    /// 1 war over), player (FFFFFFFF = none), city; byte ICF (FF = none); uint16 BE faction (3 = none; for type 1
    /// the owner from now on); uint32 BE Unix time (type 0: the deadline; type 1: the next window); uint32 BE serial.
    /// Layout from the captures (TEST_2.pcap, xenolog) and the client (0x791c4c).
    /// </summary>
    public class SM_OCCUPATION_EVENT : UCPacket<GSOpcode>
    {
        public SM_OCCUPATION_EVENT(uint type, uint playerID, int cityID, byte flag, ushort faction, int time, uint serial)
        {
            this.ID = GSOpcode.SM_OCCUPATION_EVENT;

            this.PutUIntBE(type);
            this.PutUIntBE(playerID);
            this.PutIntBE(cityID);
            this.PutByte(flag);
            this.PutUShortBE(faction);
            this.PutIntBE(time);
            this.PutUIntBE(serial);
        }
    }
}
