using System.Collections.Generic;
using Common.Network.Packets;
using TitansUC.GameServer.World;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x8067: the full result of a multi-target attack, to the attacker and each target.
    ///
    /// <code>
    /// uint32 BE   attacker
    /// byte 0, uint16 BE FFFF
    /// uint32 BE   attack number, 0, "m" (first target)
    /// byte       durability used, byte rounds used
    /// uint32 BE   0
    /// int32 BE    x, y, z of the impact
    /// uint32 BE   weapon unique id, format, template
    /// UC size    targets, 25 bytes each: uint32 BE target, uint32 BE damage, byte 1, byte result,
    ///            byte explosion, byte 00/FF (vehicle damaged or not), damaged item (unique id, format,
    ///            template), byte damage %
    /// </code>
    /// Checked against the official 0x8067s (the Java server put the damaged item once after all targets).
    /// </summary>
    public class SM_MULTI_ATTACK_RESULT : UCPacket<GSOpcode>
    {
        public SM_MULTI_ATTACK_RESULT(uint attacker, ItemNode weapon, uint attackNumber, int x, int y, int z,
            IList<KeyValuePair<uint, HitResult>> results)
        {
            this.ID = GSOpcode.SM_MULTI_ATTACK_RESULT;

            var first = results.Count > 0 ? results[0].Value : null;
            this.PutUIntBE(attacker);
            this.PutByte(0);
            this.PutUShortBE(0xFFFF);
            this.PutUIntBE(attackNumber);
            this.PutIntBE(0);
            this.PutIntBE(first != null && first.DamagedItem != null ? first.M : 0);
            this.PutByte(first != null ? (byte)first.DurabilityUsed : (byte)0);
            this.PutByte(first != null ? (byte)first.RoundsUsed : (byte)0);
            this.PutIntBE(0);
            this.PutIntBE(x);
            this.PutIntBE(y);
            this.PutIntBE(z);
            weapon.WriteReference(this);
            this.PutSize(results.Count);
            foreach (var entry in results)
            {
                var r = entry.Value;
                this.PutUIntBE(entry.Key);
                this.PutIntBE(r.Damage);
                this.PutByte(1);
                this.PutByte(r.Result);
                this.PutByte(r.Explosion);
                this.PutByte(r.VehicleDamaged ? (byte)0 : (byte)0xFF);
                SM_ATTACK_RESULT.WriteItem(this, r.DamagedItem);
                this.PutByte(r.Percent);
            }
        }
    }

    /// <summary>
    /// 0x8068: the short result of a multi-target attack, to everyone near: uint32 BE attacker, weapon template;
    /// uint16 BE FFFF; uint32 BE attack number, 0; int32 BE x, y, z; UC size targets, 7 bytes each
    /// (uint32 BE target, byte result, byte explosion, byte damage %).
    /// </summary>
    public class SM_MULTI_ATTACK_RESULT_NEAR : UCPacket<GSOpcode>
    {
        public SM_MULTI_ATTACK_RESULT_NEAR(uint attacker, ItemNode weapon, uint attackNumber, int x, int y, int z,
            IList<KeyValuePair<uint, HitResult>> results)
        {
            this.ID = GSOpcode.SM_MULTI_ATTACK_RESULT_NEAR;

            this.PutUIntBE(attacker);
            this.PutIntBE(weapon.StaticID);
            this.PutUShortBE(0xFFFF);
            this.PutUIntBE(attackNumber);
            this.PutIntBE(0);
            this.PutIntBE(x);
            this.PutIntBE(y);
            this.PutIntBE(z);
            this.PutSize(results.Count);
            foreach (var entry in results)
            {
                this.PutUIntBE(entry.Key);
                this.PutByte(entry.Value.Result);
                this.PutByte(entry.Value.Explosion);
                this.PutByte(entry.Value.Percent);
            }
        }
    }
}
