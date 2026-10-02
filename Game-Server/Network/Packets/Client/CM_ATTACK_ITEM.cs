using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x11: attack something on the ground (a vehicle nobody is in, a wreck): uint32 BE attacker, byte
    /// armament slot, byte crime flag (1 when the client knows the vehicle is another player's of its own faction;
    /// UC_Attack "CrimeFlag"), uint16 BE FFFF (sometimes 0000; echoed back), uint32 BE item unique id, format,
    /// template. Layout from the official captures (TEST_Z_GUNDAM.pcap, 88 attacks).
    /// </summary>
    public class CM_ATTACK_ITEM : UCPacket<GSOpcode>
    {
        public CM_ATTACK_ITEM()
        {
            this.ID = GSOpcode.CM_ATTACK_ITEM;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_ATTACK_ITEM();
        }

        public uint AttackerID { get; private set; }
        public int Slot { get; private set; }
        /// <summary>
        /// The melee special attack (SPECIALATTACKTEMPLATE id), FFFF none.
        /// </summary>
        public ushort SpecialAttackID { get; private set; }
        public bool CrimeFlag { get; private set; }
        public uint ItemUniqueID { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            if (this.Remaining < 20)
            {
                return;
            }
            AttackerID = this.GetUIntBE();
            Slot = this.GetByte();
            CrimeFlag = this.GetByte() == 1;
            SpecialAttackID = this.GetUShortBE();
            ItemUniqueID = this.GetUIntBE();

            ((UCGameSession)client).OnAttackItem(this);
        }
    }
}
