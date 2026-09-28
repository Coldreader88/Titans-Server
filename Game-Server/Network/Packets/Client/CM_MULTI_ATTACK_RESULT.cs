using System.Collections.Generic;
using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x67: the player fired a weapon that can hit several targets (beam weapons).
    ///
    /// <code>
    /// uint32 BE   attacker character id
    /// byte       armament slot
    /// uint16 BE   FFFF
    /// uint32 BE   skill id
    /// int32 BE    x, y, z of the impact
    /// UC size    targets, each: uint32 BE character id, uint32 BE vehicle unique id, uint16 BE distance,
    ///            int32 BE x, y, z, uint32 BE skill id
    /// </code>
    /// Layout from the official captures (BATTLE_1.pcap, LOG.pcap).
    /// </summary>
    public class CM_MULTI_ATTACK_RESULT : UCPacket<GSOpcode>
    {
        public CM_MULTI_ATTACK_RESULT()
        {
            this.ID = GSOpcode.CM_MULTI_ATTACK_RESULT;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_MULTI_ATTACK_RESULT();
        }

        public uint AttackerID { get; private set; }
        public int Slot { get; private set; }
        public int X { get; private set; }
        public int Y { get; private set; }
        public int Z { get; private set; }
        public List<MultiTarget> Targets { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            if (this.Remaining < 24)
            {
                return;
            }
            AttackerID = this.GetUIntBE();
            Slot = this.GetByte();
            this.GetUShortBE();
            this.GetUIntBE();
            X = this.GetIntBE();
            Y = this.GetIntBE();
            Z = this.GetIntBE();
            int count = this.GetSize();
            Targets = new List<MultiTarget>();
            for (int i = 0; i < count && this.Remaining >= 26; i++)
            {
                var t = new MultiTarget();
                t.TargetID = this.GetUIntBE();
                t.VehicleUID = this.GetUIntBE();
                t.Distance = this.GetUShortBE();
                this.GetBytes(12);
                this.GetUIntBE();
                Targets.Add(t);
            }

            ((UCGameSession)client).OnMultiAttackResult(this);
        }
    }

    public class MultiTarget
    {
        public uint TargetID { get; set; }
        public uint VehicleUID { get; set; }
        public int Distance { get; set; }
    }
}
