using Common.Network.Packets;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x8005: the items lying around the player: uint16 BE 2, byte list, UC size count, items.
    /// Ground items are not implemented yet, so the list is always empty (00 02 list 80, as the official server
    /// answered for lists 0 and 2 in UCGOZone-Login.pcap).
    /// Java reference: NotifySpaceCircuitItem.java / SpaceCircuitItem.java.
    /// </summary>
    public class SM_SPACE_CIRCUIT_ITEM : UCPacket<GSOpcode>
    {
        public SM_SPACE_CIRCUIT_ITEM(byte list)
        {
            this.ID = GSOpcode.SM_SPACE_CIRCUIT_ITEM;

            this.PutUShortBE(0x0002);
            this.PutByte(list);
            this.PutSize(0);
        }
    }
}
