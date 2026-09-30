using System.Collections.Generic;
using Common.Network.Packets;
using TitansUC.GameServer.World;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x803E NotifyCompleteQuest: the answer to 0x3E, in the same struct (UC_ReqBGNpcQuest, reader 0x7ab760).
    ///
    /// <code>
    /// uint16 BE   section (1; any other and the client applies nothing)
    /// uint16 BE   code: 2 done; 0x30 no room for the reward, 0x16 cannot go there, anything else "Quest Error."
    /// uint32 BE   character id (another id: "not my quest")
    /// uint32 BE   quest id
    /// int64 BE    money added (a change, not the new total; only when above 0)
    /// uint32 BE   reward container unique id, format (0 = no reward item)
    /// reward item: uint32 BE unique id, format, uint32 BE template, int64 BE count, uint32 BE state
    /// UC size     items, 28 bytes each: uint32 BE unique id, format, int64 BE count change,
    ///             uint32 BE container unique id, format, uint32 BE state (8 = gone, 9 = count changed)
    /// then, for a reward item the client does not have yet, its item description (as in 0x8016)
    /// </code>
    /// From the client: NotifyCompleteQuest 0x7cf280 applies the items, the money and the reward; the game
    /// handler 0x43fb3c shows the NPC's thanks and closes the hand-in window on code 2. No capture has it.
    /// </summary>
    public class SM_COMPLETE_QUEST : UCPacket<GSOpcode>
    {
        public const ushort Done = 2;
        public const ushort NoRoom = 0x30;
        public const ushort Error = 0x0C;

        public const uint StateGone = 8;
        public const uint StateChanged = 9;

        public SM_COMPLETE_QUEST(uint characterID, int questID, ushort code, int money = 0, ItemNode rewardContainer = null,
            ItemNode reward = null, IList<QuestUse> used = null)
        {
            this.ID = GSOpcode.SM_COMPLETE_QUEST;

            this.PutUShortBE(1);
            this.PutUShortBE(code);
            this.PutUIntBE(characterID);
            this.PutIntBE(questID);
            PutLong(money);
            if (reward != null && rewardContainer != null)
            {
                this.PutUIntBE(rewardContainer.UniqueID);
                this.PutIntBE(rewardContainer.Format);
                this.PutUIntBE(reward.UniqueID);
                this.PutIntBE(reward.Format);
                this.PutIntBE(reward.StaticID);
                PutLong(reward.Amount);
                this.PutUIntBE(0);
            }
            else
            {
                this.PutUIntBE(0);
                this.PutUIntBE(0);
                this.PutUIntBE(0);
                this.PutUIntBE(0);
                this.PutUIntBE(0xFFFFFFFF);
                PutLong(0);
                this.PutUIntBE(0);
            }
            this.PutSize(used != null ? used.Count : 0);
            if (used != null)
            {
                foreach (var u in used)
                {
                    this.PutUIntBE(u.Item.UniqueID);
                    this.PutIntBE(u.Item.Format);
                    PutLong(u.Gone ? 0 : -u.Taken);
                    this.PutUIntBE(0);
                    this.PutUIntBE(0);
                    this.PutUIntBE(u.Gone ? StateGone : StateChanged);
                }
            }
            if (reward != null && rewardContainer != null)
            {
                this.PutBytes(reward.BuildTail());
            }
        }

        private void PutLong(long value)
        {
            this.PutIntBE((int)(value >> 32));
            this.PutIntBE((int)value);
        }
    }
}
