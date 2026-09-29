using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x25: gives a vehicle standing on the ground to another player, or gives it up.
    ///
    /// <code>
    /// uint32 BE   character id (the one asking)
    /// uint32 BE   new owner's character id (FFFFFFFF = nobody)
    /// uint32 BE   vehicle static id
    /// uint32 BE   vehicle unique id, format (0x14)
    /// uint32 BE   0
    /// </code>
    /// Layout from Transfer_Oggo.pcap (to another player), Empty_Oggo_(I_Owner_it).pcap (to themselves) and
    /// LOG3.pcap (FFFFFFFF). Java reference: RequestChangeMachineOwner.java.
    /// </summary>
    public class CM_CHANGE_MACHINE_OWNER : UCPacket<GSOpcode>
    {
        public CM_CHANGE_MACHINE_OWNER()
        {
            this.ID = GSOpcode.CM_CHANGE_MACHINE_OWNER;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_CHANGE_MACHINE_OWNER();
        }

        public uint CharacterID { get; private set; }
        public uint NewOwnerID { get; private set; }
        public int StaticID { get; private set; }
        public uint VehicleUniqueID { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            if (this.Remaining < 20)
            {
                return;
            }
            var body = this.GetBytes((ushort)this.Remaining);
            CharacterID = Bytes.U32(body, 0);
            NewOwnerID = Bytes.U32(body, 4);
            StaticID = (int)Bytes.U32(body, 8);
            VehicleUniqueID = Bytes.U32(body, 12);

            ((UCGameSession)client).OnChangeMachineOwner(this);
        }
    }
}
