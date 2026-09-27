using Common.Network.Packets;
using TitansUC.GameServer.World;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x8035: tells the players around that something was put on or taken off the ground.
    ///
    /// <code>
    /// uint32 BE   action: 1 item dropped, 2 item picked up, 3 vehicle left, 4 vehicle taken
    /// record     the ground item (see <see cref="GroundItem"/>; expires is 0 once taken)
    /// uint32 BE   character id of the player who did it
    /// uint16 BE   FFFF
    /// byte       the item's list (see <see cref="GroundItem.List"/>)
    /// </code>
    /// Layout from the official 0x8035s (BATTLE_1.pcap, Alumina (11).pcap and others).
    /// </summary>
    public class SM_UPDATE_ITEM_INFO : UCPacket<GSOpcode>
    {
        public const uint ItemDropped = 1;
        public const uint ItemPickedUp = 2;
        public const uint VehicleLeft = 3;
        public const uint VehicleTaken = 4;

        public SM_UPDATE_ITEM_INFO(uint action, GroundItem item, uint actorID)
        {
            this.ID = GSOpcode.SM_UPDATE_ITEM_INFO;

            this.PutUIntBE(action);
            item.WriteRecord(this, action == ItemPickedUp || action == VehicleTaken);
            this.PutUIntBE(actorID);
            this.PutUShortBE(0xFFFF);
            this.PutByte(item.List);
        }
    }
}
