using System;
using System.Threading;
using Common.Characters;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// Hit, damage and destruction rules. The official server decided them and the captures only show the
    /// results, so the formulas are ours, built on the values the client's battle info lists as the server's
    /// inputs (weapon hit rate, damage and hit ratios by distance, shield guard probability, engine avoid rate,
    /// the weapon's operation and type skills) and fitted to the 135 attack results in the captures: about 18%
    /// misses, 8% of hits critical, a shield taking 20 to 30% of hits, and a gun doing about 0.5 to 2.8 times
    /// its power per attack whatever the rounds it fires (a melee weapon about 0.3 to 1.1 times).
    /// See <see cref="Attack"/>.
    /// </summary>
    public static class Combat
    {
        public const byte ResultHit = 0;
        public const byte ResultCritical = 1;
        public const byte ResultShield = 3;
        public const byte ResultMiss = 6;

        public const byte ExplosionNone = 0;
        public const byte ExplosionDestroyed = 1;
        public const byte ExplosionShieldBroken = 3;
        public const byte ExplosionMiss = 4;

        /// <summary>
        /// How long a wreck lies on the ground (the official ones expired 600 seconds after the kill).
        /// </summary>
        public const int WreckLifetime = 600;

        public const int ShieldSlot = 1;

        /// <summary>
        /// The chance in percent that a destroyed vehicle leaves a wreck; otherwise nothing is left of it
        /// (GameServer.xml WreckChance, 70 by default).
        /// </summary>
        public static int WreckChance = 70;

        public static bool LeavesWreck()
        {
            lock (random)
            {
                return random.Next(100) < WreckChance;
            }
        }

        /// <summary>
        /// The attack number: one counter for every attack on the server, misses included (the official Earth
        /// server's were 0x0113xxxx).
        /// </summary>
        private static int attackNumber = 0x01130000;

        private static readonly Random random = new Random();

        public static uint NextAttackNumber()
        {
            return (uint)Interlocked.Increment(ref attackNumber);
        }

        /// <summary>
        /// Rounds one attack with this weapon uses: the template's rounds per shot (the official 100mm machine
        /// gun fired 6, beam rifles 8); none for melee weapons.
        /// </summary>
        public static int RoundsPerAttack(ItemTemplate weapon)
        {
            if (weapon == null || weapon.Magazine <= 0)
            {
                return 0;
            }
            return Math.Max(1, weapon.AmmoPerShot);
        }

        /// <summary>
        /// The distance band (0 nearest to 6) a target at <paramref name="distance"/> is in: the weapon's range
        /// cut into seven equal bands (ours; the client has the seven ratios but not the bands).
        /// </summary>
        public static int Band(ItemTemplate weapon, int distance)
        {
            if (weapon == null || weapon.Range <= 0)
            {
                return 0;
            }
            return Math.Max(0, Math.Min(6, (int)((long)Math.Max(0, distance) * 7 / weapon.Range)));
        }

        private static double Ratio(float[] ratios, int band)
        {
            return ratios != null && band < ratios.Length && ratios[band] > 0 ? ratios[band] : 1.0;
        }

        /// <summary>
        /// The chance that an attack hits: the weapon's hit rate times its hit ratio at that distance, up to 10
        /// points more for the attacker's skill with the weapon (the average of its operation and type skill;
        /// 100.0 gives all 10), up to 10 points less for the defender's evasion skill, and less by a quarter of
        /// the target engine's avoid rate (a level 3 engine's 20 is 5 points); at least 5%, at most 95%.
        /// </summary>
        public static double HitChance(ItemTemplate weapon, int band, Character attacker, Character defender, ItemNode vehicle)
        {
            double chance = (weapon != null && weapon.HitRate > 0 ? weapon.HitRate : 7000) / 10000.0;
            chance *= Ratio(weapon != null ? weapon.HitRatios : null, band);
            chance += 0.10 * WeaponSkill(weapon, attacker) / 1000.0;
            if (defender != null)
            {
                chance -= 0.10 * Math.Min(1000, defender.GetSkill(Skill.EVASION)) / 1000.0;
            }
            var engine = vehicle != null ? ItemTemplates.Get(vehicle.EngineID > 0 ? vehicle.EngineID : ItemTemplates.EngineOf(vehicle.StaticID)) : null;
            if (engine != null)
            {
                chance -= engine.AvoidRate / 400.0;
            }
            return Math.Max(0.05, Math.Min(0.95, chance));
        }

        /// <summary>
        /// The chance that a hit lands on the defender's shield: its guard probability (20 or 30%), up to 5 points
        /// more for their defence skill.
        /// </summary>
        public static double GuardChance(ItemNode shield, Character defender)
        {
            var t = ItemTemplates.Get(shield.StaticID);
            double chance = (t != null && t.GuardProbability > 0 ? t.GuardProbability : 2000) / 10000.0;
            if (defender != null)
            {
                chance += 0.05 * Math.Min(1000, defender.GetSkill(Skill.DEFENCE)) / 1000.0;
            }
            return chance;
        }

        /// <summary>
        /// The attacker's skill with the weapon in tenths (0 to 1000): the average of the operation and type
        /// skills it names.
        /// </summary>
        private static int WeaponSkill(ItemTemplate weapon, Character attacker)
        {
            if (weapon == null || attacker == null)
            {
                return 0;
            }
            int total = 0, count = 0;
            foreach (var skill in new[] { weapon.OperationSkill, weapon.TypeSkill })
            {
                if (skill.HasValue)
                {
                    total += Math.Min(1000, attacker.GetSkill(skill.Value));
                    count++;
                }
            }
            return count == 0 ? 0 : total / count;
        }

        /// <summary>
        /// The damage of a hit before a critical: power times the damage ratio at that distance, times a random
        /// factor. Guns: 0.5 up to 0.5 + variation / 15 (a beam rifle's 30 gives up to 2.5, the beam bazooka's
        /// 15 up to 1.5, as in the captures). Melee weapons: 0.3 up to 0.3 + variation / 45 (the claw's 35 gives
        /// up to 1.08; the captures had 0.29 to 1.11).
        /// </summary>
        public static int Damage(ItemTemplate weapon, int band, double roll)
        {
            int power = weapon != null && weapon.Power > 0 ? weapon.Power : 50;
            int variation = weapon != null && weapon.Rate > 0 ? weapon.Rate : 20;
            bool melee = weapon == null || weapon.Magazine <= 0;
            double low = melee ? 0.3 : 0.5;
            double high = low + variation / (melee ? 45.0 : 15.0);
            double factor = low + roll * (high - low);
            return Math.Max(1, (int)(power * Ratio(weapon != null ? weapon.AttackRatios : null, band) * factor));
        }

        /// <summary>
        /// The share of hits that are critical (9 of the 111 hits in the captures), and what a critical multiplies
        /// the damage by (ours; the captured criticals were 0.5 to 3 times the power).
        /// </summary>
        public const double CriticalChance = 0.08;
        public const double CriticalFactor = 1.5;

        /// <summary>
        /// Damage %: how much of the maximum health is gone, rounded up (checked against the official values:
        /// 154 of 2900 lost is 6%, 2823 is 98%).
        /// </summary>
        public static byte DamagePercent(int health, int maxHealth)
        {
            if (maxHealth <= 0)
            {
                return 0;
            }
            long lost = Math.Max(0, maxHealth - Math.Max(0, health));
            return (byte)Math.Min(100, (lost * 100 + maxHealth - 1) / maxHealth);
        }

        /// <summary>
        /// Resolves one attack of <paramref name="weapon"/> on <paramref name="vehicle"/> and applies it (health,
        /// shield durability, the weapon's rounds and durability). <paramref name="attacker"/> and
        /// <paramref name="defender"/> are the pilots (null for an NPC or an empty vehicle). A shot beyond the
        /// weapon's range, from an empty gun or at a destroyed vehicle misses. The caller takes a destroyed
        /// vehicle away.
        /// </summary>
        public static HitResult Attack(ItemNode weapon, ItemNode vehicle, ItemNode shield, int distance,
            Character attacker = null, Character defender = null, SpecialAttack special = null)
        {
            var template = ItemTemplates.Get(weapon.StaticID);
            var r = new HitResult
            {
                AttackNumber = NextAttackNumber(),
                Weapon = weapon,
                SpecialAttackID = special != null ? special.ID : SpecialAttacks.None,
            };
            double specialHit = special != null ? special.HitRate / 10000.0 : 1.0;
            var vehicleTemplate = VehicleTemplates.Get(vehicle.StaticID);

            // GM toggles: #ammo keeps the attacker's rounds and durability, #god keeps the defender's health and shield.
            var attackerSession = attacker != null ? GameWorld.Instance.Get(attacker.ClientID) : null;
            var defenderSession = defender != null ? GameWorld.Instance.Get(defender.ClientID) : null;
            bool unlimitedAmmo = attackerSession != null && attackerSession.UnlimitedAmmo;
            bool invulnerable = defenderSession != null && defenderSession.GodMode;

            lock (vehicle)
            {
                int rounds = RoundsPerAttack(template);
                if (rounds > 0 && !unlimitedAmmo)
                {
                    rounds = Math.Min(rounds, Math.Max(0, weapon.Loaded));
                    weapon.Loaded -= rounds;
                }
                r.RoundsUsed = unlimitedAmmo ? 0 : rounds;

                int band = Band(template, distance);
                double hitRoll, damageRoll, criticalRoll, shieldRoll;
                lock (random)
                {
                    hitRoll = random.NextDouble();
                    damageRoll = random.NextDouble();
                    criticalRoll = random.NextDouble();
                    shieldRoll = random.NextDouble();
                }

                // Upgrades (ours; the captures show none in use): each power level adds 5% damage, each hit level 2
                // points of hit chance, each defence level takes 5% off the damage taken.
                var attackerVehicle = weapon.Parent != null ? weapon.Parent.Parent : null;
                int power = attackerVehicle != null ? Improvements.LevelOf(attackerVehicle.Improvement, Improvements.Power) : 0;
                int hit = attackerVehicle != null ? Improvements.LevelOf(attackerVehicle.Improvement, Improvements.Hit) : 0;
                int defence = Improvements.LevelOf(vehicle.Improvement, Improvements.Defence);

                bool outOfRange = template != null && template.Range > 0 && distance > template.Range;
                bool emptyGun = template != null && template.Magazine > 0 && rounds == 0;
                if (vehicle.Health <= 0 || outOfRange || emptyGun ||
                    hitRoll >= Math.Min(0.95, HitChance(template, band, attacker, defender, vehicle) * specialHit + 0.02 * hit))
                {
                    r.Result = ResultMiss;
                    r.Explosion = ExplosionMiss;
                    return r;
                }

                bool critical = criticalRoll < CriticalChance;
                bool shieldHit = shield != null && shieldRoll < GuardChance(shield, defender);
                int damage = Damage(template, band, damageRoll);
                damage = damage * (100 + 5 * power) * (100 - 5 * defence) / 10000;
                if (special != null)
                {
                    damage = (int)((long)damage * special.AttackRate / 10000);
                }
                if (critical)
                {
                    damage = (int)(damage * CriticalFactor);
                }
                r.Damage = invulnerable ? 0 : Math.Max(1, damage);
                r.Result = critical ? ResultCritical : ResultHit;
                r.DurabilityUsed = unlimitedAmmo ? 0 : special != null ? special.Durability : 1;
                UseDurability(weapon, r.DurabilityUsed);
                r.M = vehicleTemplate != null ? vehicleTemplate.CombatValue : 1000;

                if (shieldHit)
                {
                    r.Result = ResultShield;
                    r.DamagedItem = shield;
                    UseDurability(shield, r.Damage);
                    if (shield.Stats[0] <= 0)
                    {
                        r.Explosion = ExplosionShieldBroken;
                        r.ShieldBroken = true;
                    }
                    return r;
                }

                r.VehicleDamaged = true;
                r.DamagedItem = vehicle;
                int health = Math.Max(0, vehicle.Health - r.Damage);
                PlayerInventory.SetHealth(vehicle, health);
                r.Percent = DamagePercent(health, vehicle.MaxHealth);
                if (health == 0)
                {
                    r.Percent = 100;
                    r.Explosion = ExplosionDestroyed;
                    r.Destroyed = true;
                }
                return r;
            }
        }

        private static void UseDurability(ItemNode item, int amount)
        {
            if (item.Stats != null && item.Stats.Length > 0)
            {
                item.Stats[0] = Math.Max(0, item.Stats[0] - amount);
            }
        }
    }

    public class HitResult
    {
        public uint AttackNumber { get; set; }
        public ItemNode Weapon { get; set; }
        public int Damage { get; set; }
        public byte Result { get; set; }
        public byte Explosion { get; set; }

        /// <summary>True when the vehicle itself lost health (not a miss or a shield hit).</summary>
        public bool VehicleDamaged { get; set; }

        /// <summary>The vehicle or shield that took the hit; null on a miss.</summary>
        public ItemNode DamagedItem { get; set; }

        /// <summary>The vehicle's damage % after the hit (0 for misses and shield hits).</summary>
        public byte Percent { get; set; }

        public bool Destroyed { get; set; }
        public bool ShieldBroken { get; set; }
        public int M { get; set; }
        public int DurabilityUsed { get; set; }

        /// <summary>
        /// The melee special attack used (SPECIALATTACKTEMPLATE id), <see cref="SpecialAttacks.None"/> for a normal one;
        /// echoed in 0x800F, 0x8036 and 0x8011.
        /// </summary>
        public ushort SpecialAttackID { get; set; } = SpecialAttacks.None;
        public int RoundsUsed { get; set; }

        /// <summary>
        /// The target is of the attacker's faction: "relation to target" 0 in 0x800F and 0x8067 (1 = enemy), which
        /// the client uses to count the kill as a friendly one.
        /// </summary>
        public bool Friendly { get; set; }

        /// <summary>
        /// The attack was a crime (see <see cref="Criminal"/>): the crime bit of 0x800F, counted in 0x8067 and
        /// 0x8068. The attacker's client adds the crime points itself.
        /// </summary>
        public bool Crime { get; set; }
    }
}
