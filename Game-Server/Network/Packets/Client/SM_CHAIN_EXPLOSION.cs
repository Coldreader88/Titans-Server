using Common.Network.Packets;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x8012: the damage a chain explosion did (UC_ChainExplosionResult).
    ///
    /// <code>
    /// uint16 BE   code (2 = done)
    /// uint32 BE   character id
    /// byte        damaged part (0 = the vehicle)
    /// int32 BE    damage
    /// uint32 BE   damaged vehicle unique id, format
    /// </code>
    /// Official reply (ismay_symp_damage_megellan.pcap, a Magellan exploding): code 2, part 0, damage 800.
    /// Java wrote uint32 3 and uint16 0x20 where the part and damage are.
    /// </summary>
    public class SM_CHAIN_EXPLOSION : UCPacket<GSOpcode>
    {
        public SM_CHAIN_EXPLOSION(uint characterID, int damage, uint vehicleUniqueID, int vehicleFormat)
        {
            this.ID = GSOpcode.SM_CHAIN_EXPLOSION;

            this.PutUShortBE(2);
            this.PutUIntBE(characterID);
            this.PutByte(0);
            this.PutIntBE(damage);
            this.PutUIntBE(vehicleUniqueID);
            this.PutIntBE(vehicleFormat);
        }
    }
}
