using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace UCTool.Structures.Implementations
{
    [Serializable, XmlRoot("MasterExtentionFile")]
    public class MasterExtentionFile : UCStructures<MasterExtentionEntry>
    {

        public string SourceFileExtention { get; set; }

        public override void Read(System.IO.BinaryReader br)
        {
            try
            {
                BinaryReader ms = null;

                if (br != null && br.BaseStream.Length > 0)
                {
                    ms = new BinaryReader(new MemoryStream(br.ReadBytes((int)br.BaseStream.Length)));

                    br.Close();

                    this.Count = 1;

                    Console.WriteLine("Entry Count: {0}", this.Count);

                    this.Entries = new MasterExtentionEntry[this.Count];

                    for (int i = 0; i < this.Count; i++)
                    {
                        var entry = new MasterExtentionEntry();

                        entry.Read(ms);

                        this.Entries[i] = entry;

                    }


                }
                else
                {
                    throw new Exception("Error reading stream, it's null or empty.");
                }

                if (ms != null && ms.BaseStream.CanRead)
                {
                    var baseStrm = ms.BaseStream;
                    ms.Close();
                    baseStrm.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public override void Write(BinaryWriter bw)
        {
            try
            {

                if (bw != null && bw.BaseStream.CanWrite)
                {
                    foreach (var entry in Entries.ToList())
                    {
                        entry.Write(bw);
                    }

                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

    }
}
