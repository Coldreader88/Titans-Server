using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x1D: reload a weapon: uint16 BE 1, uint16 BE 0, uint32 BE character id, ammunition unique id and
    /// format, its container's unique id and format, weapon unique id and format, uint32 BE 0, uint32 BE rounds.
    /// The reply (0x801D) is the request with 2 in bytes 2-3 (100mm_MG_Reload_(40_rounds).pcap).
    /// </summary>
    public class CM_USE_ITEM_WITH_TARGET : UCPacket<GSOpcode>
    {
        public CM_USE_ITEM_WITH_TARGET()
        {
            this.ID = GSOpcode.CM_USE_ITEM_WITH_TARGET;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_USE_ITEM_WITH_TARGET();
        }

        public byte[] Body { get; private set; }
        public uint CharacterID { get; private set; }
        public uint AmmoUID { get; private set; }
        public uint ContainerUID { get; private set; }
        public uint WeaponUID { get; private set; }
        public int Rounds { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            if (this.Remaining < 40)
            {
                return;
            }
            Body = this.GetBytes((ushort)this.Remaining);
            CharacterID = Bytes.U32(Body, 4);
            AmmoUID = Bytes.U32(Body, 8);
            ContainerUID = Bytes.U32(Body, 16);
            WeaponUID = Bytes.U32(Body, 24);
            Rounds = (int)Bytes.U32(Body, 36);

            ((UCGameSession)client).OnReload(this);
        }
    }
}
