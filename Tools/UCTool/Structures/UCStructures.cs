using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace UCTool.Structures
{
    [Serializable, XmlRoot("UCStructures")]
    public class UCStructures<T> where T: UCStructure, new()
    {
        public string SourceFile { get; set; }
        public ushort Count { get; set; } 
        public T[] Entries { get; set; }

        public UCStructures()
        {
            this.Count = 0;
            this.Entries = new T[0];
        }

        public virtual void Read(BinaryReader br)
        {
            try
            {
                if (br != null && br.BaseStream.Length > 0)
                {
                    this.Count = br.ReadUInt16();

                    Console.WriteLine("Entry Count: {0}", this.Count);

                    this.Entries = new T[this.Count];

                    for (int i = 0; i < this.Count; i++)
                    {
                        var entry = new UCStructure();

                        entry.Read(br);

                    }


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

                if (bw != null && bw.BaseStream.CanWrite)
                {
                    bw.Write(Count);

                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public void GenerateSourceFile(string name = "", string ext = "")
        {

            FileStream fs = null;
            BinaryWriter bw = null;

            try
            {

                if (name.Any())
                {
                    fs = new FileStream("NEW_" + name.ToUpper() + ext, FileMode.Create);
                }
                else
                {
                    fs = new FileStream("NEW_" + SourceFile + ext, FileMode.Create);
                }

                if (fs != null)
                {
                    bw = new BinaryWriter(fs);

                    this.Write(bw);

                    for (int i = 0; i < this.Count; i++)
                    {
                        Entries[i].Write(bw);
                    }

                    bw.Write((byte)0);

                    bw.BaseStream.SetLength(bw.BaseStream.Length / 2 + 1);

                    bw.Flush();
                    fs.Flush();
                    bw.Close();
                    fs.Close();

                }
                else
                {
                    throw new Exception("couldnt create an output stream for the file: " + this.GetType().Name);
                }


            }
            catch (Exception ex)
            {
                if (fs != null)
                {
                    if (bw != null)
                    {
                        bw.Flush();
                    }

                    fs.Flush();

                    if (bw != null)
                    {
                        bw.Close();
                    }

                    fs.Close();
                }

                Console.WriteLine(ex.Message);

                return;
            }
        }

        public void SerializeInstance()
        {
            try
            {
                if (Entries.Any())
                {
                    var fs = new FileStream(this.SourceFile.Any() ? this.SourceFile.ToUpper() + ".XML" : this.GetType().Name.ToUpper() + ".XML", FileMode.Create);
                    var sw = new StreamWriter(fs);

                    var serializer = new XmlSerializer(this.GetType(), new XmlRootAttribute(this.GetType().Name));

                    serializer.Serialize(sw, this);

                    sw.Flush();
                    fs.Flush();
                    sw.Close();
                    fs.Close();

                }
                else
                {
                    throw new Exception("There are no Enties to serialize!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

    }
}
