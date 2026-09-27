using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UCTool.Structures;
using UCTool.Structures.Implementations;
using System.Xml.Serialization;

namespace UCTool
{
    class Program
    {
        private static string FileName, opType;

        static void Main(string[] args)
        {

            try
            {

                Console.WriteLine("UCTool Created by Dante.\n");

                if (args.Count() > 0)
                {
                    if (args[0].ToLower() == "-o")
                    {
                        Parse(args[1]);
                    }
                    else if (args[0].ToLower() == "-g")
                    {
                        DeserializeAndGenerate(args[1]);
                    }
                    else
                    {
                        Console.WriteLine("Invalid argument, valid arguements are [-o 'opens a file and outputs and xml for review/edits]\n" + 
                                          "[-g 'opens an xml file that was generated and generates a new source file']");
                    }
                }
                else
                {
                    Console.WriteLine("Options: [o: open a file and output an editable xml], [g: open an editable xml file and output it to a UCGO file.] ");
                    
                    Console.Write("Enter an Operation: ");
                    opType = Console.ReadLine().ToLower();

                   if (opType == "o")
                   {
                       Console.WriteLine("Operation is set to read a UCGO file.\n");
                   }else if (opType == "g")
                   {
                       Console.WriteLine("Operation is set to read an XML file and convert to to a UCGO file.\n");
                   }
                   else
                   {
                       throw new Exception(opType != null ? opType.Any() ? "Operation: " + opType + " is not supported." : "You must enter an operation type [o, g]." : "You must enter an operation type [o, g].");
                   }

                    Console.Write("Enter a File: ");
                    FileName = Console.ReadLine().ToLower();

                    if (FileName != null)
                    {
                        if (FileName.Any())
                        {
                            Console.WriteLine();
                            if (opType == "o")
                            {
                                Parse(FileName);
                            }
                            else if (opType == "g")
                            {
                                DeserializeAndGenerate(FileName);
                            }
                        }
                        else
                        {
                            throw new Exception("You must enter a file to open.");
                        }
                    }
                    else
                    {
                        throw new Exception("You must enter a file to open.");
                    }

                }

                Console.Write("Done..Press Enter to Exit.");

                Console.ReadLine();

            }
            catch (Exception ex)
            {
                
                Console.WriteLine(ex.Message);
            }

        }

        static void Parse(string fileName)
        {
            
            try
            {
                string fn = fileName.ToLower();

                if (fn.EndsWith(".elt"))
                {
                    string fileType = fn.Replace(".elt", "");

                    FileStream fs = new FileStream(fileName, FileMode.Open);

                    switch (fileType)
                    {
                        case "itemdata":
                            {
                                var file = new ItemData();

                                file.SourceFile = fileName.ToUpper();

                                file.Read(new BinaryReader(fs));

                                file.SerializeInstance();

                                file.GenerateSourceFile();

                                break;
                            }

                        default:
                            {
                                Console.WriteLine("No Handling for File: " + fileName);
                                break;
                            }
                    }

                }
                else if (fn.EndsWith(".bin"))
                {
                    string fileType = fn.Replace(".bin", "");

                    FileStream fs = new FileStream(fileName, FileMode.Open);

                    switch (fileType)
                    {
                        case "idlink":
                            {
                                var file = new IDLink();

                                file.SourceFile = fileName.ToUpper();

                                file.Read(new BinaryReader(fs));

                                file.SerializeInstance();

                                break;
                            }

                        default:
                            {
                                Console.WriteLine("No Handling for File: " + fileName);
                                break;
                            }
                    }

                }
                else if (fn.EndsWith(".mef"))
                {
                    string fileType = fn;

                    FileStream fs = new FileStream(fileName, FileMode.Open);

                    switch (fileType.EndsWith(".mef"))
                    {
                        case true:
                            {
                                var file = new MasterExtentionFile();

                                file.SourceFile = fileName.Replace(".mef", "_MEF").ToUpper();
                                file.SourceFileExtention = ".MEF";

                                file.Read(new BinaryReader(fs));

                                file.SerializeInstance();

                                break;
                            }

                        case false:
                        default:
                            {
                                Console.WriteLine("No Handling for File: " + fileName);
                                break;
                            }
                    }

                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }

        static UCStructures<UCStructure> Deserialize(StreamReader sw)
        {

            try
            {
                //UCStructures<UCStructure> structures = null;

                XmlSerializer serializer = new XmlSerializer(typeof(UCStructures<UCStructure>));

                //sw.Close();

                return (UCStructures<UCStructure>)serializer.Deserialize(sw);

            }
            catch (Exception ex)
            {
                
                Console.WriteLine(ex);

                //sw.Close();

                return null;
            }

        }

        static T Deserialize<T>(StreamReader sw) where T: new()
        {

            try
            {
                //UCStructures<UCStructure> structures = null;

                XmlSerializer serializer = new XmlSerializer(typeof(T));

                //sw.Close();

                return (T)serializer.Deserialize(sw);

            }
            catch (Exception ex)
            {

                Console.WriteLine(ex);

                //sw.Close();

                return new T();
            }

        }

        static void DeserializeAndGenerate(string fileName)
        {

            StreamReader sw = null;

            try
            {

                var fName = fileName.ToLower();

                if (fName.EndsWith(".xml"))
                {
                    fName = fName.Replace(".xml", "");

                    switch (fName)
                    {
                        case "itemdata":
                            {
                                sw = new StreamReader(fileName);

                                var data = Deserialize<ItemData>(sw);

                                data.GenerateSourceFile();

                                break;
                            }

                        case "idlink":
                            {
                                sw = new StreamReader(fileName);

                                var data = Deserialize<IDLink>(sw);

                                data.GenerateSourceFile();

                                break;
                            }

                        default:
                            {
                                if (fName.ToLower().Contains("_mef"))
                                {
                                    sw = new StreamReader(fileName);

                                    var data = Deserialize<MasterExtentionFile>(sw);

                                    data.SourceFile = data.SourceFile.ToUpper().Replace("_MEF", "");

                                    data.GenerateSourceFile("", data.SourceFileExtention);
                                }
                                else
                                {
                                    Console.WriteLine("No Handling for file type: " + fileName);
                                }
                                break;
                            }
                    }

                }


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }


            if (sw != null)
            {
                sw.Close();
            }

        }


    }
}
