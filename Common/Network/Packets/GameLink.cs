using System.Collections.Generic;

namespace Common.Network.Packets
{
    /// <summary>
    /// Bodies of the packets on the link between the CMS server and the game servers (see
    /// <see cref="CGOpcode"/>). The link uses the same framing and encryption as the client connections.
    /// Layouts follow the Java cluster packets (mina_cmsserver cluster/, mina_gameserver cluster/cms/)
    /// except where noted.
    /// </summary>
    public static class GameLink
    {
        /// <summary>
        /// 0x7F, game server -> CMS: UC string password (CMSServer.xml GameLinkPassword). Not in Java.
        /// </summary>
        public static UCPacket<CGOpcode> Hello(string password)
        {
            var p = New(CGOpcode.GS_LINK_HELLO);
            p.PutUCString(password);
            return p;
        }

        /// <summary>
        /// 0x00, CMS -> game server: uint32 BE character id, int32 BE x, y, z. Java: NotifyPlayerTeleport.
        /// </summary>
        public static UCPacket<CGOpcode> Teleport(uint characterID, int x, int y, int z)
        {
            var p = New(CGOpcode.CMS_TELEPORT);
            p.PutUIntBE(characterID);
            p.PutIntBE(x);
            p.PutIntBE(y);
            p.PutIntBE(z);
            return p;
        }

        /// <summary>
        /// 0x01, CMS -> game server: uint32 BE character id, UC string arguments joined with "::".
        /// The Java server looked the item up itself and sent ids; the C# CMS server has no item
        /// templates, so the game server gets the arguments as typed.
        /// </summary>
        public static UCPacket<CGOpcode> Spawn(uint characterID, IEnumerable<string> args)
        {
            var p = New(CGOpcode.CMS_SPAWN);
            p.PutUIntBE(characterID);
            p.PutUCString(string.Join("::", args));
            return p;
        }

        /// <summary>
        /// 0x02, CMS -> game server: int32 BE seconds until the game server closes. Java: NotifyGSClosure.
        /// </summary>
        public static UCPacket<CGOpcode> Closure(int seconds)
        {
            var p = New(CGOpcode.CMS_CLOSURE);
            p.PutIntBE(seconds);
            return p;
        }

        /// <summary>
        /// 0x03, CMS -> game server: a 0 byte. Java: NotifyEndMaintenance.
        /// </summary>
        public static UCPacket<CGOpcode> EndMaintenance()
        {
            var p = New(CGOpcode.CMS_END_MAINTENANCE);
            p.PutByte(0);
            return p;
        }

        /// <summary>
        /// 0x06, CMS -> game server: uint32 BE character id, UC string message. Java: NotifyPositionLog.
        /// </summary>
        public static UCPacket<CGOpcode> PositionLog(uint characterID, string message)
        {
            var p = New(CGOpcode.CMS_POSITION_LOG);
            p.PutUIntBE(characterID);
            p.PutUCString(message);
            return p;
        }

        /// <summary>
        /// 0x08, CMS -> game server: uint32 BE character to move, uint32 BE character to move to.
        /// Java: NotifyTeleportToPlayer.
        /// </summary>
        public static UCPacket<CGOpcode> TeleportTo(uint fromID, uint toID)
        {
            var p = New(CGOpcode.CMS_TELEPORT_TO_PLAYER);
            p.PutUIntBE(fromID);
            p.PutUIntBE(toID);
            return p;
        }

        /// <summary>
        /// 0x01, game server -> CMS: uint32 BE NPC id, UC string message, said to everyone. Java: NotifyNPCChat.
        /// </summary>
        public static UCPacket<CGOpcode> NpcChat(uint npcID, string message)
        {
            var p = New(CGOpcode.GS_NPC_CHAT);
            p.PutUIntBE(npcID);
            p.PutUCString(message);
            return p;
        }

        /// <summary>
        /// 0x5A, game server -> CMS: UC string, a system message to everyone. Java: NotifyGameSystemMessage.
        /// </summary>
        public static UCPacket<CGOpcode> SystemMessage(string message)
        {
            var p = New(CGOpcode.GS_SYSTEM_MESSAGE);
            p.PutUCString(message);
            return p;
        }

        /// <summary>
        /// 0x5B, game server -> CMS: uint32 BE character id, UC string, a system message to one player.
        /// </summary>
        public static UCPacket<CGOpcode> SystemMessage(uint characterID, string message)
        {
            var p = New(CGOpcode.GS_PLAYER_SYSTEM_MESSAGE);
            p.PutUIntBE(characterID);
            p.PutUCString(message);
            return p;
        }

        private static UCPacket<CGOpcode> New(CGOpcode opcode)
        {
            return new UCPacket<CGOpcode> { ID = opcode };
        }
    }
}
