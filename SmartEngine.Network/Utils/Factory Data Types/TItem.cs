using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

namespace SmartEngine.Network.Utils.FactoryDataTypes
{
    public class TTItem<A, B>
    {
        protected List<TTItem<A, B>> instances { get; set;}

        /// <summary>
        /// 技能ID
        /// </summary>
        public A ID { get; set; }

        /// <summary>
        /// 技能ID2
        /// </summary>
        public B ID2 { get; set; }

        /// <summary>
        /// 技能ID2
        /// </summary>
        public B ID3 { get; set; }

        public bool Variation { get; set; }

        public TTItem()
        {
            instances = new List<TTItem<A, B>>();

            this.AddInstance(this);

        }

        public void AddInstance(TTItem<A, B> instance)
        {
            instances.Add(instance);
        }

        protected TTItem<A, B> GetInstance(A a)
        {
            foreach (TTItem<A, B> item in this.instances)
            {
                if(item.ID.ToString() == a.ToString())
                {
                    return item;
                }
            }

            return null;
        }

        protected TTItem<A, B> GetInstance(B b)
        {
            foreach (TTItem<A, B> item in this.instances)
            {
                if(System.Convert.ToUInt64(item.ID2) == System.Convert.ToUInt64(b.ToString()))
                {
                    return item;
                }
            }

            return null;
        }

        /**
        protected virtual List<T> Instances<T>() where T: TTItem<A, B>, new()
        {
            List<T> list = new List<T>();


            foreach (var item in instances)
            {
                T t = (T)item;

                list.Add(t);
            }

            return list;
        }
        */

        public virtual void Finalize()
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            return string.Format("ID:{0}, ID2:{1}", ID, ID2);
        }
    }

    public class TItem<A, B, C> : TTItem<A, B> where C: TTItem<A, B>, new()
    {

        public TItem() : base()      
        {
        }

        public void AddInstance(C instance)
        {
            base.AddInstance(instance);
        }

        public new C GetInstance(B id)
        {

            foreach(var instance in this.Instances())
            {
                if(System.Convert.ToUInt64(instance.ID3) == System.Convert.ToUInt64(id))
                {
                    return instance;
                }
            }

            return Instances().FirstOrDefault();
        }

        public C GetLastInstance()
        {
            return Instances().LastOrDefault();
        }

        public List<C> Instances()
        {
            List<C> list = new List<C>();


            foreach (var item in instances)
            {
                C c = (C)item;

                list.Add(c);
            }

            return list;
        }
        

        public override string ToString()
        {
            return string.Format("ID:{0}, ID2:{1}", ID, ID2);
        }
    }

    public class ModularItem<T> : TItem<uint, uint, T> where T: TItem<uint, uint, T>, new()
    {
        public ModularItem()
            : base()
        {

        }
    }
}
