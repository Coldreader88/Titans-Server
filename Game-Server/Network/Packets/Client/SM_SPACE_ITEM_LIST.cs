using System.Collections.Generic;
using Common.Network.Packets;
using TitansUC.GameServer.World;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x8027: what is inside a vehicle or wreck on the ground, for the window 0x27 opens.
    ///
    /// <code>
    /// uint16 BE   1, uint16 BE 2
    /// uint32 BE   vehicle unique id, format, template
    /// UC size    number of items, then per item the 62-byte ground item record (see
    ///            <see cref="GroundItem.WriteRecord"/>) with no position, the looker as owner and
    ///            the durability for equipment
    /// </code>
    /// Layout from the official reply in BATTLE_1.pcap: an ACGUY's ER kits and claws, its cargo and armaments
    /// in one list.
    /// </summary>
    public class SM_SPACE_ITEM_LIST : UCPacket<GSOpcode>
    {
        public SM_SPACE_ITEM_LIST(ItemNode vehicle, IList<ItemNode> items, uint lookerID)
        {
            this.ID = GSOpcode.SM_SPACE_ITEM_LIST;

            this.PutUShortBE(1);
            this.PutUShortBE(2);
            vehicle.WriteReference(this);
            this.PutSize(items.Count);
            ushort counter = 0;
            foreach (var item in items)
            {
                item.WriteReference(this);
                this.PutBytes(new byte[22]);
                this.PutIntBE(item.Amount > 0 ? item.Amount : 1);
                this.PutUIntBE(lookerID);
                this.PutSize(0);
                this.PutIntBE(item.Created);
                this.PutIntBE(item.IsEquipment ? item.Stats[0] : 0);
                this.PutIntBE(item.IsEquipment ? item.Stats[1] : 0);
                this.PutIntBE(0);
                this.PutUShortBE(++counter);
                this.PutSize(0);
            }
        }
    }
}
