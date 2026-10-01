using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x17: moves an item between containers.
    ///
    /// <code>
    /// uint16 BE   section: 7 = between containers, 8 = get in a vehicle from the hangar,
    ///             9 = put the piloted vehicle back in the hangar, 0x0A = put money into a money item (the
    ///             "destination container" is the money item: the main money, 500000), 0x0B = the same into
    ///             another stackable item (specs/re-unknown-packets.md 3.3)
    /// uint16 BE   0
    /// uint32 BE   character id
    /// uint32 BE   item unique id, format
    /// uint32 BE   source container unique id, format
    /// uint32 BE   destination container unique id, format
    /// uint32 BE   0xFFFFFFFF
    /// uint32 BE   source container static id, destination container static id
    /// uint32 BE   0
    /// uint32 BE   amount
    /// 2 bytes    echoed in the reply (FF FF between containers, FF 00 get in, 00 FF put back)
    /// </code>
    /// Java reference: RequestMoveItem.java / MoveItem.java; layout from the official captures
    /// ("TGM-79 GM TRAINER in out of Hanger.pcap", "transfer lunatitaniumfrombankto781overweight.pcap").
    /// </summary>
    public class CM_MOVE_ITEM : UCPacket<GSOpcode>
    {
        public const int SectionContainers = 7;
        public const int SectionRide = 8;
        public const int SectionPutBack = 9;
        public const int SectionIntoMoney = 0x0A;
        public const int SectionIntoStack = 0x0B;

        public CM_MOVE_ITEM()
        {
            this.ID = GSOpcode.CM_MOVE_ITEM;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_MOVE_ITEM();
        }

        public int Section { get; private set; }
        public uint CharacterID { get; private set; }
        public uint ItemUniqueID { get; private set; }
        public int ItemFormat { get; private set; }
        public uint SourceUniqueID { get; private set; }
        public int SourceFormat { get; private set; }
        public uint DestUniqueID { get; private set; }
        public int DestFormat { get; private set; }
        public int SourceStaticID { get; private set; }
        public int DestStaticID { get; private set; }
        public int Amount { get; private set; }
        public byte[] Tail { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            Read();

            ((UCGameSession)client).OnMoveItem(this);
        }

        public void Read()
        {
            Section = this.GetUShortBE();
            this.GetUShortBE();
            CharacterID = this.GetUIntBE();
            ItemUniqueID = this.GetUIntBE();
            ItemFormat = this.GetIntBE();
            SourceUniqueID = this.GetUIntBE();
            SourceFormat = this.GetIntBE();
            DestUniqueID = this.GetUIntBE();
            DestFormat = this.GetIntBE();
            this.GetUIntBE();
            SourceStaticID = this.GetIntBE();
            DestStaticID = this.GetIntBE();
            this.GetUIntBE();
            Amount = this.GetIntBE();
            Tail = this.Remaining >= 2 ? this.GetBytes(2) : new byte[] { 0xFF, 0xFF };
        }
    }
}
