using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x0D GrowBattleSkill: byte 0 (the combat skill table), uint16 BE skill index. The client sends one for each
    /// operation skill its vehicle needs (0 mobile suit on a Z Gundam in TEST_Z_GUNDAM.pcap) after about 300
    /// seconds of travel at full speed, then waits for 0x800D before it measures again, so every 0x0D is answered
    /// (see <see cref="UCGameSession.OnGrowBattleSkill"/>). Layout from the client (UCC_Eternal::RequestGrowVehicleOperationSkill).
    /// </summary>
    public class CM_GROW_BATTLE_SKILL : UCPacket<GSOpcode>
    {
        public CM_GROW_BATTLE_SKILL()
        {
            this.ID = GSOpcode.CM_GROW_BATTLE_SKILL;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_GROW_BATTLE_SKILL();
        }

        public byte Type { get; private set; }
        public int Index { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            if (this.Remaining >= 3)
            {
                Type = this.GetByte();
                Index = this.GetUShortBE();
            }
            else
            {
                Type = 0xFF;
                Index = -1;
            }
            ((UCGameSession)client).OnGrowBattleSkill(this);
        }
    }
}
