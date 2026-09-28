using Common.Network.Packets;
using SmartEngine.Network;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x6D: uint32 BE 1, uint32 BE id, byte 1. The client sends it after its shot destroyed an NPC (the id is
    /// the NPC's: 1000000043 in "Weapon_Manipulation_0.1___Ambac_0.1_(MS06RP).pcap" and
    /// "space_engagement_0.1.pcap"). The official server never answered it, so neither do we: the server
    /// already knows from the attack result.
    /// </summary>
    public class CM_TARGET_DESTROYED : UCPacket<GSOpcode>
    {
        public CM_TARGET_DESTROYED()
        {
            this.ID = GSOpcode.CM_TARGET_DESTROYED;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_TARGET_DESTROYED();
        }

        public override void OnProcess(Session<GSOpcode> client)
        {
        }
    }
}
