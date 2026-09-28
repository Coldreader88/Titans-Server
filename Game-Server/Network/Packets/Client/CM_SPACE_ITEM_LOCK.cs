using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x26 (lock) and 0x27 (list): the player opens a vehicle or wreck on the ground to look inside.
    ///
    /// <code>
    /// uint16 BE   1, uint16 BE (anything; 0x0A82 and 0x0F98 seen)
    /// uint32 BE   vehicle unique id, format (0x14)
    /// uint32 BE   vehicle template
    /// uint32 BE   character id of the player
    /// </code>
    /// The client sends 0x26 first and 0x27 once 0x8026 lets it in (BATTLE_1.pcap; Java
    /// RequestSpaceItemLockControl and RequestSpaceItemContainerList). 0x8026 is the request with bytes 2-3 =
    /// 0002 (0001 refuses, the client then writes in the journal that it may not open it).
    /// </summary>
    public class CM_SPACE_ITEM_LOCK : UCPacket<GSOpcode>
    {
        public CM_SPACE_ITEM_LOCK(GSOpcode opcode)
        {
            this.ID = opcode;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_SPACE_ITEM_LOCK(this.ID);
        }

        public byte[] Body { get; private set; }
        public uint VehicleUniqueID { get; private set; }
        public uint CharacterID { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            if (this.Remaining < 20)
            {
                return;
            }
            Body = this.GetBytes((ushort)this.Remaining);
            VehicleUniqueID = Bytes.U32(Body, 4);
            CharacterID = Bytes.U32(Body, 16);

            var session = (UCGameSession)client;
            if (this.ID == GSOpcode.CM_SPACE_ITEM_LOCK)
            {
                session.OnSpaceItemLock(this);
            }
            else
            {
                session.OnSpaceItemList(this);
            }
        }
    }
}
