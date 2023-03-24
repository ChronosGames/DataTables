using System;
using System.Collections.Generic;
using System.Text;

namespace DataTables
{
    [Serializable]
    public struct MultiKey<TKey1, TKey2>
    {
        public TKey1 Key1 { get; }

        public TKey2 Key2 { get; }

        public MultiKey(TKey1 key1, TKey2 key2)
        {
            Key1 = key1;
            Key2 = key2;
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
            s.Append(']');
            return s.ToString();
        }

        public override bool Equals(object obj)
        {
            if (obj is MultiKey<TKey1, TKey2> multiKey)
            {
                return EqualityComparer<TKey1>.Default.Equals(Key1, multiKey.Key1) &&
                       EqualityComparer<TKey2>.Default.Equals(Key2, multiKey.Key2);
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
                return hash;
            }
        }

        public static bool operator ==(MultiKey<TKey1, TKey2> obj1, MultiKey<TKey1, TKey2> obj2)
        {
            return obj1.Equals(obj2);
        }

        public static bool operator !=(MultiKey<TKey1, TKey2> obj1, MultiKey<TKey1, TKey2> obj2)
        {
            return !(obj1 == obj2);
        }
    }
}
