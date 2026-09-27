using System.Collections.Generic;
using Common.Network.Packets;
using TitansUC.GameServer.World;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x8005: the items of one list lying around the player: uint16 BE 2, byte list, UC size count, then one
    /// ground item record each (see <see cref="GroundItem"/>). Layout from UCGOZone-Login.pcap (empty lists)
    /// and Alaron_Zsaeo_VIMP_Flag2.pcap (a vehicle).
    /// Java reference: NotifySpaceCircuitItem.java / SpaceCircuitItem.java.
    /// </summary>
    public class SM_SPACE_CIRCUIT_ITEM : UCPacket<GSOpcode>
    {
        public SM_SPACE_CIRCUIT_ITEM(byte list, IList<GroundItem> items)
        {
            this.ID = GSOpcode.SM_SPACE_CIRCUIT_ITEM;

            this.PutUShortBE(0x0002);
            this.PutByte(list);
            this.PutSize(items.Count);
            foreach (var item in items)
            {
                item.WriteRecord(this, false);
            }
        }
    }
}
