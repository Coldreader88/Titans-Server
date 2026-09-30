using System.Collections.Generic;
using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x1B: equip or unequip the piloted vehicle's weapons and shield (section 2), or change clothes
    /// (section 1).
    ///
    /// <code>
    /// uint16 BE   section: 2 = armaments, 1 = clothes ("fitting apply"; an empty list when nothing changed)
    /// uint16 BE   0
    /// uint32 BE   character id
    /// 8 bytes    0
    /// UC size    entries, 12 bytes each: uint16 BE action (1 equip, 2 unequip), byte slot, byte 3,
    ///            uint32 BE item unique id, format (0, 0 when unequipping)
    /// </code>
    /// The reply (0x801B) is the request with 2 in bytes 2-3 (Equiped_RX-78_Shield.pcap and others).
    ///
    /// Clothes entries (the client's RequestDressUp, uc.exe 0x7c6509) use the same 12 bytes with byte 3 = 0: the
    /// weared slot 1-8 and action 1 = wear into the empty slot, 2 = take off (item id 0), 3 = swap for the item.
    /// The client changes nothing until the reply; its NotifyEquipItem (0x7cfd50) needs code 2, then moves the
    /// items itself (taken off clothes go into the backpack) and redraws the character.
    /// </summary>
    public class CM_EQUIP_ITEM : UCPacket<GSOpcode>
    {
        public const int Clothes = 1;
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
