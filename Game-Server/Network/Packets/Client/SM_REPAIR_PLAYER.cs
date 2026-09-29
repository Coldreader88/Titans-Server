using System;
using Common.Network.Packets;
using TitansUC.GameServer.World;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x8069 (to the two players) and 0x806A (to everyone near): the result of a repair with an MR tool kit.
    /// Field names from the client (UC_RepairPlayerResult, UC_RepairPlayerResultForAround); layout from
    /// Oggo_Repair_Other_(I_repair_MS06D).pcap and transfer_lunatitaniumfrombankto781overweight.pcap.
    ///
    /// <code>
    /// 0x8069: src id, target id, float BE repair % (of max health), int32 BE amount repaired, byte result (0),
    ///         int16 BE special attack id, uint32 BE attack number, byte durability used, byte ammunition used,
    ///         kit (uid, format, static id), repaired vehicle (uid, format, static id), byte damage % left
    /// 0x806A: src id, target id, byte result, int16 BE special attack id, uint32 BE attack number,
    ///         uint32 BE kit static id, repaired vehicle (uid, format), byte damage % left
    /// </code>
    /// Official: 1088 repaired = 45.33% and 1050 = 29.17%, both leaving 0% damage.
    /// </summary>
    public class SM_REPAIR_PLAYER : UCPacket<GSOpcode>
    {
        public SM_REPAIR_PLAYER(bool around, uint src, uint target, int amount, int maxHealth, short specialAttack,
            uint attackNumber, ItemNode kit, ItemNode vehicle, byte damagePercent)
        {
            this.ID = around ? GSOpcode.SM_REPAIR_PLAYER_NEAR : GSOpcode.SM_REPAIR_PLAYER;

            this.PutUIntBE(src);
            this.PutUIntBE(target);
            if (!around)
            {
                float percent = maxHealth > 0 ? amount * 100f / maxHealth : 0f;
                this.PutUIntBE(BitConverter.ToUInt32(BitConverter.GetBytes(percent), 0));
                this.PutIntBE(amount);
            }
            this.PutByte(0);
            this.PutShortBE(specialAttack);
            this.PutUIntBE(attackNumber);
            if (!around)
            {
                this.PutByte(1);
                this.PutByte(0);
                this.PutUIntBE(kit.UniqueID);
                this.PutIntBE(kit.Format);
                this.PutIntBE(kit.StaticID);
                this.PutUIntBE(vehicle.UniqueID);
                this.PutIntBE(vehicle.Format);
                this.PutIntBE(vehicle.StaticID);
            }
            else
            {
                this.PutIntBE(kit.StaticID);
                this.PutUIntBE(vehicle.UniqueID);
                this.PutIntBE(vehicle.Format);
            }
            this.PutByte(damagePercent);
        }
    }
}
