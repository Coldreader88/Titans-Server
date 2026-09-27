using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x16: asks for one container or item.
    ///
    /// <code>
    /// uint32 BE   0x00020000
    /// uint32 BE   character id
    /// uint32 BE   unique id, format
    /// uint32 BE   parent unique id, parent format (0 for a top-level container)
    /// uint32 BE   static id (container static id or item template id)
    /// uint32 BE   parent static id
    /// uint32 BE   0x0B
    /// </code>
    /// Java reference: RequestItemInfo.java; layout from UCGOZone-Login.pcap.
    /// </summary>
    public class CM_ITEM_INFO : UCPacket<GSOpcode>
    {
        public CM_ITEM_INFO()
        {
            this.ID = GSOpcode.CM_ITEM_INFO;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_ITEM_INFO();
        }

        public uint CharacterID { get; private set; }

        public uint UniqueID { get; private set; }

        public int Format { get; private set; }

        public uint ParentUniqueID { get; private set; }

        public int ParentFormat { get; private set; }

        public int StaticID { get; private set; }

        public int ParentStaticID { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            Read();

            ((UCGameSession)client).OnItemInfo(this);
        }

        public void Read()
        {
            this.GetUIntBE();
            CharacterID = this.GetUIntBE();
            UniqueID = this.GetUIntBE();
            Format = this.GetIntBE();
            ParentUniqueID = this.GetUIntBE();
            ParentFormat = this.GetIntBE();
            StaticID = this.GetIntBE();
            ParentStaticID = this.GetIntBE();
        }
    }
}
