using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x24: picks an item up from the ground, or gets in a vehicle standing there.
    ///
    /// <code>
    /// uint16 BE   mini op: 1 = pick up an item, 2 = pick up money onto the money there, 3 = get in a
    ///             vehicle, 4 = pick up an item onto the stack of it there
    /// uint16 BE   0
    /// uint32 BE   character id
    /// uint32 BE   ground item unique id, format
    /// uint32 BE   destination container unique id, format (weared for a vehicle)
    /// 28 bytes   0
    /// uint32 BE   amount
    /// uint32 BE   item static id, destination container static id
    /// uint32 BE   1
    /// byte       FF for an item, 00 for a vehicle
    /// </code>
    /// The official requests were 69 bytes (28 zero bytes); the fields after the zeros are read from the
    /// end. Java reference: RequestSpacePickupItemBuff.java; layout from BATTLE_1.pcap.
    /// </summary>
    public class CM_SPACE_PICKUP_ITEM : UCPacket<GSOpcode>
    {
        public const int PickUpItem = 1;
        public const int PickUpMoney = 2;
        public const int GetIn = 3;
        public const int PickUpOntoStack = 4;

        public CM_SPACE_PICKUP_ITEM()
        {
            this.ID = GSOpcode.CM_SPACE_PICKUP_ITEM;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_SPACE_PICKUP_ITEM();
        }

        public byte[] Body { get; private set; }
        public int MiniOp { get; private set; }
        public uint CharacterID { get; private set; }
        public uint ItemUniqueID { get; private set; }
        public uint DestUniqueID { get; private set; }
        public int Amount { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            if (!Read())
            {
                return;
            }

            ((UCGameSession)client).OnSpacePickupItem(this);
        }

        public bool Read()
        {
            if (this.Remaining < 41)
            {
                return false;
            }
            Body = this.GetBytes((ushort)this.Remaining);
            MiniOp = Bytes.U16(Body, 0);
            CharacterID = Bytes.U32(Body, 4);
            ItemUniqueID = Bytes.U32(Body, 8);
            DestUniqueID = Bytes.U32(Body, 16);
            Amount = (int)Bytes.U32(Body, Body.Length - 17);
            return true;
        }
    }
}
