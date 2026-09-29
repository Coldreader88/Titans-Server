using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// The battle town requests (layouts from the client; no capture has them):
    /// <code>
    /// 0x71 StartOccupation:                uint32 BE city
    /// 0x73 CaptureFlag:                    uint32 BE 1, character id, city; byte ICF (0-4)
    /// 0x74 RegisterPlayerToOccupation:     uint32 BE city, character id; byte FF
    /// 0x75 UnregisterPlayerFromOccupation: uint32 BE city, character id; byte FF
    /// </code>
    /// </summary>
    public class CM_OCCUPATION : UCPacket<GSOpcode>
    {
        private readonly GSOpcode opcode;

        public CM_OCCUPATION(GSOpcode opcode)
        {
            this.opcode = opcode;
            this.ID = opcode;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_OCCUPATION(opcode);
        }

        public GSOpcode Request { get { return opcode; } }
        public int CityID { get; private set; }
        public uint CharacterID { get; private set; }
        public int Flag { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            Flag = -1;
            CityID = -1;
            switch (opcode)
            {
                case GSOpcode.CM_START_OCCUPATION:
                    if (this.Remaining >= 4)
                    {
                        CityID = this.GetIntBE();
                    }
                    break;
                case GSOpcode.CM_CAPTURE_FLAG:
                    if (this.Remaining >= 13)
                    {
                        this.GetUIntBE();
                        CharacterID = this.GetUIntBE();
                        CityID = this.GetIntBE();
                        Flag = this.GetByte();
                    }
                    break;
                default:
                    if (this.Remaining >= 8)
                    {
                        CityID = this.GetIntBE();
                        CharacterID = this.GetUIntBE();
                    }
                    break;
            }
            ((UCGameSession)client).OnOccupation(this);
        }
    }
}
