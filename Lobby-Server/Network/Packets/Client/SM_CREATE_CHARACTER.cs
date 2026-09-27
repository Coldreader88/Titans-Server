using Common.Network.Packets;

namespace TitansUC.LobbyServer.Network.Packets.Client
{
    /// <summary>
    /// 0x38003: the character was created.
    ///
    /// <code>
    /// uint32 BE   0x00040002
    /// 8 x 00
    /// uint32 BE   new character id
    /// 12 x 00
    /// </code>
    /// The client then asks for the character list again. No failure reply is known: the Java server
    /// sends nothing when creation fails.
    /// Java reference: mina_loginserver NotifyCharacterCreation.java; matches the official reply in
    /// UCGOCharCreation.pcap.
    /// </summary>
    public class SM_CREATE_CHARACTER : UCPacket<LSOpcode>
    {
        public SM_CREATE_CHARACTER(uint characterID)
        {
            this.ID = LSOpcode.SM_CREATE_CHARACTER;

            this.PutUIntBE(0x00040002);
            this.PutBytes(new byte[8]);
            this.PutUIntBE(characterID);
            this.PutBytes(new byte[12]);
        }
    }
}
