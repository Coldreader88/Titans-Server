using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x69: repairs another player's vehicle with the MR tool kit the player's vehicle holds.
    ///
    /// <code>
    /// uint32 BE   character id (the one repairing)
    /// uint32 BE   character id of the one repaired
    /// uint32 BE   their vehicle's unique id
    /// byte        weapon (armament slot of the kit)
    /// int16 BE    special attack id (-1)
    /// </code>
    /// Field names from the client (UC_RepairPlayer: SrcPlayerID, TgtPlayerID, TgtHighVehicleID, Weapon,
    /// SpAttackID); layout from Oggo_Repair_Other_(I_repair_MS06D).pcap. Java reference: RequestRepairPlayer.java.
    /// </summary>
    public class CM_REPAIR_PLAYER : UCPacket<GSOpcode>
    {
        public CM_REPAIR_PLAYER()
        {
            this.ID = GSOpcode.CM_REPAIR_PLAYER;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_REPAIR_PLAYER();
        }

        public uint CharacterID { get; private set; }
        public uint TargetID { get; private set; }
        public uint VehicleUniqueID { get; private set; }
        public byte Weapon { get; private set; }
        public short SpecialAttackID { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            if (this.Remaining < 15)
            {
                return;
            }
            var body = this.GetBytes((ushort)this.Remaining);
            CharacterID = Bytes.U32(body, 0);
            TargetID = Bytes.U32(body, 4);
            VehicleUniqueID = Bytes.U32(body, 8);
            Weapon = body[12];
            SpecialAttackID = (short)Bytes.U16(body, 13);

            ((UCGameSession)client).OnRepairPlayer(this);
        }
    }
}
