using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.LobbyServer.Network.Client;

namespace TitansUC.LobbyServer.Network.Packets.Client
{
    /// <summary>
    /// 0x30003: create a character.
    ///
    /// <code>
    /// uint32 BE   account id
    /// 78 bytes    appearance: gender at 14, faction at 16, face at 37, hair style at 39,
    ///             skin at 66, hair colour at 68; the rest is not used
    /// UC string   name (UTF-16LE), followed by one 00 byte
    /// byte        starting city (0 = Sydney, see Common.Characters.City)
    /// uint32 BE   -1
    /// 0x83        strength, spirit, luck (uint32 BE each), then their sum
    /// </code>
    /// Java reference: mina_loginserver RequestCharacterCreation.java and BufferCharacterAppearance.java;
    /// checked against the official capture UCGOCharCreation.pcap.
    /// </summary>
    public class CM_REQUEST_CREATE_CHARACTER : UCPacket<LSOpcode>
    {
        public const int AppearanceSize = 78;

        public CM_REQUEST_CREATE_CHARACTER()
        {
            this.ID = LSOpcode.CM_REQUEST_CREATE_CHARACTER;
        }

        public override Packet<LSOpcode> New()
        {
            return new CM_REQUEST_CREATE_CHARACTER();
        }

        public uint AccountID { get; private set; }
        public byte Gender { get; private set; }
        public byte Faction { get; private set; }
        public byte Face { get; private set; }
        public byte HairStyle { get; private set; }
        public byte Skin { get; private set; }
        public byte HairColor { get; private set; }
        public string Name { get; private set; }
        public byte City { get; private set; }
        public int Strength { get; private set; }
        public int Spirit { get; private set; }
        public int Luck { get; private set; }

        public override void OnProcess(Session<LSOpcode> client)
        {
            Read();

            ((UCLobbySession)client).OnRequestCreateCharacter(this);
        }

        public void Read()
        {
            AccountID = this.GetUIntBE();

            var appearance = this.GetBytes(AppearanceSize);
            Gender = appearance[14];
            Faction = appearance[16];
            Face = appearance[37];
            HairStyle = appearance[39];
            Skin = appearance[66];
            HairColor = appearance[68];

            Name = this.GetUCString().TrimEnd('\0');
            this.GetByte();

            City = this.GetByte();
            this.GetUIntBE();

            this.GetSize();
            Strength = this.GetIntBE();
            Spirit = this.GetIntBE();
            Luck = this.GetIntBE();
        }
    }
}
