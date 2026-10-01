using Common.Network.Packets;
using TitansUC.GameServer.World;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x8011: result of an attack on a ground item, to the attacker: uint32 BE 2, attacker, damage; byte 0, byte
    /// crime (1 when the vehicle is another player's of the same faction: the client adds a crime point); uint16 BE
    /// the melee special attack used (FFFF none); uint32 BE attack number; byte durability used, byte rounds used; weapon
    /// (unique id, format, template); uint32 BE target unique id, 0x14. 42 bytes (TEST_Z_GUNDAM.pcap).
    /// Everyone near gets the item's new health in 0x8035 action 5.
    /// </summary>
    public class SM_ATTACK_ITEM : UCPacket<GSOpcode>
    {
        public SM_ATTACK_ITEM(uint attacker, HitResult r, GroundItem target)
        {
            this.ID = GSOpcode.SM_ATTACK_ITEM;

            this.PutUIntBE(2);
            this.PutUIntBE(attacker);
            this.PutIntBE(r.Damage);
            this.PutByte(0);
            this.PutByte(r.Crime ? (byte)1 : (byte)0);
            this.PutUShortBE(r.SpecialAttackID);
            this.PutUIntBE(r.AttackNumber);
            this.PutByte((byte)r.DurabilityUsed);
            this.PutByte((byte)r.RoundsUsed);
            r.Weapon.WriteReference(this);
            this.PutUIntBE(target.UniqueID);
            this.PutIntBE(target.Node.Format);
        }
    }
}
