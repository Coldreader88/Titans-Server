using System.Collections.Generic;
using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// The Status Setting window's Apply: the arrows the player changed.
    /// <code>
    /// 0x0B ChangeSkillManagement:  uint32 BE character id; UC size n, n x byte table; UC size n, n x byte index;
    ///                              UC size n, n x byte arrow
    /// 0x0C ChangeStatusManagement: uint32 BE character id; UC size n, n x byte status (0 strength, 1 spirit, 2 luck);
    ///                              UC size n, n x byte arrow
    /// </code>
    /// Answered with 0x800B / 0x800C (UC_ResultMsg); on code 2 the client keeps the new arrows, otherwise it
    /// sends them again next time. Layouts from the client (UC_ChangeSkillManagement, UC_ChangeStatusManagement);
    /// no capture has them.
    /// </summary>
    public class CM_CHANGE_MANAGEMENT : UCPacket<GSOpcode>
    {
        private readonly GSOpcode opcode;

        public CM_CHANGE_MANAGEMENT(GSOpcode opcode)
        {
            this.opcode = opcode;
            this.ID = opcode;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_CHANGE_MANAGEMENT(opcode);
        }

        public bool Statuses { get { return opcode == GSOpcode.CM_CHANGE_STATUS_MANAGEMENT; } }
        public uint CharacterID { get; private set; }

        /// <summary>
        /// (table, index, arrow) per change; the table is 0xFF for statuses. Null when the lists do not match.
        /// </summary>
        public List<byte[]> Changes { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            Changes = null;
            try
            {
                CharacterID = this.GetUIntBE();
                byte[] types = Statuses ? null : ReadList();
                byte[] indexes = ReadList();
                byte[] arrows = ReadList();
                if (indexes.Length == arrows.Length && (types == null || types.Length == indexes.Length))
                {
                    Changes = new List<byte[]>();
                    for (int i = 0; i < indexes.Length; i++)
                    {
                        Changes.Add(new[] { types != null ? types[i] : (byte)0xFF, indexes[i], arrows[i] });
                    }
                }
            }
            catch (System.Exception)
            {
                Changes = null;
            }
            ((UCGameSession)client).OnChangeManagement(this);
        }

        private byte[] ReadList()
        {
            int n = this.GetSize();
            var list = new byte[n];
            for (int i = 0; i < n; i++)
            {
                list[i] = this.GetByte();
            }
            return list;
        }
    }
}
