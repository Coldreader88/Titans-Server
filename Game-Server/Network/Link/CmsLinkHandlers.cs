using Common.Network.Packets;
using SmartEngine.Network;

namespace TitansUC.GameServer.Network.Link
{
    /// <summary>
    /// 0x00: uint32 BE character id, int32 BE x, y, z. Java: RequestPlayerTeleport.java. See <see cref="GameLink"/>.
    /// </summary>
    public class CMS_TELEPORT : UCPacket<CGOpcode>
    {
        public CMS_TELEPORT()
        {
            this.ID = CGOpcode.CMS_TELEPORT;
        }

        public override Packet<CGOpcode> New()
        {
            return new CMS_TELEPORT();
        }

        public override void OnProcess(Session<CGOpcode> client)
        {
            var link = (CmsLink)client;
            uint id = this.GetUIntBE();
            int x = this.GetIntBE();
            int y = this.GetIntBE();
            int z = this.GetIntBE();
            link.OnTeleport(id, x, y, z);
        }
    }

    /// <summary>
    /// 0x01: uint32 BE character id, UC string arguments joined with "::". See <see cref="GameLink"/>.
    /// </summary>
    public class CMS_SPAWN : UCPacket<CGOpcode>
    {
        public CMS_SPAWN()
        {
            this.ID = CGOpcode.CMS_SPAWN;
        }

        public override Packet<CGOpcode> New()
        {
            return new CMS_SPAWN();
        }

        public override void OnProcess(Session<CGOpcode> client)
        {
            var link = (CmsLink)client;
            uint id = this.GetUIntBE();
            link.OnSpawn(id, this.GetUCString());
        }
    }

    /// <summary>
    /// 0x02: int32 BE seconds until the server closes. Java: RequestGSClosure.java. See <see cref="GameLink"/>.
    /// </summary>
    public class CMS_CLOSURE : UCPacket<CGOpcode>
    {
        public CMS_CLOSURE()
        {
            this.ID = CGOpcode.CMS_CLOSURE;
        }

        public override Packet<CGOpcode> New()
        {
            return new CMS_CLOSURE();
        }

        public override void OnProcess(Session<CGOpcode> client)
        {
            var link = (CmsLink)client;
            link.OnClosure(this.GetIntBE());
        }
    }

    /// <summary>
    /// 0x03: a 0 byte. Java: RequestEndMaintenance.java. See <see cref="GameLink"/>.
    /// </summary>
    public class CMS_END_MAINTENANCE : UCPacket<CGOpcode>
    {
        public CMS_END_MAINTENANCE()
        {
            this.ID = CGOpcode.CMS_END_MAINTENANCE;
        }

        public override Packet<CGOpcode> New()
        {
            return new CMS_END_MAINTENANCE();
        }

        public override void OnProcess(Session<CGOpcode> client)
        {
            var link = (CmsLink)client;
            link.OnEndMaintenance();
        }
    }

    /// <summary>
    /// 0x06: uint32 BE character id, UC string message. Java: RequestPositionLog.java. See <see cref="GameLink"/>.
    /// </summary>
    public class CMS_POSITION_LOG : UCPacket<CGOpcode>
    {
        public CMS_POSITION_LOG()
        {
            this.ID = CGOpcode.CMS_POSITION_LOG;
        }

        public override Packet<CGOpcode> New()
        {
            return new CMS_POSITION_LOG();
        }

        public override void OnProcess(Session<CGOpcode> client)
        {
            var link = (CmsLink)client;
            uint id = this.GetUIntBE();
            link.OnPositionLog(id, this.GetUCString());
        }
    }

    /// <summary>
    /// 0x08: uint32 BE character to move, uint32 BE character to move to. Java: RequestTeleportToPlayer.java. See <see cref="GameLink"/>.
    /// </summary>
    public class CMS_TELEPORT_TO_PLAYER : UCPacket<CGOpcode>
    {
        public CMS_TELEPORT_TO_PLAYER()
        {
            this.ID = CGOpcode.CMS_TELEPORT_TO_PLAYER;
        }

        public override Packet<CGOpcode> New()
        {
            return new CMS_TELEPORT_TO_PLAYER();
        }

        public override void OnProcess(Session<CGOpcode> client)
        {
            var link = (CmsLink)client;
            uint from = this.GetUIntBE();
            link.OnTeleportTo(from, this.GetUIntBE());
        }
    }
}
