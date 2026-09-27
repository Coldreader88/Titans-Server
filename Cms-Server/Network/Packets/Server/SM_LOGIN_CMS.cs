using Common.Network.Packets;

namespace TitansUC.CmsServer.Network.Packets.Server
{
    /// <summary>
    /// 0x8001: the CMS login reply.
    ///
    /// <code>
    /// UC string   character name
    /// uint32 BE   0x06B4
    /// uint32 BE   varies per login on the official server (meaning unknown)
    /// uint32 BE   1
    /// 8 x 00
    /// int32 BE    team id (-1 = none)
    /// uint32 BE   0xFFFFFFFF
    /// byte        1
    /// uint16 BE   0
    /// byte        rank
    /// UC size     0
    /// </code>
    /// Java reference: NotifyLoginCMS.java; checked against 16 official logins (UCGOZone-Login.pcap,
    /// Login GM.pcap, Zoning in, EFF.pcap, ...).
    /// </summary>
    public class SM_LOGIN_CMS : CmsPacket
    {
        public SM_LOGIN_CMS(string name, int teamID, byte rank, uint loginNumber)
        {
            this.ID = CMSOpcode.SM_LOGIN_CMS;

            this.PutUCString(name);
            this.PutUIntBE(0x06B4);
            this.PutUIntBE(loginNumber);
            this.PutUIntBE(1);
            this.PutBytes(new byte[8]);
            this.PutIntBE(teamID);
            this.PutUIntBE(0xFFFFFFFF);
            this.PutByte(1);
            this.PutUShortBE(0);
            this.PutByte(rank);
            this.PutSize(0);
        }
    }
}
