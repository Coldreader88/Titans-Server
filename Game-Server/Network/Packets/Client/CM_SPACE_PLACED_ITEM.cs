using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x23: drops an item on the ground, or gets out of the piloted vehicle leaving it there.
    ///
    /// <code>
    /// uint16 BE   mini op: 1 = drop an item, 2 = get out of the vehicle
    /// uint16 BE   0
    /// uint32 BE   character id
    /// uint32 BE   item (or vehicle) unique id, format
    /// uint32 BE   container unique id, format (0, 0 for money; weared for a vehicle)
    /// 20 bytes   0
    /// uint32 BE   amount
    /// uint32 BE   item static id, container static id
    /// uint32 BE   1 (3 for money)
    /// int32 BE    x, y, z
    /// 6 bytes    rotation
    /// byte       FF for an item, 00 for a vehicle
    /// </code>
    /// Java reference: RequestSpacePlacedItem.java; layout from the official captures (Alumina (11).pcap,
    /// Backpack Drop 11222 EF.pcap, BATTLE_1.pcap). The whole request is kept: the reply echoes it.
    /// </summary>
    public class CM_SPACE_PLACED_ITEM : UCPacket<GSOpcode>
    {
        public const int DropItem = 1;
        public const int GetOff = 2;

        public CM_SPACE_PLACED_ITEM()
        {
            this.ID = GSOpcode.CM_SPACE_PLACED_ITEM;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_SPACE_PLACED_ITEM();
        }

        public byte[] Body { get; private set; }
        public int MiniOp { get; private set; }
        public uint CharacterID { get; private set; }
        public uint ItemUniqueID { get; private set; }
        public uint ContainerUniqueID { get; private set; }
        public int Amount { get; private set; }
        public int X { get; private set; }
        public int Y { get; private set; }
        public int Z { get; private set; }
        public byte[] Rotation { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            if (!Read())
            {
                return;
            }

            ((UCGameSession)client).OnSpacePlacedItem(this);
        }

        public bool Read()
        {
            if (this.Remaining < 79)
            {
                return false;
            }
            Body = this.GetBytes((ushort)this.Remaining);
            MiniOp = Bytes.U16(Body, 0);
            CharacterID = Bytes.U32(Body, 4);
            ItemUniqueID = Bytes.U32(Body, 8);
            ContainerUniqueID = Bytes.U32(Body, 16);
            Amount = (int)Bytes.U32(Body, 44);
            X = (int)Bytes.U32(Body, 60);
            Y = (int)Bytes.U32(Body, 64);
            Z = (int)Bytes.U32(Body, 68);
            Rotation = Bytes.Slice(Body, 72, 6);
            return true;
        }
    }
}
