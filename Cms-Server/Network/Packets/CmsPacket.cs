using System.Collections.Generic;
using Common.Network.Packets;

namespace TitansUC.CmsServer.Network.Packets
{
    /// <summary>
    /// A CMS server packet, with the pieces many of them share.
    ///
    /// Answers to invitations and most team, friend and group chat notices use one 28 byte record:
    /// <code>
    /// uint16 BE   kind (0x1C team created, 0x1D joined team, 0x1E team invitation answer,
    ///             0x1F kicked from team, 0x20 left team, 0x29 group chat created, 0x2A joined group
    ///             chat, 0x2B group chat invitation answer, 0x2D left group chat, 0x30 friend added,
    ///             0x31 friend request answer, 0x32 friend deleted)
    /// uint16 BE   result (2 = yes / done, 1 = no)
    /// uint32 BE   team or group chat id (0 for friends)
    /// uint32 BE   creation time (Unix seconds) when something was created, else 0
    /// uint32 BE   character id
    /// 12 x 00
    /// </code>
    /// The layouts are from the official captures in UCGO Packet Logs.zip.
    /// </summary>
    public class CmsPacket : UCPacket<CMSOpcode>
    {
        public const int RecordSize = 28;

        public const ushort ResultYes = 2;
        public const ushort ResultNo = 1;

        public void PutRecord(ushort kind, ushort result, uint id, uint time, uint characterID)
        {
            this.PutUShortBE(kind);
            this.PutUShortBE(result);
            this.PutUIntBE(id);
            this.PutUIntBE(time);
            this.PutUIntBE(characterID);
            this.PutBytes(new byte[12]);
        }

        /// <summary>
        /// Reads a UC size and that many character ids.
        /// </summary>
        public static List<uint> ReadIDs(UCPacket<CMSOpcode> p)
        {
            var ids = new List<uint>();
            if (p.Remaining == 0)
            {
                return ids;
            }

            int count = p.GetSize();
            for (int i = 0; i < count && p.Remaining >= 4; i++)
            {
                ids.Add(p.GetUIntBE());
            }
            return ids;
        }

        public void PutIDs(ICollection<uint> ids)
        {
            this.PutSize(ids.Count);
            foreach (uint id in ids)
            {
                this.PutUIntBE(id);
            }
        }

        /// <summary>
        /// Reads the character id at offset 12 of a 28 byte record.
        /// </summary>
        public static uint ReadInvitee(UCPacket<CMSOpcode> p)
        {
            if (p.Remaining < 16)
            {
                return 0;
            }
            p.GetBytes(12);
            return p.GetUIntBE();
        }
    }
}
