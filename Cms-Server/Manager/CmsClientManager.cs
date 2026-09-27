using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.CmsServer.Network.Client;
using TitansUC.CmsServer.Network.Packets.Client;

namespace TitansUC.CmsServer.Manager
{
    public class CmsClientManager : ClientManager<CMSOpcode>
    {
        static readonly CmsClientManager instance = new CmsClientManager();

        public static CmsClientManager Instance { get { return instance; } }

        public CmsClientManager()
        {
            RegisterPacketHandler(CMSOpcode.CM_LOGIN_CMS, new CM_LOGIN_CMS());
            RegisterPacketHandler(CMSOpcode.CM_LOGOUT_CMS, new CM_LOGOUT_CMS());
            RegisterPacketHandler(CMSOpcode.CM_CHAT_MSG, new CM_CHAT_MSG());
            RegisterPacketHandler(CMSOpcode.CM_HEARTBEAT, new CM_HEARTBEAT());
            RegisterPacketHandler(CMSOpcode.CM_RELAY, new CM_RELAY());
            RegisterPacketHandler(CMSOpcode.CM_TEAM_NAME, new CM_TEAM_NAME());
            RegisterPacketHandler(CMSOpcode.CM_TEAM_MEMBER_STATUS, new CM_TEAM_MEMBER_STATUS());
            RegisterPacketHandler(CMSOpcode.CM_FRIEND_STATUS, new CM_FRIEND_STATUS());
            RegisterPacketHandler(CMSOpcode.CM_CREATE_TEAM, new CM_CREATE_TEAM());
            RegisterPacketHandler(CMSOpcode.CM_TEAM_INFO, new CM_TEAM_INFO());
            RegisterPacketHandler(CMSOpcode.CM_TEAM_ADD_MEMBER, new CM_TEAM_ADD_MEMBER());
            RegisterPacketHandler(CMSOpcode.CM_TEAM_LEAVE, new CM_TEAM_LEAVE());
            RegisterPacketHandler(CMSOpcode.CM_TEAM_KICK, new CM_TEAM_KICK());
            RegisterPacketHandler(CMSOpcode.CM_CHAT_INFO, new CM_CHAT_INFO());
            RegisterPacketHandler(CMSOpcode.CM_TEAM_REGISTER_ONLINE, new CM_TEAM_REGISTER_ONLINE());
            RegisterPacketHandler(CMSOpcode.CM_GROUP_CHAT_CREATE, new CM_GROUP_CHAT_CREATE());
            RegisterPacketHandler(CMSOpcode.CM_GROUP_CHAT_MEMBERS, new CM_GROUP_CHAT_MEMBERS());
            RegisterPacketHandler(CMSOpcode.CM_GROUP_CHAT_ADD_MEMBER, new CM_GROUP_CHAT_ADD_MEMBER());
            RegisterPacketHandler(CMSOpcode.CM_GROUP_CHAT_LEAVE, new CM_GROUP_CHAT_LEAVE());
            RegisterPacketHandler(CMSOpcode.CM_COMMUNITY_LIST, new CM_COMMUNITY_LIST());
            RegisterPacketHandler(CMSOpcode.CM_ADD_FRIEND, new CM_ADD_FRIEND());
            RegisterPacketHandler(CMSOpcode.CM_DELETE_FRIEND, new CM_DELETE_FRIEND());
            RegisterPacketHandler(CMSOpcode.CM_FRIENDS_REGISTER_ONLINE, new CM_FRIENDS_REGISTER_ONLINE());
        }

        protected override Session<CMSOpcode> NewSession()
        {
            return new UCCmsSession();
        }
    }
}
