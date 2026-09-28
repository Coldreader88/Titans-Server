using Common.Network.Packets;
using TitansUC.GameServer.World;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x800F: the full result of an attack, to the attacker and the target.
    ///
    /// <code>
    /// uint32 BE   attacker, target
    /// uint32 BE   damage (health points)
    /// byte       1
    /// byte       result: 0 hit, 1 critical, 3 shield, 6 miss
    /// 2 bytes    0
    /// uint16 BE   FFFF
    /// byte       explosion: 0 none, 1 destroyed (a wreck stays), 3 shield broken, 4 miss
    /// byte       00 when the vehicle was damaged, FF otherwise
    /// uint32 BE   attack number
    /// uint32 BE   0
    /// uint32 BE   "m": the target template's combat value (unknown meaning; 0 on a miss)
    /// byte       weapon durability used, byte rounds used
    /// uint32 BE   weapon unique id, format, template
    /// uint32 BE   damaged item unique id, format, template (0, 0, -1 on a miss)
    /// byte       damaged vehicle's damage %
    /// </code>
    /// 59 bytes, checked against the official 0x800Fs (BATTLE_1.pcap, 100mm_MG_cartridge_empty.pcap).
    /// </summary>
    public class SM_ATTACK_RESULT : UCPacket<GSOpcode>
    {
        public SM_ATTACK_RESULT(uint attacker, uint target, HitResult r)
        {
            this.ID = GSOpcode.SM_ATTACK_RESULT;

            this.PutUIntBE(attacker);
            this.PutUIntBE(target);
            this.PutIntBE(r.Damage);
            this.PutByte(1);
            this.PutByte(r.Result);
            this.PutByte(0);
            this.PutByte(0);
            this.PutUShortBE(0xFFFF);
            this.PutByte(r.Explosion);
            this.PutByte(r.VehicleDamaged ? (byte)0 : (byte)0xFF);
            this.PutUIntBE(r.AttackNumber);
            this.PutIntBE(0);
            this.PutIntBE(r.DamagedItem != null ? r.M : 0);
            this.PutByte((byte)r.DurabilityUsed);
            this.PutByte((byte)r.RoundsUsed);
            WriteItem(this, r.Weapon);
            WriteItem(this, r.DamagedItem);
            this.PutByte(r.Percent);
        }

        /// <summary>
        /// Unique id, format and template of an item; 0, 0, -1 for none.
        /// </summary>
        public static void WriteItem(UCPacket<GSOpcode> p, ItemNode item)
        {
            if (item == null)
            {
                p.PutIntBE(0);
                p.PutIntBE(0);
                p.PutIntBE(-1);
                return;
            }
            item.WriteReference(p);
        }
    }
}
