using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DataTables
{
    [Serializable]
    public struct MultiKey<TKey1, TKey2, TKey3, TKey4>
    {
        public TKey1 Key1 { get; }

        public TKey2 Key2 { get; }

        public TKey3 Key3 { get; }

        public TKey4 Key4 { get; }

        public MultiKey(TKey1 key1, TKey2 key2, TKey3 key3, TKey4 key4)
        {
            Key1 = key1;
            Key2 = key2;
            Key3 = key3;
            Key4 = key4;
        }

        public override string ToString()
        {
            var s = new StringBuilder();
            s.Append('[');
            if (Key1 != null)
            {
                s.Append(Key1.ToString());
            }
            s.Append(", ");
            if (Key2 != null)
            {
                s.Append(Key2.ToString());
            }
            s.Append(", ");
            if (Key3 != null)
            {
                s.Append(Key3.ToString());
            }
            s.Append(", ");
            if (Key4 != null)
            {
                s.Append(Key4.ToString());
            }
            s.Append(']');
            return s.ToString();
        }

        public override bool Equals(object? obj)
        {
            if (obj is MultiKey<TKey1, TKey2, TKey3, TKey4> multiKey)
            {
                return EqualityComparer<TKey1>.Default.Equals(Key1, multiKey.Key1) &&
                       EqualityComparer<TKey2>.Default.Equals(Key2, multiKey.Key2) &&
                       EqualityComparer<TKey3>.Default.Equals(Key3, multiKey.Key3) &&
                       EqualityComparer<TKey4>.Default.Equals(Key4, multiKey.Key4);
            }

            return false;
        }

        //https://www.loganfranken.com/blog/687/overriding-equals-in-c-part-1/
        public override int GetHashCode()
        {
            unchecked
            {
                const int hashingBase = (int)2166136261;
                const int hashingMultiplier = 16777619;

                int hash = hashingBase;
                hash = (hash * hashingMultiplier) ^ Key1.GetHashCode();
                hash = (hash * hashingMultiplier) ^ Key2.GetHashCode();
                hash = (hash * hashingMultiplier) ^ Key3.GetHashCode();
                hash = (hash * hashingMultiplier) ^ Key4.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(MultiKey<TKey1, TKey2, TKey3, TKey4> obj1, MultiKey<TKey1, TKey2, TKey3, TKey4> obj2)
        {
            return obj1.Equals(obj2);
        }

        public static bool operator !=(MultiKey<TKey1, TKey2, TKey3, TKey4> obj1, MultiKey<TKey1, TKey2, TKey3, TKey4> obj2)
        {
            return !(obj1 == obj2);
        }
    }

    public class MultiDictionary<TKey1, TKey2, TKey3, TKey4, TValue> : Dictionary<MultiKey<TKey1, TKey2, TKey3, TKey4>, TValue>
    {
        public void Add(TKey1 key1, TKey2 key2, TKey3 key3, TKey4 key4, TValue value)
        {
            this.Add(KeyCreator(key1, key2, key3, key4), value);
        }

        public bool Contains(TKey1 key1, TKey2 key2, TKey3 key3, TKey4 key4, TValue value)
        {
            var keys = KeyCreator(key1, key2, key3, key4);
            var item = new KeyValuePair<MultiKey<TKey1, TKey2, TKey3, TKey4>, TValue>(keys, value);
            return this.Contains(item);
        }

        public bool Contains(TKey1 key1, TKey2 key2, TKey3 key3, TKey4 key4, TValue value, IEqualityComparer<KeyValuePair<MultiKey<TKey1, TKey2, TKey3, TKey4>, TValue>> comparer)
        {
            var keys = KeyCreator(key1, key2, key3, key4);
            var item = new KeyValuePair<MultiKey<TKey1, TKey2, TKey3, TKey4>, TValue>(keys, value);
            return this.Contains(item, comparer);
        }

        public bool ContainsKey(TKey1 key1, TKey2 key2, TKey3 key3, TKey4 key4)
        {
            return ContainsKey(KeyCreator(key1, key2, key3, key4));
        }

        public bool Remove(TKey1 key1, TKey2 key2, TKey3 key3, TKey4 key4)
        {
            return Remove(KeyCreator(key1, key2, key3, key4));
        }

        public bool TryGetValue(TKey1 key1, TKey2 key2, TKey3 key3, TKey4 key4, out TValue? value)
        {
            return TryGetValue(KeyCreator(key1, key2, key3, key4), out value);
        }

        private MultiKey<TKey1, TKey2, TKey3, TKey4> KeyCreator(TKey1 key1, TKey2 key2, TKey3 key3, TKey4 key4)
        {
            return new MultiKey<TKey1, TKey2, TKey3, TKey4>(key1, key2, key3, key4);
        }
    }
}
