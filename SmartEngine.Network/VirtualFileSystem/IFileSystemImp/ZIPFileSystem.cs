using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmartEngine.Network.IO;
using SmartEngine.Core;

namespace SmartEngine.Network.VirtualFileSystem.IFileSystemImp
{
    public class ZIPFileSystem : IFileSystem
    {
        string rootPath = ".";
        #region IFileSystem Members

        public bool Init(string path)
        {
            if (path != "")
                rootPath = path + "/";
            else
                rootPath = "";
            return true;
        }

        public System.IO.Stream OpenFile(string path)
        {
            if (path.EndsWith(".xml"))
            {
                path = path.Replace(".xml", ".gz");
            }

            if (path.ToLower().EndsWith(".gz.gz"))
            {
                path.Replace(".gz.gz", ".gz"); if (!path.ToLower().EndsWith(".xml") && !path.ToLower().EndsWith(".gz"))
                {
                    path += ".xml";
                }
            }

            if (path.IndexOf(":") < 0)
            {
                return new System.IO.Compression.GZipStream(new System.IO.FileStream(rootPath + path, System.IO.FileMode.Open, System.IO.FileAccess.Read), CompressionMode.Decompress);
            }
            else
            {
                return new System.IO.Compression.GZipStream(new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read), CompressionMode.Decompress);
            }
        }

        public bool Exists(string path)
        {
            return System.IO.File.Exists(path);
        }

        public string[] SearchFile(string path, string pattern)
        {
            return System.IO.Directory.GetFiles(rootPath + path, pattern);
        }

        public string[] SearchFile(string path, string pattern, System.IO.SearchOption option)
        {
            return System.IO.Directory.GetFiles(rootPath + path, pattern, option);
        }

        public void Close()
        {
        }

        static MemoryStream Decompress(FileStream gzip)
        {
            // Create a GZIP stream with decompression mode.
            // ... Then create a buffer and write into while reading from the GZIP stream.
            using (GZipStream stream = new GZipStream(new BinaryReaderV2(gzip).BaseStream, CompressionMode.Decompress))
            {
                const int size = 4096;
                byte[] buffer = new byte[size];
                using (MemoryStream memory = new MemoryStream())
                {
                    int count = 0;
                    do
                    {
                        count = stream.Read(buffer, 0, size);
                        if (count > 0)
                        {
                            memory.Write(buffer, 0, count);
                        }
                    }
                    while (count > 0);

                    return memory;
                }
            }
        }

        #endregion
    }
}
