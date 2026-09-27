using Common.Network.Packets;

namespace Common.Characters
{
    /// <summary>
    /// Writes how a character on foot looks. Used by the character select screen (0x38002) and by the
    /// game server's looks (0x800A) and paper doll (0x806F) replies.
    ///
    /// <code>
    /// 0x94 then 8 x (uint16 BE wear id, style): dress, top, coat, bottom, shoes, gloves, hat, glasses
    /// 00 00, skin, 00, face, 00 00, hair style, hair colour, 00, 26 x 00
    /// </code>
    /// Java reference: mina_common Appearance.getHumanLooks. Matches the official 0x800A reply in
    /// UCGOZone-Login.pcap.
    /// </summary>
    public static class CharacterLooks
    {
        public static readonly ApparelType[] Order =
        {
            ApparelType.DRESS, ApparelType.TOP, ApparelType.COAT, ApparelType.BOTTOM,
            ApparelType.SHOES, ApparelType.GLOVES, ApparelType.HAT, ApparelType.GLASSES,
        };

        public static void Write<T>(UCPacket<T> p, Character c)
        {
            p.PutSize(20);
            foreach (var type in Order)
            {
                var apparel = c.GetApparel(type);
                p.PutShortBE((short)CharacterData.GetWearID(apparel.ItemID));
                p.PutByte((byte)apparel.Style);
            }
            p.PutShortBE(0);
            p.PutByte((byte)c.Skin);
            p.PutByte(0);
            p.PutByte((byte)c.Face);
            p.PutShortBE(0);
            p.PutByte((byte)c.HairStyle);
            p.PutByte((byte)c.HairColor);
            p.PutByte(0);
            p.PutBytes(new byte[26]);
        }
    }
}
