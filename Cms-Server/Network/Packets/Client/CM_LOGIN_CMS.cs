using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.CmsServer.Network.Client;

namespace TitansUC.CmsServer.Network.Packets.Client
{
    /// <summary>
    /// 0x01: log in to the CMS server with the character that entered the game.
    ///
    /// <code>
    /// uint32 BE   character id
    /// UC string   character name
    /// uint32 BE   0xFFFFFFFF
    /// </code>
    /// Java reference: RequestLoginCMS.java; layout from UCGOZone-Login.pcap.
    /// </summary>
    public class CM_LOGIN_CMS : CmsPacket
    {
        public CM_LOGIN_CMS()
        {
            this.ID = CMSOpcode.CM_LOGIN_CMS;
        }

        public override Packet<CMSOpcode> New()
        {
            return new CM_LOGIN_CMS();
        }

        public uint CharacterID { get; private set; }

        public string Name { get; private set; }

        public override void OnProcess(Session<CMSOpcode> client)
        {
            Read();

            ((UCCmsSession)client).OnLogin(this);
        }

        public void Read()
        {
            CharacterID = this.GetUIntBE();
            Name = this.Remaining > 0 ? this.GetUCString() : string.Empty;
        }
    }
}
