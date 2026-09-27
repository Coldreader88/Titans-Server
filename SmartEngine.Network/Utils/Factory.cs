using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Xml;
using System.Diagnostics;
using SmartEngine.Network.Utils.Factory_Nodes;
using SmartEngine.Network.VirtualFileSystem;
using SmartEngine.Network.IO;
using SmartEngine.Core;
using SmartEngine.Network.Utils.Factory_Nodes;

namespace SmartEngine.Network.Utils
{
    public enum FactoryType
    {
        NONE,
        CSV,
        XML,
        XMLDATA,
        LARGEXML,
    }

    /// <summary>
    /// 通用CSV/XML读取器
    /// </summary>
    /// <typeparam name="K">类自身，用于创建Singleton</typeparam>
    /// <typeparam name="T">读取后数据类型</typeparam>
    public abstract class Factory<K, T>
        where K : new()
        where T : new()
    {
        protected Dictionary<uint, T> items = new Dictionary<uint, T>();
        FactoryType type;
        protected string loadingTab = "";
        protected string loadedTab = "";
        protected string databaseName = "";

        public Dictionary<uint, T> Items { get { return items; } }
        public T this[uint id]
        {
            get
            {
                return items[id];
            }
            set
            {
                items[id] = value;
            }
        }
        public FactoryType FactoryType { get { return this.type; } set { this.type = value; } }
        string path;
        Encoding encoding;
        bool isFolder;
        bool bLoaded = false;

        public bool Loaded { get { return bLoaded; } }
        protected bool ModularFactory { get; set; }

        public uint Count { get; set; }
        public uint VariationCount { get; set; }

        public Factory()
        {

        }

        protected abstract uint GetKey(T item);

        protected abstract void ParseCSV(T item, string[] paras);

        protected virtual void ParseXML(XmlElement root, XmlElement current, T item)
        {
            throw new NotImplementedException();
        }

        protected virtual void ParseXML(DataTable root, DataRow current, DataColumn column, T item)
        {
            throw new NotImplementedException();
        }

        protected virtual void ParseXML(DataElement root, DataElement current, T item)
        {
            throw new NotImplementedException();
        }

        public virtual void FinalizeData()
        {

        }

        public void Reload()
        {
            items.Clear();
            Init(path, encoding, isFolder, ModularFactory);
        }

        public void Init(string[] files, System.Text.Encoding encoding, bool bModularFactory = false)
        {
            int count = 0;
            this.encoding = encoding;
            this.ModularFactory = bModularFactory;

            switch (this.type)
            {
                case FactoryType.CSV:
                    foreach (string i in files)
                        count += InitCSV(i, encoding);
                    break;
                case FactoryType.XML:
                    foreach (string i in files)
                        count += InitXML(i, encoding);
                    break;
                case FactoryType.XMLDATA:
                    foreach (string i in files)
                        count += InitLargeXML(i, encoding);
                    break;
                case FactoryType.LARGEXML:
                    foreach (string i in files)
                        count += InitLargeXML(i, encoding, true);
                    break;
                default:
                    throw new Exception(string.Format("No FactoryType set for class:{0}", this.ToString()));
            }

            this.Count = (uint)count;

#if !Web
            Logger.ProgressBarHide(Count + this.loadedTab);
#endif
            bLoaded = true;
        }

        public void Init(string path, System.Text.Encoding encoding, bool isFolder, bool bModularFactory = false)
        {
            string[] files = null;
            int count = 0;
            this.path = path;
            this.encoding = encoding;
            this.isFolder = isFolder;
            this.ModularFactory = bModularFactory;
            if (isFolder)
            {
                string pattern = "*.*";
                if (this.FactoryType == FactoryType.CSV)
                    pattern = "*.csv";
                else if (this.FactoryType == FactoryType.XML)
                    pattern = "*.xml";
                files = VirtualFileSystemManager.Instance.FileSystem.SearchFile(path, pattern);
            }
            else
            {
                files = new string[1];
                files[0] = path;
            }
            switch (this.type)
            {
                case FactoryType.CSV:
                    foreach (string i in files)
                        count += InitCSV(i, encoding);
                    break;
                case FactoryType.XML:
                    foreach (string i in files)
                        count += InitXML(i, encoding);
                    break;
                case FactoryType.XMLDATA:
                    foreach (string i in files)
                        count += InitLargeXML(i, encoding);
                    break;
                case FactoryType.LARGEXML:
                    foreach (string i in files)
                        count += InitLargeXML(i, encoding, true);
                    break;
                default:
                    throw new Exception(string.Format("No FactoryType set for class:{0}", this.ToString()));
            }

            this.Count = (uint)count;

#if !Web
            Logger.ProgressBarHide(Count + this.loadedTab);
#endif
            bLoaded = true;
        }

        public void Init(string path, System.Text.Encoding encoding, bool bModularFactory = false)
        {
            Init(path, encoding, false, bModularFactory);
        }

        protected XmlElement FindRoot(XmlDocument doc)
        {
            foreach (object i in doc.ChildNodes)
            {
                if (i.GetType() == typeof(XmlElement))
                    return (XmlElement)i;
            }
            return null;
        }

        protected virtual void ParseNode(XmlElement ele, T item)
        {
            XmlNodeList list;
            list = ele.ChildNodes;
            foreach (object j in list)
            {
                XmlElement i;
                if (j.GetType() != typeof(XmlElement)) continue;
                i = (XmlElement)j;
                try
                {
                    ParseXML(ele, i, item);
                    if (i.ChildNodes.Count != 0)
                        ParseNode(i, item);
                }
                catch (Exception ex)
                {
                    Logger.ShowError("Error on parsing:" + path);
                    Logger.ShowError(ele.InnerXml);

                    Logger.ShowError(ex);
                }
            }
        }

        protected virtual int InitXML(string path, System.Text.Encoding encoding)
        {
            if (!path.ToLower().EndsWith(".xml") && !path.ToLower().EndsWith(".gz"))
            {
                path += ".xml";
            }

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
                    T item = new T();
                    XmlElement i;
                    if (j.GetType() != typeof(XmlElement)) continue;
                    i = (XmlElement)j;
                    try
                    {
                        ParseXML(root, i, item);
                        if (i.ChildNodes.Count != 0)
                            ParseNode(i, item);
                    }
                    catch (Exception ex)
                    {
                        Logger.ShowError("Error on parsing:" + path);
                        Logger.ShowError(GetKey(item).ToString());

                        Logger.ShowError(ex);
                    }
                    uint key = GetKey(item);
                    if (!items.ContainsKey(key))
                        items.Add(key, item);
#if !Web
                    if ((DateTime.Now - time).TotalMilliseconds > 10)
                    {
                        time = DateTime.Now;
                        if (list.Count > 100)
                            Logger.ProgressBarShow((uint)count, (uint)list.Count, label);
                    }
#endif
                    count++;
                }

                this.FinalizeData();
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

        protected virtual int InitLargeXML(string path, System.Text.Encoding encoding, bool bCompatibility = false)
        {
            int count = 0;

            if (!path.ToLower().EndsWith(".xml") && !path.ToLower().EndsWith(".gz"))
            {
                path += ".xml";
            }

            if (Debugger.IsAttached)
            {
                Debug.WriteLine(string.Format("Loading LARGE XML: {0}", path));
            }

            try
            {
                DateTime time = DateTime.Now;
                DateTime loadStart = DateTime.Now;
                string label = this.loadingTab;

                var data = new DataSet();

                data.ReadXml(VirtualFileSystemManager.Instance.FileSystem.OpenFile(path));

                var loadEnd = DateTime.Now;

                TimeSpan span = loadEnd.Subtract(loadStart);

                if (Debugger.IsAttached || true)
                {
                    Debug.WriteLine("Time taken to load database: " + span.ToString());
                }

                var Tables = data.Tables;
                int rowCount = 0;

                foreach (DataTable table in Tables)
                {
                    rowCount += table.Rows.Count;
                }

                //var info = string.Format("Tables:{0} Rows: {1}", Tables.Count, rowCount);

                //Logger.ShowWarning(info);


                if (rowCount > 100)
                    Logger.ProgressBarShow((uint)count, (uint)rowCount, label);

                var parseStart = DateTime.Now;

                foreach (DataTable table in Tables)
                {
                    //System.Diagnostics.Debug.WriteLine(string.Format("table: {0}", table.Namespace));
                    foreach (DataRow row in table.Rows)
                    {

                        //System.Diagnostics.Debug.WriteLine(string.Format("root: {0}", row.Table.TableName));

                        var item = new T();

                        foreach (DataColumn column in table.Columns)
                        {
                            var ColumnName = column.ColumnName;
                            var ColumnData = row[column].ToString();

                            if (!bCompatibility)
                            {
                                ParseXML(table, row, column, item);
                            }
                            else
                            {
                                var root = new DataElement(table.TableName, "");
                                var current = new DataElement(ColumnName, ColumnData);

                                ParseXML(root, current, item);
                            }

                            //System.Diagnostics.Debug.WriteLine("{0} - {1}", ColumnName, ColumnData);
                        }

                        uint key = GetKey(item);
                        if (!items.ContainsKey(key))
                            items.Add(key, item);

                        /*if (Debugger.IsAttached)
                        {
                            Debug.WriteLine("Added Item: " + key);
                        }*/

                        if ((DateTime.Now - time).TotalMilliseconds > 10)
                        {
                            time = DateTime.Now;
                            if (rowCount > 100)
                                Logger.ProgressBarShow((uint) count, (uint) rowCount, label);
                        }
                        count++;
                    }
                }

                this.FinalizeData();

                if (Debugger.IsAttached)
                {
                    var parseEnd = DateTime.Now;

                    var parseSpan = parseEnd.Subtract(parseStart);

                    Debug.WriteLine("Time taken to iterate through the database: " + parseSpan.ToString());
                }

            }
            catch (Exception ex)
            {
                Logger.ShowError("Error on parsing:" + path);
                Logger.ShowError(ex.Message);
            }

            return count;
        }

        int InitCSV(string path, System.Text.Encoding encoding)
        {
            if (!path.ToLower().EndsWith(".csv"))
            {
                path += ".csv";
            }

            System.IO.StreamReader sr = new System.IO.StreamReader(VirtualFileSystemManager.Instance.FileSystem.OpenFile(path), encoding);
            int count = 0;
            int lines = 0;
            string label = this.loadingTab;
#if !Web
            Logger.ProgressBarShow(0, (uint)sr.BaseStream.Length, label);
#endif
            DateTime time = DateTime.Now;
            while (!sr.EndOfStream)
            {
                string line;
                lines++;
                line = sr.ReadLine();
                string[] paras;
                try
                {
                    T item = new T();
                    if (line.IndexOf('#') != -1)
                        line = line.Substring(0, line.IndexOf('#'));
                    if (line == "") continue;
                    paras = line.Split(',');
                    if (paras.Length < 2)
                        continue;
                    for (int i = 0; i < paras.Length; i++)
                    {
                        if (paras[i] == "")
                            paras[i] = "0";
                    }
                    ParseCSV(item, paras);

                    uint key = GetKey(item);
                    if (!items.ContainsKey(key))
                        items.Add(key, item);
#if !Web
                    if ((DateTime.Now - time).TotalMilliseconds > 10)
                    {
                        time = DateTime.Now;
                        Logger.ProgressBarShow((uint)sr.BaseStream.Position, (uint)sr.BaseStream.Length, label);
                    }
#endif
                    count++;
                }
                catch (Exception)
                {
                    Logger.ShowError("Error on parsing " + this.databaseName + " db!\r\n       File:" + path + ":" + lines.ToString() + "\r\n       Content:" + line);
                }
            }
            sr.Close();
            return count;
        }

        /// <summary>
        /// Return an instance of 
        /// </summary>
        public static K Instance
        {
            get { return SingletonHolder.instance; }
            set { SingletonHolder.instance = value; }
        }

        /// <summary>
        /// Sealed class to avoid any heritage from this helper class
        /// </summary>
        private sealed class SingletonHolder
        {
            internal static K instance = new K();

            /// <summary>
            /// Explicit static constructor to tell C# compiler not to mark type as beforefieldinit
            /// </summary>
            static SingletonHolder()
            {
            }
        }
    }
}
