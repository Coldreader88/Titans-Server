using Common.Characters;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// The criminal count (score slot 4). From the client:
    /// <list type="bullet">
    /// <item>A result packet whose crime bit or count is set makes the attacker's client add CRIMEINFO[type] x n
    /// points (1 for an attack, 10 for a swap or a vehicle taken); when the count was 0 it adds one more and
    /// counts a previous offense. The server adds the same (<see cref="Add"/>) and sends no 0x8008 for it.</item>
    /// <item>While the count is above 0 the client asks every 30 s (count 1) or 180 s to lower it (0x08), draws the
    /// player in the criminal colours (others see bit 0 of the position record's player state) and refuses some
    /// actions itself. Account levels 3, 4 and 5 never count.</item>
    /// <item>0x803D NotifyExilePlayer makes the client show error 85 and leave.</item>
    /// </list>
    /// Our own rules (the official ones are not in the client): attacking a player of your own faction who is not a
    /// criminal is a crime, as is attacking their empty vehicle on the ground; attacking criminals and NPCs is not.
    /// A player whose count reaches GameServer.xml CrimeExileCount is exiled: sent away to the exile point of their
    /// side (CrimeExileEarth / CrimeExileSpace, where they stand when unset) and disconnected; the count stays.
    /// </summary>
    public static class Criminal
    {
        public const int TypeAttack = 0;
        public const int TypeSwap = 1;
        public const int TypeMachine = 2;

        /// <summary>
        /// The client's CRIMEINFO table: points per crime of each type.
        /// </summary>
        public static readonly int[] CrimeInfo = { 1, 10, 10, 1 };

        /// <summary>
        /// Seconds the client waits before asking to lower a count of 1, and a larger count (CRIMECONFIG).
        /// </summary>
        public const int LastPointSeconds = 30;
        public const int PointSeconds = 180;

        /// <summary>
        /// The count at which a player is exiled (GameServer.xml CrimeExileCount; 0 = never).
        /// </summary>
        public static int ExileCount = 30;

        /// <summary>
        /// Where exiled players are sent on Earth and in Space; null leaves them where they are.
        /// </summary>
        public static int[] ExileEarth;
        public static int[] ExileSpace;

        /// <summary>
        /// Whether crimes of a player of this account level count (the client's ACCOUNTATTRIBUTE: not 3, 4, 5).
        /// </summary>
        public static bool Counts(byte accountLevel)
        {
            return accountLevel < 3 || accountLevel > 5;
        }

        /// <summary>
        /// Adds <paramref name="n"/> crimes of <paramref name="type"/> as the client does. Returns true when the
        /// count went up from 0.
        /// </summary>
        public static bool Add(Character c, int type, int n)
        {
            int points = CrimeInfo[type] * n;
            if (points <= 0)
            {
                return false;
            }
            lock (c)
            {
                if (c.CrimeCount != 0)
                {
                    c.CrimeCount += points;
                    return false;
                }
                c.PreviousOffense++;
                c.CrimeCount = points + 1;
                return true;
            }
        }

        /// <summary>
        /// Seconds the client waits before asking to lower this count.
        /// </summary>
        public static int WaitSeconds(int count)
        {
            return count > 1 ? PointSeconds : LastPointSeconds;
        }

        /// <summary>
        /// Reads "x, y, z"; null when empty or not three numbers.
        /// </summary>
        public static int[] ParsePoint(string text)
        {
            var parts = (text ?? string.Empty).Split(',');
            if (parts.Length != 3)
            {
                return null;
            }
            var point = new int[3];
            for (int i = 0; i < 3; i++)
            {
                if (!int.TryParse(parts[i].Trim(), out point[i]))
                {
                    return null;
                }
            }
            return point;
        }
    }
}
