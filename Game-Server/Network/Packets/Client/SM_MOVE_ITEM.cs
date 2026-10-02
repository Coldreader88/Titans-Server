using Common.Network.Packets;
using TitansUC.GameServer.World;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x8017: confirms a move (see <see cref="CM_MOVE_ITEM"/>).
    ///
    /// <code>
    /// uint32 BE   kind: 0x01010002 moved, 0x02010002 split into a new item, 0x03010002 merged into a stack,
    ///             0x04010002 part added to a stack, 0x01020002 got in a vehicle, 0x01030002 put it back
    /// uint32 BE   character id
    /// uint32 BE   item unique id, format
    /// uint32 BE   source container unique id, format (0, 0 for a split)
    /// uint32 BE   destination container unique id, format
    /// uint32 BE   target unique id, format, static id: 0, 0, item static id for a move; the stack or new
    ///             item for merge / add / split; 0, 0, -1 for a vehicle
    /// uint32 BE   source container static id (0 for a split), destination container static id
    /// uint32 BE   0
    /// uint32 BE   amount
    /// 2 bytes    the request's last two bytes
    /// UC size    then, for a split, the new item's description (as in 0x8016)
    /// </code>
    /// Java reference: MoveItem.write; checked against every 0x8017 in the official captures.
    /// </summary>
    public class SM_MOVE_ITEM : UCPacket<GSOpcode>
    {
        public const uint Moved = 0x01010002;
        public const uint Split = 0x02010002;
        public const uint Merged = 0x03010002;
        public const uint AddedToStack = 0x04010002;
        public const uint Rode = 0x01020002;
        public const uint PutBack = 0x01030002;

        /// <summary>
        /// The refusal: the success layout (63 bytes, the client does not parse less and then stays locked, see
        /// specs/re-refusal-replies.md) with 0x000C instead of 2 in the kind's low half; nothing moves.
        /// </summary>
        public static SM_MOVE_ITEM Refused(CM_MOVE_ITEM request)
        {
            uint kind = request.Section == CM_MOVE_ITEM.SectionRide ? Rode :
                request.Section == CM_MOVE_ITEM.SectionPutBack ? PutBack : Moved;
            return new SM_MOVE_ITEM((kind & 0xFFFF0000) | 0x000C, request, null, 0);
        }

        public SM_MOVE_ITEM(uint kind, CM_MOVE_ITEM request, ItemNode target, int itemStaticID)
        {
            this.ID = GSOpcode.SM_MOVE_ITEM;

            bool split = kind == Split;
            this.PutUIntBE(kind);
            this.PutUIntBE(request.CharacterID);
            this.PutUIntBE(request.ItemUniqueID);
            this.PutIntBE(request.ItemFormat);
            this.PutUIntBE(split ? 0 : request.SourceUniqueID);
            this.PutIntBE(split ? 0 : request.SourceFormat);
            this.PutUIntBE(request.DestUniqueID);
            this.PutIntBE(request.DestFormat);
            if (target != null)
            {
                target.WriteReference(this);
            }
            else
            {
                this.PutUIntBE(0);
                this.PutIntBE(0);
                this.PutIntBE(itemStaticID);
            }
            this.PutIntBE(split ? 0 : request.SourceStaticID);
            this.PutIntBE(request.DestStaticID);
            this.PutIntBE(0);
            this.PutIntBE(request.Amount);
            this.PutBytes(request.Tail);
            if (split)
            {
                var tail = target.BuildTail();
                this.PutSize(tail.Length);
                this.PutBytes(tail);
            }
            else
            {
                this.PutSize(0);
            }
        }
    }
}
