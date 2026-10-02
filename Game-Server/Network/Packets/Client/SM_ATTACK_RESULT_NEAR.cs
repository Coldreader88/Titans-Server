using Common.Network.Packets;
using TitansUC.GameServer.World;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x8036: the short result of an attack, to everyone near the target (attacker and target included):
    /// uint32 BE attacker, target, weapon template; byte result, 0; uint16 BE FFFF; byte explosion; uint32 BE
    /// attack number; byte damage %. 22 bytes, checked against the official 0x8036s.
    /// </summary>
    public class SM_ATTACK_RESULT_NEAR : UCPacket<GSOpcode>
    {
        public SM_ATTACK_RESULT_NEAR(uint attacker, uint target, HitResult r)
        {
            this.ID = GSOpcode.SM_ATTACK_RESULT_NEAR;

            this.PutUIntBE(attacker);
            this.PutUIntBE(target);
            this.PutIntBE(r.Weapon.StaticID);
            this.PutByte(r.Result);
            this.PutByte(0);
            this.PutUShortBE(r.SpecialAttackID);
            this.PutByte(r.Explosion);
            this.PutUIntBE(r.AttackNumber);
            this.PutByte(r.Percent);
        }
    }
}
