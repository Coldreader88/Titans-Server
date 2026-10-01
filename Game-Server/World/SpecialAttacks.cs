using System;
using Common.Characters;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// Melee special attacks (the client's SPECIALATTACKTEMPLATE.DAT, 4 records; specs/re-combat-chat-targets.md
    /// section 1). They are a combo: the 2nd swing in a row uses the weapon's first special (skill 30.0), the 3rd its
    /// second (70.0). The client sends the template id (0-3, FFFF none) in 0x0F and 0x11 and only offers one the
    /// weapon has (WEAPONTEMPLATE special_attack_ids), the pilot's skill allows and the weapon's durability covers;
    /// the server checks the same and otherwise treats the swing as a normal attack. Each special multiplies the
    /// damage and the hit chance, and wears the weapon by its durability cost instead of 1.
    /// </summary>
    public static class SpecialAttacks
    {
        public const ushort None = 0xFFFF;

        // id, damage x10000, hit chance x10000, durability used (and needed), skill, skill level x10. The extra
        // swing time (0 / 500 / 1000 ms) is the client's; no special uses extra rounds.
        private static readonly SpecialAttack[] table =
        {
            new SpecialAttack(0, 15000, 8500, 2, Skill.CQB, 300),
            new SpecialAttack(1, 20000, 7000, 5, Skill.CQB, 700),
            new SpecialAttack(2, 15000, 8500, 2, Skill.HANDTOHAND_COMBAT, 300),
            new SpecialAttack(3, 20000, 7000, 5, Skill.HANDTOHAND_COMBAT, 700),
        };

        /// <summary>
        /// The special the client asked for, when this weapon has it, the attacker's skill allows it and the weapon
        /// has the durability; null for a normal attack.
        /// </summary>
        public static SpecialAttack Resolve(ushort id, ItemNode weapon, Character attacker)
        {
            if (id == None || id >= table.Length || weapon == null)
            {
                return null;
            }
            var template = ItemTemplates.Get(weapon.StaticID);
            if (template == null || template.SpecialAttackIDs == null || Array.IndexOf(template.SpecialAttackIDs, (short)id) < 0)
            {
                return null;
            }
            var special = table[id];
            if (attacker != null && attacker.GetSkill(special.Skill) < special.SkillLevel)
            {
                return null;
            }
            if (weapon.Stats != null && weapon.Stats.Length > 0 && weapon.Stats[0] < special.Durability)
            {
                return null;
            }
            return special;
        }
    }

    public class SpecialAttack
    {
        public SpecialAttack(ushort id, int attackRate, int hitRate, int durability, Skill skill, int skillLevel)
        {
            ID = id;
            AttackRate = attackRate;
            HitRate = hitRate;
            Durability = durability;
            Skill = skill;
            SkillLevel = skillLevel;
        }

        public ushort ID { get; private set; }

        /// <summary>Damage multiplier x10000 (x1.5, x2.0).</summary>
        public int AttackRate { get; private set; }

        /// <summary>Hit chance multiplier x10000 (0.85, 0.70; our reading: the client only carries the value).</summary>
        public int HitRate { get; private set; }

        /// <summary>Weapon durability used, and the least the weapon must have (2, 5).</summary>
        public int Durability { get; private set; }

        public Skill Skill { get; private set; }
        public int SkillLevel { get; private set; }
    }
}
