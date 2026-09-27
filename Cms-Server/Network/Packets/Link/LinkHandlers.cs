using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.CmsServer.Network.Client;

namespace TitansUC.CmsServer.Network.Packets.Link
{
    /// <summary>
    /// 0x7F from a game server: UC string link password. See <see cref="GameLink.Hello"/>.
    /// </summary>
    public class GS_LINK_HELLO : UCPacket<CGOpcode>
    {
        public GS_LINK_HELLO()
        {
            this.ID = CGOpcode.GS_LINK_HELLO;
        }

        public override Packet<CGOpcode> New()
        {
            return new GS_LINK_HELLO();
        }

        public override void OnProcess(Session<CGOpcode> client)
        {
            ((GameLinkSession)client).OnHello(this.GetUCString());
        }
    }

    /// <summary>
    /// 0x01 from a game server: uint32 BE NPC id, UC string message. See <see cref="GameLink.NpcChat"/>.
    /// Java reference: cluster/incoming/RequestNPCChat.java.
    /// </summary>
    public class GS_NPC_CHAT : UCPacket<CGOpcode>
    {
        public GS_NPC_CHAT()
        {
            this.ID = CGOpcode.GS_NPC_CHAT;
        }

        public override Packet<CGOpcode> New()
        {
            return new GS_NPC_CHAT();
        }

        public override void OnProcess(Session<CGOpcode> client)
        {
            uint npcID = this.GetUIntBE();
            ((GameLinkSession)client).OnNpcChat(npcID, this.GetUCString());
        }
    }

    /// <summary>
    /// 0x5A from a game server: UC string, a system message to everyone.
    /// Java reference: cluster/incoming/RequestGameSystemMessage.java.
    /// </summary>
    public class GS_SYSTEM_MESSAGE : UCPacket<CGOpcode>
    {
        public GS_SYSTEM_MESSAGE()
        {
            this.ID = CGOpcode.GS_SYSTEM_MESSAGE;
        }

        public override Packet<CGOpcode> New()
        {
            return new GS_SYSTEM_MESSAGE();
        }

        public override void OnProcess(Session<CGOpcode> client)
        {
            ((GameLinkSession)client).OnSystemMessage(0, this.GetUCString());
        }
    }

    /// <summary>
    /// 0x5B from a game server: uint32 BE character id, UC string, a system message to one player.
    /// Java reference: cluster/incoming/RequestGameSystemMessage.java.
    /// </summary>
    public class GS_PLAYER_SYSTEM_MESSAGE : UCPacket<CGOpcode>
    {
        public GS_PLAYER_SYSTEM_MESSAGE()
        {
            this.ID = CGOpcode.GS_PLAYER_SYSTEM_MESSAGE;
        }

        public override Packet<CGOpcode> New()
        {
            return new GS_PLAYER_SYSTEM_MESSAGE();
        }

        public override void OnProcess(Session<CGOpcode> client)
        {
            uint characterID = this.GetUIntBE();
            ((GameLinkSession)client).OnSystemMessage(characterID, this.GetUCString());
        }
    }
}
