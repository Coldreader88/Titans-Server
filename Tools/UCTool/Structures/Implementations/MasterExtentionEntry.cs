using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace UCTool.Structures.Implementations
{
    [Serializable, XmlRoot("MasterExtentionEntry")]
    public class MasterExtentionEntry : UCStructure
    {

        public string Version,
                      Alias,
                      Model;

        public uint UnknownIntA, UnknownIntB;

        public string BaseModel,
                      LodResource,
                      MaterialResource,
                      MotionLinkResource,
                      ThrusterResource,
                      SoundResource;

        public float CollisionRadius = 0;

        public byte[] UnknownBytes = null;

        public MasterExtentionEntry() : base()
        {
            this.ID = 0;
        }

        public override void Read(System.IO.BinaryReader br)
        {
            try
            {
                this.Version = this.ReadFixedUString(br, 8);
                this.Alias = this.ReadUString(br, true);
                this.Model = this.ReadUString(br, true);
                this.UnknownIntA = br.ReadUInt32();
                this.UnknownIntA = br.ReadUInt32();
                this.BaseModel = this.ReadUString(br, true);
                this.LodResource = this.ReadUString(br, true);
                this.MaterialResource = this.ReadUString(br, true);
                this.MotionLinkResource = this.ReadUString(br, true);
                this.ThrusterResource = this.ReadUString(br, true);
                this.SoundResource = this.ReadUString(br, true);

                this.CollisionRadius = br.ReadSingle();

                this.UnknownBytes = br.ReadBytes(Convert.ToInt32(br.BaseStream.Length - br.BaseStream.Position));


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
                /*
                this.Version = this.ReadFixedUString(br, 8);
                this.Alias = this.ReadUString(br, true);
                this.Model = this.ReadUString(br, true);
                this.UnknownIntA = br.ReadUInt32();
                this.UnknownIntA = br.ReadUInt32();
                this.BaseModel = this.ReadUString(br, true);
                this.LodResource = this.ReadUString(br, true);
                this.MaterialResource = this.ReadUString(br, true);
                this.MotionLinkResource = this.ReadUString(br, true);
                this.ThrusterResource = this.ReadUString(br, true);
                this.SoundResource = this.ReadUString(br, true);

                this.CollisionRadius = br.ReadSingle();
                 */

                this.WriteFixedUString(this.Version, bw);
                this.WriteUString(this.Alias, bw, true);
                this.WriteUString(this.Model, bw, true);
                bw.Write(this.UnknownIntA);
                bw.Write(this.UnknownIntB);
                this.WriteUString(this.BaseModel, bw, true);
                this.WriteUString(this.LodResource, bw, true);
                this.WriteUString(this.MaterialResource, bw, true);
                this.WriteUString(this.MotionLinkResource, bw, true);
                this.WriteUString(this.ThrusterResource, bw, true);
                this.WriteUString(this.SoundResource, bw, true);

                bw.Write(this.CollisionRadius);

                bw.Write(this.UnknownBytes, 0, this.UnknownBytes.Count() - 1);

                bw.BaseStream.SetLength(bw.BaseStream.Length);


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

    }
}
