using System.Collections.Generic;
using System.Linq;

namespace TitansUC.CmsServer.World
{
    /// <summary>
    /// A group chat ("G chat"). Only kept in memory, like on the Java server; it ends when its last
    /// member leaves. Java reference: model/chat/Channel.java.
    /// </summary>
    public class GroupChat
    {
        public GroupChat(uint id, uint created)
        {
            ID = id;
            Created = created;
            Members = new List<Member>();
        }

        public uint ID { get; private set; }

        public uint Created { get; private set; }

        /// <summary>
        /// In joining order. Only change it while holding the <see cref="CmsWorld"/> lock.
        /// </summary>
        public List<Member> Members { get; private set; }

        public bool Contains(uint characterID)
        {
            return Members.Any(m => m.ClientID == characterID);
        }
    }
}
