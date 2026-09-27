using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x15: throws an item away; the client sends it for a car or shuttle bought to ride away once it is
    /// done with it (BATTLE_1.pcap, Earth_To_Space.pcap).
    ///
    /// <code>
    /// uint16 BE   3, uint16 BE 0
    /// uint32 BE   character id
    /// uint32 BE   item unique id, format
    /// uint32 BE   container unique id, format
    /// uint32 BE   item static id, container static id
    /// uint32 BE   1
    /// byte       0
    /// </code>
    /// The reply (0x8015) is the request with 2 in bytes 2-3. Java reference: RequestDeleteItem.java (which
    /// also got the player out of the vehicle).
    /// </summary>
    public class CM_DELETE_ITEM : UCPacket<GSOpcode>
    {
        public CM_DELETE_ITEM()
        {
            this.ID = GSOpcode.CM_DELETE_ITEM;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_DELETE_ITEM();
        }

        public byte[] Body { get; private set; }
        public uint CharacterID { get; private set; }
        public uint ItemUniqueID { get; private set; }
        public uint ContainerUniqueID { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            if (this.Remaining < 24)
            {
                return;
            }
            Body = this.GetBytes((ushort)this.Remaining);
            CharacterID = Bytes.U32(Body, 4);
            ItemUniqueID = Bytes.U32(Body, 8);
            ContainerUniqueID = Bytes.U32(Body, 16);

            ((UCGameSession)client).OnDeleteItem(this);
        }
    }
}
