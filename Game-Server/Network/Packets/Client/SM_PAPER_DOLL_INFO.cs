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
    /// uint32 BE x 6 score: NPC wins, NPC losses, player wins, player losses, penalty, previous offense
    /// int32 BE -1, int32 BE -1, 3 x FF (no vehicle)
    /// 0x82 medals: uint32 BE 0, uint32 BE 0, character id
    /// uint16 BE   9
    /// byte       gender
    /// looks (see <see cref="CharacterLooks"/>)
    /// </code>
    /// Java reference: PaperDoll.java, PlayerScoreWriter.java, PlayerMedalWriter.java. Not checked against a capture.
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

            this.PutIntBE(0);
            this.PutIntBE(0);
            this.PutIntBE(character.Score);
            this.PutIntBE(character.Lost);
            this.PutIntBE(0);
            this.PutIntBE(0);

            this.PutIntBE(-1);
            this.PutIntBE(-1);
            this.PutBytes(0xFF, 0xFF, 0xFF);

            this.PutSize(2);
            this.PutIntBE(0);
            this.PutIntBE(0);
            this.PutUIntBE(character.ClientID);

            this.PutUShortBE(0x0009);
            this.PutByte((byte)character.Gender);
            CharacterLooks.Write(this, character);
        }
    }
}
