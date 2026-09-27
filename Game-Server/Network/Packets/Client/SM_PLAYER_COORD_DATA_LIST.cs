using Common.Network.Packets;
using System.Collections.Generic;
using TitansUC.GameServer.World;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x8003: everyone the player can see.
    ///
    /// <code>
    /// uint16 BE   2
    /// UC size    number of records, then the <see cref="CoordData"/> records (the player first)
    /// uint32 BE   number of records
    /// uint32 BE   account id
    /// uint32 BE   character id
    /// uint16 BE   zone
    /// uint32 BE   0
    /// uint16 BE   0
    /// </code>
    /// Java reference: NotifyPlayerCoordDataList.java and GameWorld.write. The trailer follows UCGOZone-Login.pcap, where
    /// the count is a uint32 (Java wrote one byte and a byte zone).
    /// </summary>
    public class SM_PLAYER_COORD_DATA_LIST : UCPacket<GSOpcode>
    {
        public SM_PLAYER_COORD_DATA_LIST(uint accountID, CoordData self, List<CoordData> others)
        {
            this.ID = GSOpcode.SM_PLAYER_COORD_DATA_LIST;

            int count = 1 + others.Count;

            this.PutUShortBE(0x0002);
            this.PutSize(count);
            self.Write(this);
            foreach (var other in others)
            {
                other.Write(this);
            }
            this.PutIntBE(count);
            this.PutUIntBE(accountID);
            this.PutUIntBE(self.CharacterID);
            this.PutUShortBE(self.ClusterID);
            this.PutIntBE(0);
            this.PutUShortBE(0);
        }
    }
}
