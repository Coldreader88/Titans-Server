using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Xml;
using System.Diagnostics;

using SmartEngine.Network.VirtualFileSystem;
using SmartEngine.Network.IO;
using SmartEngine.Core;
using SmartEngine.Network.Utils.FactoryDataTypes;

namespace SmartEngine.Network.Utils
{
    public abstract class DuplicateFactory<K, T, A, B> : Factory<K, T>
        where K : new()
        where T : TTItem<A, B>, new()
    {

        protected string Path { get; set; }

        public DuplicateFactory()
            : base()
        {
        }

        protected override int InitXML(string path, Encoding encoding)
        {

            if (!path.ToLower().EndsWith(".xml") && !path.ToLower().EndsWith(".gz"))
            {
                path += ".xml";
            }

            Path = path;

            XmlDocument xml = new XmlDocument();
            int count = 0;
            var stream = new BinaryReader(VirtualFileSystemManager.Instance.FileSystem.OpenFile(path));


            try
            {
                XmlElement root;
                XmlNodeList list;
                xml.Load(stream.BaseStream);
                root = FindRoot(xml);
                list = root.ChildNodes;
                DateTime time = DateTime.Now;
                string label = this.loadingTab;
                if (list.Count > 100)
                    Logger.ProgressBarShow(0, (uint)list.Count, label);

                foreach (object j in list)
                {
                    var item = new T();
                    XmlElement i;
                    if (j.GetType() != typeof(XmlElement)) continue;
                    i = (XmlElement)j;
                    try
                    {
                        Parse(root, i, item);
                    }
                    catch (Exception ex)
                    {
                        Logger.ShowError("Error on parsing:" + path);
                        Logger.ShowError(GetKey(item).ToString());

                        Logger.ShowError(ex);
                    }

                    uint key = GetKey(item);

                    if (!items.ContainsKey(key))
                    {
                        items.Add(key, item);
                    }
                    else
                    {
                        T instance = null;

                        if (items.TryGetValue(key, out instance))
                        {
                            item.Variation = true;
                            instance.AddInstance(item);
                        }
                        else
                        {
                            throw new NullReferenceException("couldnt get first instance of skill: " + key);
                        }
                    }
#if !Web
                    if ((DateTime.Now - time).TotalMilliseconds > 10)
                    {
                        time = DateTime.Now;
                        if (list.Count > 100)
                            Logger.ProgressBarShow((uint)count, (uint)list.Count, label);
                    }
#endif
                    if (!item.Variation)
                    {
                        count++;
                        this.Count = (uint)count;
                    }
                    else
                    {
                        this.VariationCount += 1;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.ShowError("Error on parsing:" + path);
                Logger.ShowError(ex.Message);
            }

            stream.Close();
            xml.RemoveAll();
            xml = null;

            return count;
        }

        protected void ParseNode(XmlElement child, T item, bool bDuplicate = false)
        {
            XmlNodeList list;
            list = child.ChildNodes;
            foreach (object j in list)
            {
                XmlElement i;
                if (j.GetType() != typeof(XmlElement)) continue;
                i = (XmlElement)j;
                try
                {
                    ParseXML(child, i, item, bDuplicate);
                    if (i.ChildNodes.Count != 0)
                        ParseNode(i, item, bDuplicate);
                }
                catch (Exception ex)
                {
                    Logger.ShowError("Error on parsing:" + Path);
                    Logger.ShowError(child.InnerXml);

                    Logger.ShowError(ex);
                }
            }
        }

        private void Parse(XmlElement root, XmlElement child, T item, bool bDuplicate = false)
        {
            ParseXML(root, child, item, bDuplicate);
            if (child.ChildNodes.Count != 0)
            {
                ParseNode(child, item);
            }
        }

        protected virtual void ParseXML(XmlElement root, XmlElement current, T item, bool bDuplicate)
        {
            base.ParseXML(root, current, item);
        }

        //protected abstract void ParseDuplicateXML(XmlElement root, XmlElement current, T item);
        //protected abstract void ParseDuplicateXML(DataTable root, DataRow current, DataColumn column, T item);

    }
}
