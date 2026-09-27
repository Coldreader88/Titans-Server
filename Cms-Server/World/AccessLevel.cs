namespace TitansUC.CmsServer.World
{
    /// <summary>
    /// GM tags (characters.char_access) in the order the Java server ranked them (Tag.java ordinals:
    /// PLAYER, GM, VIP, EVENT, ADMIN). A command needing GM can be used by GM, VIP, EVENT and ADMIN.
    /// Any other char_access value counts as a player.
    /// </summary>
    public static class AccessLevel
    {
        public const int PlayerTag = 10;
        public const int GMTag = 4;
        public const int VIPTag = 5;
        public const int EventTag = 7;
        public const int AdminTag = 9;

        public const int Player = 0;
        public const int GM = 1;
        public const int VIP = 2;
        public const int Event = 3;
        public const int Admin = 4;

        /// <summary>
        /// The rank of a char_access value (0 = player ... 4 = admin).
        /// </summary>
        public static int Of(int access)
        {
            switch (access)
            {
                case GMTag: return GM;
                case VIPTag: return VIP;
                case EventTag: return Event;
                case AdminTag: return Admin;
                default: return Player;
            }
        }

        public static string Name(int level)
        {
            switch (level)
            {
                case GM: return "GM";
                case VIP: return "VIP";
                case Event: return "EVENT";
                case Admin: return "ADMIN";
                default: return "PLAYER";
            }
        }
    }
}
