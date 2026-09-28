using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x0F: the player fired at one target.
    ///
    /// <code>
    /// uint32 BE   attacker character id
    /// uint32 BE   target character id (or NPC id)
    /// uint32 BE   target's vehicle unique id (0 for NPCs)
    /// byte       armament slot that fired (the weapon is not in the packet)
    /// uint32 BE   distance to the target (about world units / 4)
    /// uint16 BE   FFFF
    /// int32 BE    x, y, z where the shot lands
    /// UC size    skill ids used (4 bytes each), then an empty list
    /// </code>
    /// Layout from the official captures (BATTLE_1.pcap, 100mm_MG_cartridge_empty.pcap). Java reference:
    /// RequestAttackResult.java.
    /// </summary>
    public class CM_ATTACK_RESULT : UCPacket<GSOpcode>
    {
        public CM_ATTACK_RESULT()
        {
            this.ID = GSOpcode.CM_ATTACK_RESULT;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_ATTACK_RESULT();
        }

        public uint AttackerID { get; private set; }
        public uint TargetID { get; private set; }
        public uint TargetVehicleUID { get; private set; }
        public int Slot { get; private set; }
        public int Distance { get; private set; }
        public int X { get; private set; }
        public int Y { get; private set; }
        public int Z { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            if (this.Remaining < 31)
            {
                return;
            }
            AttackerID = this.GetUIntBE();
            TargetID = this.GetUIntBE();
            TargetVehicleUID = this.GetUIntBE();
            Slot = this.GetByte();
            Distance = this.GetIntBE();
            this.GetUShortBE();
            X = this.GetIntBE();
            Y = this.GetIntBE();
            Z = this.GetIntBE();

            ((UCGameSession)client).OnAttackResult(this);
        }
    }
}
