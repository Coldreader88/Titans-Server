using Common.Network.Packets;
using Common.Characters;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x806F: a player's profile window.
    ///
    /// <code>
    /// uint32 BE   character id
    /// uint16 BE   faction
    /// byte       rank
    /// UC string   name
    /// uint32 BE x 6 score: enemy NPC kills, friendly NPC kills, enemy player kills, friendly player kills,
    ///            criminal count, previous offense (the client's UC_PaperDollInfo; "Light" in
    ///            Open_Other_Player_Stat_Window.pcap had previous offense 3)
    /// int32 BE -1, int32 BE -1, 3 x FF (no vehicle)
    /// 0x82 medal points (Richmond, Newman), character id
    /// uint16 BE   9
    /// byte       gender
    /// looks (see <see cref="CharacterLooks"/>)
    /// </code>
    /// Java reference: PaperDoll.java, PlayerScoreWriter.java, PlayerMedalWriter.java. Score order from the client.
    /// </summary>
    public class SM_PAPER_DOLL_INFO : UCPacket<GSOpcode>
    {
        public SM_PAPER_DOLL_INFO(Character character)
        {
            this.ID = GSOpcode.SM_PAPER_DOLL_INFO;

            this.PutUIntBE(character.ClientID);
            this.PutUShortBE((ushort)character.Faction);
            this.PutByte((byte)character.Rank);
            this.PutUCString(character.Name);

            foreach (int slot in new[] { ScoreSlot.EnemyNpcKills, ScoreSlot.FriendlyNpcKills, ScoreSlot.EnemyPlayerKills,
                ScoreSlot.FriendlyPlayerKills, ScoreSlot.CriminalCount, ScoreSlot.PreviousOffense })
            {
                this.PutIntBE(character.Scores[slot]);
            }

            this.PutIntBE(-1);
            this.PutIntBE(-1);
            this.PutBytes(0xFF, 0xFF, 0xFF);

            this.PutSize(character.Medals.Length);
            foreach (int medal in character.Medals)
            {
                this.PutIntBE(medal);
            }
            this.PutUIntBE(character.ClientID);

            this.PutUShortBE(0x0009);
            this.PutByte((byte)character.Gender);
            CharacterLooks.Write(this, character);
        }
    }
}
