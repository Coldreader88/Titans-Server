using System.Collections.Generic;
using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x3E CompleteQuest: the player hands an NPC the items of a quest (UC_ReqBGNpcQuest).
    ///
    /// <code>
    /// uint16 BE   section (1)
    /// uint16 BE   code (0)
    /// uint32 BE   character id
    /// uint32 BE   quest id (QUESTLIST)
    /// int64 BE    price (0)
    /// uint32 BE   reward container unique id, format (0)
    /// reward item, 24 bytes: uint32 BE unique id, format, uint32 BE template (FFFFFFFF), int64 BE count, uint32 BE state
    /// UC size     items, 28 bytes each: uint32 BE unique id, format, uint64 BE count (0),
    ///             uint32 BE container unique id, format, uint32 BE state (7)
    /// </code>
    /// No capture has it; the layout is the client's (ctor 0x7ab294, writer 0x7ab5e8, sent from the hand-in window
    /// 0x57df5c via 0x7bd1e8). The client lists one item per required entry its slots match and sends the request
    /// even when they do not cover the quest; it waits for 0x803E before it allows another hand-in.
    /// </summary>
    public class CM_COMPLETE_QUEST : UCPacket<GSOpcode>
    {
        public const int FixedLength = 52;
        public const int ItemLength = 28;

        public CM_COMPLETE_QUEST()
        {
            this.ID = GSOpcode.CM_COMPLETE_QUEST;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_COMPLETE_QUEST();
        }

        public bool Valid { get; private set; }
        public uint CharacterID { get; private set; }
        public int QuestID { get; private set; }
        public List<uint> Items { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            Items = new List<uint>();
            var body = this.Remaining > 0 ? this.GetBytes((ushort)this.Remaining) : new byte[0];
            if (body.Length > FixedLength)
            {
                CharacterID = Bytes.U32(body, 4);
                QuestID = (int)Bytes.U32(body, 8);
                int p = FixedLength;
                byte f = body[p++];
                int count = f & 0x7F;
                if ((f & 0x80) == 0 && p < body.Length)
                {
                    count = (body[p++] & 0x7F) * 0x80 + f;
                }
                for (int i = 0; i < count && p + ItemLength <= body.Length; i++, p += ItemLength)
                {
                    Items.Add(Bytes.U32(body, p));
                }
                Valid = true;
            }
            ((UCGameSession)client).OnCompleteQuest(this);
        }
    }
}
