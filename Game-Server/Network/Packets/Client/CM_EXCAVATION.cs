using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x32: mine the block the player's vehicle stands on with its mining weapon (UC_ExcavationItem).
    ///
    /// <code>
    /// uint16 BE   section (1)
    /// uint16 BE   code (0)
    /// uint32 BE   character id
    /// uint16 BE   cluster id
    /// uint32 BE   block id (mine id * 256 + grid cell)
    /// uint32 BE   vehicle unique id, format
    /// uint32 BE   vehicle template id
    /// uint32 BE   mining weapon unique id, format
    /// byte        weapon durability used (0)
    /// UC size     items (0)
    /// </code>
    /// No official capture has it; the layout is the client's own (uc.exe builds it at 0x7c0710, and the reply
    /// uses the same class). The client only sends it from a vehicle that can mine, holding a mining weapon.
    /// Java reference: RequestExcavation.java / Excavation2.java (35 bytes, without the item count).
    /// </summary>
    public class CM_EXCAVATION : UCPacket<GSOpcode>
    {
        public const int HeaderLength = 35;

        public CM_EXCAVATION()
        {
            this.ID = GSOpcode.CM_EXCAVATION;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_EXCAVATION();
        }

        public byte[] Body { get; private set; }
        public uint CharacterID { get; private set; }
        public int BlockID { get; private set; }
        public uint VehicleUniqueID { get; private set; }
        public uint WeaponUniqueID { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            if (this.Remaining < HeaderLength)
            {
                return;
            }
            Body = this.GetBytes((ushort)this.Remaining);
            CharacterID = Bytes.U32(Body, 4);
            BlockID = (int)Bytes.U32(Body, 10);
            VehicleUniqueID = Bytes.U32(Body, 14);
            WeaponUniqueID = Bytes.U32(Body, 26);

            ((UCGameSession)client).OnExcavation(this);
        }
    }
}
