using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace UCTool.Structures.Implementations
{
    [Serializable, XmlRoot("ItemEntry")]
    public class ItemEntry : UCStructure
    {

        public string Comment { get; set; }
        public string MeshRoot { get; set; }
        public string Reference { get; set; }
        public string SubReference { get; set; }
        public UInt16 TypeA { get; set; }
        public UInt16 TypeB { get; set; }
        public byte TypeC { get; set; }

        public ItemEntry() : base()
        {
            this.Comment = "";
            this.MeshRoot = "";
            this.Reference = "";
            this.SubReference = "";
            this.TypeA = 0xFFFF;
            this.TypeB = 0xFFFF;
            this.TypeC = 0x00;
        }

        public override void Read(System.IO.BinaryReader br)
        {
            base.Read(br);

            try
            {
                this.Comment = this.ReadUString(br);
                this.MeshRoot = this.ReadUString(br);
                this.Reference = this.ReadUString(br);
                this.SubReference = this.ReadUString(br);
                this.TypeA = br.ReadUInt16();
                this.TypeB = br.ReadUInt16();
                this.TypeC = (byte)br.ReadByte();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public override void Write(System.IO.BinaryWriter bw)
        {

            try
            {
                base.Write(bw);

                this.WriteUString(this.Comment, bw);
                this.WriteUString(this.MeshRoot, bw);
                this.WriteUString(this.Reference, bw);
                this.WriteUString(this.SubReference, bw);
                bw.Write(this.TypeA);
                bw.Write(this.TypeB);
                bw.Write(this.TypeC);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

    }
}
