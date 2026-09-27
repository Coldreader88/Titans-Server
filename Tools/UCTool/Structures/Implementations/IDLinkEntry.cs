using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace UCTool.Structures.Implementations
{
    [Serializable, XmlRoot("IDLinkEntry")]
    public class IDLinkEntry : UCStructure
    {

        public String ResourceFile;
        public ushort EntityType = 0;
        public uint FOV = 0;
        public string PreviewModel, PreviewColors, PreviewAnimation;

        public IDLinkEntry() : base()
        {

        }

        public override void Read(System.IO.BinaryReader br)
        {
            base.Read(br);

            try
            {
                this.ResourceFile = this.ReadUString(br);
                this.EntityType = br.ReadUInt16();
                this.FOV = br.ReadUInt32();
                this.PreviewModel = this.ReadUString(br);
                this.PreviewColors = this.ReadUString(br);

                if (this.EntityType > 0)
                {
                    this.PreviewAnimation = this.ReadUString(br);
                }
                else
                {
                    this.PreviewAnimation = br.ReadUInt16().ToString();
                }
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

                this.WriteUString(this.ResourceFile, bw);
                bw.Write(EntityType);
                bw.Write(FOV);
                this.WriteUString(this.PreviewModel, bw);
                this.WriteUString(this.PreviewColors, bw);

                if (this.EntityType > 0)
                {
                    this.WriteFixedUString(this.PreviewAnimation, bw);
                }
                else
                {
                    bw.Write((ushort)0);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

    }
}
