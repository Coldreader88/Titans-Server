using System;
using System.Threading;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// Hit, damage and destruction rules. The official server decided them; the captures show the results
    /// (see the attack result packets) but not the formulas, so the numbers here are this server's own:
    /// 15% misses (and every shot beyond the weapon's range), 5% critical hits for double damage, 20% of hits on
    /// a vehicle with a shield in slot 1 hit the shield, and a hit does the weapon's power times the rounds it
    /// fires (at least 1), times 0.6 to 1.0.
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
        /// Rounds one attack with this weapon fires: none for melee weapons (no magazine); for the others a
        /// share of the magazine (the official 100mm machine gun, 150 rounds, fired 6 per burst).
        /// </summary>
        public static int RoundsPerAttack(ItemTemplate weapon)
        {
            if (weapon == null || weapon.Magazine <= 0)
            {
                return 0;
            }
            return Math.Max(1, Math.Min(15, weapon.Magazine / 25));
        }

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
        /// shield durability, the weapon's rounds and durability). The caller takes a destroyed vehicle away.
        /// </summary>
        public static HitResult Attack(ItemNode weapon, ItemNode vehicle, ItemNode shield, int distance)
        {
            var template = ItemTemplates.Get(weapon.StaticID);
            var r = new HitResult
            {
                AttackNumber = NextAttackNumber(),
                Weapon = weapon,
            };
            var vehicleTemplate = VehicleTemplates.Get(vehicle.StaticID);

            lock (vehicle)
            {
                int rounds = RoundsPerAttack(template);
                if (rounds > 0)
                {
                    rounds = Math.Min(rounds, Math.Max(0, weapon.Loaded));
                    weapon.Loaded -= rounds;
                }
                r.RoundsUsed = rounds;

                double roll;
                lock (random)
                {
                    roll = random.NextDouble();
                }

                bool outOfRange = template != null && template.Range > 0 && distance > template.Range;
                bool emptyGun = template != null && template.Magazine > 0 && rounds == 0;
                if (vehicle.Health <= 0 || outOfRange || emptyGun || roll < 0.15)
                {
                    r.Result = ResultMiss;
                    r.Explosion = ExplosionMiss;
                    return r;
                }

                double factor;
                bool critical, shieldHit;
                lock (random)
                {
                    factor = 0.6 + random.NextDouble() * 0.4;
                    critical = random.NextDouble() < 0.05;
                    shieldHit = shield != null && random.NextDouble() < 0.2;
                }
                int power = template != null && template.Power > 0 ? template.Power : 50;
                int damage = (int)(power * Math.Max(1, rounds) * factor * (critical ? 2 : 1));
                r.Damage = Math.Max(1, damage);
                r.Result = critical ? ResultCritical : ResultHit;
                r.DurabilityUsed = 1;
                UseDurability(weapon, 1);
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
        public int RoundsUsed { get; set; }
    }
}
