using System;
using SCG = System.Collections.Generic;

using C5;

namespace SmartEngine.Network.Utils.Collections
{
    class BasicCollectionValue<T> : CollectionValueBase<T>, ICollectionValue<T>
    {
        SCG.IEnumerable<T> enumerable;
        Func<T> chooser;
        int count;
        //TODO: add delegate for checking validity!

        public BasicCollectionValue(SCG.IEnumerable<T> e, Func<T> chooser, int c) { enumerable = e; count = c; this.chooser = chooser; }

        public override int Count { get { return count; } }

        public override Speed CountSpeed { get { return Speed.Constant; } }

        public override bool IsEmpty { get { return count == 0; } }

        public override T Choose() { return chooser(); }

        public override System.Collections.Generic.IEnumerator<T> GetEnumerator()
        {
            return enumerable.GetEnumerator();
        }
    }

    interface IMultiCollection<K, V>
    {
        SCG.IEqualityComparer<K> KeyEqualityComparer { get; }
        SCG.IEqualityComparer<V> ValueEqualityComparer { get; }
        bool Add(K k, V v);
        bool Remove(K k, V v);
        ICollectionValue<V> this[K k] { get; }
        ICollectionValue<K> Keys { get; }
        SCG.IEnumerable<V> Values { get; }

    }

    public class BasicMultiCollection<K, V, W, U> : IMultiCollection<K, V>//: IDictionary<K, W>
        where W : ICollection<V>, new()
        where U : IDictionary<K, W>, new()
    {
        U dict = new U();

        public SCG.IEqualityComparer<K> KeyEqualityComparer { get { return EqualityComparer<K>.Default; } }

        public SCG.IEqualityComparer<V> ValueEqualityComparer { get { return EqualityComparer<V>.Default; } } //TODO: depends on W!

        public bool Add(K k, V v)
        {
            W w;
            if (!dict.Find(ref k, out w))
                dict.Add(k, w = new W());
            return w.Add(v);
        }

        public bool Remove(K k, V v)
        {
            W w;
            if (dict.Find(ref k, out w) && w.Remove(v))
            {
                if (w.Count == 0)
                    dict.Remove(k);
                return true;
            }
            return false;
        }

        public ICollectionValue<V> this[K k] { get { return dict[k]; } }

        public ICollectionValue<K> Keys { get { return dict.Keys; } }

        public SCG.IEnumerable<V> Values
        {
            get
            {
                foreach (W w in dict.Values)
                    foreach (V v in w)
                        yield return v;
            }
        }
    }

    public class MultiCollection<K, V> : BasicMultiCollection<K, V, HashSet<V>, HashDictionary<K, HashSet<V>>>
    {
        public int Count { get { return this.Keys.Count; } } 

        public bool Any()
        {
            return this.Keys.Count > 0;
        }

        /*
        public SCG.List<V> this[K k]
        {
            get
            {
                bool hasKey = false;

                foreach (var key in Keys)
                {
                    if (key.Equals(k))
                    {
                        hasKey = true;
                        break;
                    }
                }

                if (hasKey)
                {
                    return new SCG.List<V>(base[k].ToArray());
                }
                else
                {
                    return new SCG.List<V>();
                }

            }
        }
         */
    }

    public class MultiHashDictionary<K, V> : HashDictionary<K, ICollection<V>>
    {
        private int count = 0;      // Cached value count, updated by events only

        private void IncrementCount(Object sender, ItemCountEventArgs<V> args)
        {
            count += args.Count;
        }

        private void DecrementCount(Object sender, ItemCountEventArgs<V> args)
        {
            count -= args.Count;
        }

        private void ClearedCount(Object sender, ClearedEventArgs args)
        {
            count -= args.Count;
        }

        public MultiHashDictionary()
        {
            ItemsAdded +=
              delegate(Object sender, ItemCountEventArgs<KeyValuePair<K, ICollection<V>>> args)
              {
                  ICollection<V> values = args.Item.Value;
                  if (values != null)
                  {
                      count += values.Count;
                      values.ItemsAdded += IncrementCount;
                      values.ItemsRemoved += DecrementCount;
                      values.CollectionCleared += ClearedCount;
                  }
              };
            ItemsRemoved +=
              delegate(Object sender, ItemCountEventArgs<KeyValuePair<K, ICollection<V>>> args)
              {
                  ICollection<V> values = args.Item.Value;
                  if (values != null)
                  {
                      count -= values.Count;
                      values.ItemsAdded -= IncrementCount;
                      values.ItemsRemoved -= DecrementCount;
                      values.CollectionCleared -= ClearedCount;
                  }
              };
        }

        // Return total count of values associated with keys.  

        public new virtual int Count
        {
            get
            {
                return count;
            }
        }

        public override Speed CountSpeed
        {
            get { return Speed.Constant; }
        }

        // Add a (key,value) pair

        public virtual void Add(K k, V v)
        {
            ICollection<V> values;
            if (!base.Find(ref k, out values) || values == null)
            {
                values = new HashSet<V>();
                Add(k, values);
            }
            values.Add(v);
        }

        // Remove a single (key,value) pair, if present; return true if
        // anything was removed, else false

        public virtual bool Remove(K k, V v)
        {
            ICollection<V> values;
            if (base.Find(ref k, out values) && values != null)
            {
                if (values.Remove(v))
                {
                    if (values.IsEmpty)
                        base.Remove(k);
                    return true;
                }
            }
            return false;
        }

        // Determine whether key k is associated with a value

        public override bool Contains(K k)
        {
            ICollection<V> values;
            return Find(ref k, out values) && values != null && !values.IsEmpty;
        }

        // Determine whether each key in ks is associated with a value

        public override bool ContainsAll<U>(SCG.IEnumerable<U> ks)
        {
            foreach (K k in ks)
                if (!Contains(k))
                    return false;
            return true;
        }

        // Get or set the value collection associated with key k

        public override ICollection<V> this[K k]
        {
            get
            {
                ICollection<V> values;
                return base.Find(ref k, out values) && values != null ? values : new HashSet<V>();
            }
            set
            {
                base[k] = value;
            }
        }

        // Clearing the multidictionary should remove event listeners

        public override void Clear()
        {
            foreach (ICollection<V> values in Values)
                if (values != null)
                {
                    count -= values.Count;
                    values.ItemsAdded -= IncrementCount;
                    values.ItemsRemoved -= DecrementCount;
                    values.CollectionCleared -= ClearedCount;
                }
            base.Clear();
        }
    }


}
