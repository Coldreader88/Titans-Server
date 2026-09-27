using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.CmsServer.Network.Client;

namespace TitansUC.CmsServer.Network.Packets.Client
{
    /// <summary>
    /// 0x14: tells the team the player is online.
    ///
    /// <code>
    /// uint32 BE   character id
    /// uint32 BE   1
    /// </code>
    /// Java reference: RequestTeamRegisterOnline.java; layout from Login GM.pcap.
    /// </summary>
    public class CM_TEAM_REGISTER_ONLINE : CmsPacket
    {
        public CM_TEAM_REGISTER_ONLINE()
        {
            this.ID = CMSOpcode.CM_TEAM_REGISTER_ONLINE;
        }

        public override Packet<CMSOpcode> New()
        {
            return new CM_TEAM_REGISTER_ONLINE();
        }

        public uint CharacterID { get; private set; }

        public uint Value { get; private set; }

        public override void OnProcess(Session<CMSOpcode> client)
        {
            Read();

            ((UCCmsSession)client).OnTeamRegisterOnline(this);
        }

        public void Read()
        {
            CharacterID = this.GetUIntBE();
            Value = this.GetUIntBE();
        }
    }
}
