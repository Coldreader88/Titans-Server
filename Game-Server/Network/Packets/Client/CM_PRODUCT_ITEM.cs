using System.Collections.Generic;
using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x28: make something in the productive container (the factory), or take a vehicle in it apart;
    /// 0x29: the client's timer for it ran out.
    ///
    /// 0x28 (from the official captures of refining, weapon, shield, MS, MA, clothes and dismantling):
    /// <code>
    /// uint16 BE   action: 1 vehicle, 2 refine, 3 clothes, 4 weapon or shield, 5 dismantle, 8 vehicle upgrade
    /// uint16 BE   0 (the reply's result: 2 done, 0x0C failed)
    /// uint32 BE   character id
    /// uint32 BE   0x33 (0x04 for clothes), product category, product format (0x14 material, 0x10 weapon,
    ///             0x0A MS, 7 clothes)
    /// uint32 BE   product template, amount
    /// uint32 BE   productive container unique id, format 0x14, 7
    /// uint32 BE   time: -1 (the reply's start time, unix seconds)
    /// UC size     ingredients, 40 bytes each: uint32 BE state (7; the reply's 8 used up, 9 some of the stack
    ///             left), template, unique id, format, container unique id, container format, container static
    ///             id, 2 ints, amount
    /// byte        colour index (the client's UC_RequestProductItem _color_index): the colour slot picked in the dye
    ///             window for clothes (see ClothesColours), 0 otherwise, 0xFF when taking a vehicle apart or upgrading
    /// byte        improve level (1 for an upgrade), then uint32 BE unique id and format of the vehicle upgraded
    /// </code>
    /// The reply 0x8028 is the request with the result, the start time and the ingredient states filled in,
    /// then what came out: UC size, 12 bytes each (uint32 BE template, amount, 0), and a byte 0.
    /// A failure lists what it gave back. 0x29 names the productive container, product, start time and
    /// action; 0x8029 is it with 0x0002 in bytes 2-3.
    /// </summary>
    public class CM_PRODUCT_ITEM : UCPacket<GSOpcode>
    {
        public const int ActionVehicle = 1;
        public const int ActionRefine = 2;
        public const int ActionClothes = 3;
        public const int ActionWeapon = 4;
        public const int ActionDismantle = 5;
        public const int ActionUpgrade = 8;

        public const int IngredientSize = 40;
        public const int IngredientsOffset = 44;

        public CM_PRODUCT_ITEM(GSOpcode opcode)
        {
            this.ID = opcode;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_PRODUCT_ITEM(this.ID);
        }

        public byte[] Body { get; private set; }
        public int Action { get; private set; }
        public uint CharacterID { get; private set; }
        public int ProductID { get; private set; }
        public int Amount { get; private set; }
        public uint FactoryUniqueID { get; private set; }

        /// <summary>
        /// Where the ingredient list's count byte is; the ingredients follow it.
        /// </summary>
        public int ListOffset { get; private set; }

        /// <summary>
        /// The colour slot picked for clothes; -1 when the packet ends before it.
        /// </summary>
        public int ColourIndex { get; private set; }
        public List<Input> Inputs { get; private set; }

        public class Input
        {
            public int Offset;
            public int TemplateID;
            public uint UniqueID;
            public uint ContainerUniqueID;
            public int Amount;
        }

        public override void OnProcess(Session<GSOpcode> client)
        {
            var session = (UCGameSession)client;
            if (this.ID == GSOpcode.CM_PRODUCT_DONE)
            {
                if (this.Remaining < 8)
                {
                    return;
                }
                Body = this.GetBytes((ushort)this.Remaining);
                session.OnProductDone(this);
                return;
            }

            if (this.Remaining < IngredientsOffset + 1)
            {
                return;
            }
            Body = this.GetBytes((ushort)this.Remaining);
            Action = Bytes.U16(Body, 0);
            CharacterID = Bytes.U32(Body, 4);
            ProductID = (int)Bytes.U32(Body, 20);
            Amount = (int)Bytes.U32(Body, 24);
            FactoryUniqueID = Bytes.U32(Body, 28);

            // The count is a UC size; the client sends at most a handful, so it is one byte.
            ListOffset = IngredientsOffset;
            int count = Body[ListOffset] & 0x7F;
            Inputs = new List<Input>();
            for (int i = 0; i < count; i++)
            {
                int o = ListOffset + 1 + i * IngredientSize;
                if (o + IngredientSize > Body.Length)
                {
                    return;
                }
                Inputs.Add(new Input
                {
                    Offset = o,
                    TemplateID = (int)Bytes.U32(Body, o + 4),
                    UniqueID = Bytes.U32(Body, o + 8),
                    ContainerUniqueID = Bytes.U32(Body, o + 16),
                    Amount = (int)Bytes.U32(Body, o + 36),
                });
            }
            int tail = ListOffset + 1 + count * IngredientSize;
            ColourIndex = tail < Body.Length ? Body[tail] : -1;
            session.OnProductItem(this);
        }
    }

    /// <summary>
    /// 0x8028: see <see cref="CM_PRODUCT_ITEM"/>.
    /// </summary>
    public class SM_PRODUCT_ITEM : UCPacket<GSOpcode>
    {
        public const int Done = 2;
        public const int Failed = 0x0C;

        public const uint StateUsedUp = 8;
        public const uint StatePartlyUsed = 9;

        /// <param name="states">The new state of each ingredient, in request order.</param>
        /// <param name="output">What came out, or what a failure gave back: template and amount.</param>
        public SM_PRODUCT_ITEM(CM_PRODUCT_ITEM request, int result, uint startTime, IList<uint> states,
            IList<KeyValuePair<int, int>> output)
        {
            this.ID = GSOpcode.SM_PRODUCT_ITEM;

            var body = (byte[])request.Body.Clone();
            body[2] = (byte)(result >> 8);
            body[3] = (byte)result;
            Bytes.PutU32(body, 40, startTime);
            for (int i = 0; i < request.Inputs.Count && i < states.Count; i++)
            {
                Bytes.PutU32(body, request.Inputs[i].Offset, states[i]);
            }
            this.PutBytes(body);
            this.PutSize(output.Count);
            foreach (var o in output)
            {
                this.PutIntBE(o.Key);
                this.PutIntBE(o.Value);
                this.PutIntBE(0);
            }
            this.PutByte(0);
        }
    }
}
