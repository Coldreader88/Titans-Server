using System.Collections.Generic;
using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x1B: equip or unequip the piloted vehicle's weapons and shield.
    ///
    /// <code>
    /// uint16 BE   2 = armaments (1 = "fitting apply", clothes, with an empty list)
    /// uint16 BE   0
    /// uint32 BE   character id
    /// 8 bytes    0
    /// UC size    entries, 12 bytes each: uint16 BE action (1 equip, 2 unequip), byte slot, byte 3,
    ///            uint32 BE item unique id, format (0, 0 when unequipping)
    /// </code>
    /// The reply (0x801B) is the request with 2 in bytes 2-3 (Equiped_RX-78_Shield.pcap and others).
    /// </summary>
    public class CM_EQUIP_ITEM : UCPacket<GSOpcode>
    {
        public const int Armaments = 2;
        public const int Equip = 1;
        public const int Unequip = 2;

        public CM_EQUIP_ITEM()
        {
            this.ID = GSOpcode.CM_EQUIP_ITEM;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_EQUIP_ITEM();
        }

        public byte[] Body { get; private set; }
        public int SubOp { get; private set; }
        public uint CharacterID { get; private set; }
        public List<EquipEntry> Entries { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            if (this.Remaining < 17)
            {
                return;
            }
            Body = this.GetBytes((ushort)this.Remaining);
            SubOp = Bytes.U16(Body, 0);
            CharacterID = Bytes.U32(Body, 4);
            Entries = new List<EquipEntry>();
            int p = 16;
            int count = 0, shift = 0;
            while (p < Body.Length)
            {
                byte b = Body[p++];
                count |= (b & 0x7F) << shift;
                shift += 7;
                if ((b & 0x80) != 0)
                {
                    break;
                }
            }
            for (int i = 0; i < count && p + 12 <= Body.Length; i++, p += 12)
            {
                Entries.Add(new EquipEntry
                {
                    Action = Bytes.U16(Body, p),
                    Slot = Body[p + 2],
                    ItemUniqueID = Bytes.U32(Body, p + 4),
                });
            }

            ((UCGameSession)client).OnEquipItem(this);
        }
    }

    public class EquipEntry
    {
        public int Action { get; set; }
        public int Slot { get; set; }
        public uint ItemUniqueID { get; set; }
    }
}
