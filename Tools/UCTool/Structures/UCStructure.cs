using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace UCTool.Structures
{
    [Serializable, XmlRoot("UCStructure")]
    public class UCStructure
    {
        public uint ID { get; set; }

        public UCStructure()
        {
            this.ID = 0xDEADBABE;
        }

        public virtual void Read(BinaryReader br)
        {
            try
            {
                if (br != null && br.BaseStream.Length > 0)
                {
                    this.ID = br.ReadUInt32();

                    Console.WriteLine("Entry ID: {0}", this.ID);

                }
                else
                {
                    throw new Exception("Error reading stream, it's null or empty.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public virtual void Write(BinaryWriter bw)
        {

            try
            {
                bw.Write(this.ID);
            }
            catch (Exception ex)
            {
                
                Console.WriteLine(ex);
            }

        }

        protected virtual String ReadUString(BinaryReader br, bool bIntSize = false)
        {
            int size = bIntSize ? br.ReadInt32() : br.ReadUInt16();
            byte[] str_buff = br.ReadBytes(size * 2);

            string obj_str = Encoding.Unicode.GetString(str_buff);


            return obj_str;
        }

        protected virtual String ReadFixedUString(BinaryReader br, int size)
        {
            byte[] str_buff = br.ReadBytes(size * 2);

            string obj_str = Encoding.Unicode.GetString(str_buff);


            return obj_str;
        }

        protected virtual void WriteUString(String str, BinaryWriter bw, bool bIntSize = false)
        {
            var len = bIntSize ? str.Length : (ushort) str.Length;
            bw.Write(len);
            bw.Write(Encoding.Unicode.GetBytes(str));
        }

        protected virtual void WriteFixedUString(String str, BinaryWriter bw)
        {
            bw.Write(Encoding.Unicode.GetBytes(str));
        }
    }
}
