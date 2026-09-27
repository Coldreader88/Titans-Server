using Common.Network.Packets;

namespace TitansUC.LobbyServer.Network.Packets.Client
{
    /// <summary>
    /// 0x38004: the character was deleted.
    ///
    /// <code>
    /// uint32 BE   0x00050002
    /// 24 x 00
    /// </code>
    /// The client then asks for the character list again. Follows the official reply in
    /// "char delete.txt" (UCGO Packet Logs.zip); the Java server (NotifyCharacterDeletion.java) sent
    /// 0x00050002, 00, 0x18 instead.
    /// </summary>
    public class SM_DELETE_CHARACTER : UCPacket<LSOpcode>
    {
        public SM_DELETE_CHARACTER()
        {
            this.ID = LSOpcode.SM_DELETE_CHARACTER;

            this.PutUIntBE(0x00050002);
            this.PutBytes(new byte[24]);
        }
    }
}
