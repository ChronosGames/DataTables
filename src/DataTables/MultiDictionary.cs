using System;
using System.Collections.Generic;
using System.Linq;

namespace DataTables
{
    public class MultiDictionary<TKey1, TKey2, TValue> : Dictionary<MultiKey<TKey1, TKey2>, TValue>
    {
        public void Add(TKey1 key1, TKey2 key2, TValue value)
        {
            this.Add(KeyCreator(key1, key2), value);
        }

        public bool Contains(TKey1 key1, TKey2 key2, TValue value)
        {
            var keys = KeyCreator(key1, key2);
            var item = new KeyValuePair<MultiKey<TKey1, TKey2>, TValue>(keys, value);
            return this.Contains(item);
        }

        public bool Contains(TKey1 key1, TKey2 key2, TValue value, IEqualityComparer<KeyValuePair<MultiKey<TKey1, TKey2>, TValue>> comparer)
        {
            var keys = KeyCreator(key1, key2);
            var item = new KeyValuePair<MultiKey<TKey1, TKey2>, TValue>(keys, value);
            return this.Contains(item, comparer);
        }

        public bool ContainsKey(TKey1 key1, TKey2 key2)
        {
            return ContainsKey(KeyCreator(key1, key2));
        }

        public bool Remove(TKey1 key1, TKey2 key2)
        {
            return Remove(KeyCreator(key1, key2));
        }

        public bool TryGetValue(TKey1 key1, TKey2 key2, out TValue value)
        {
            return TryGetValue(KeyCreator(key1, key2), out value);
        }

        private MultiKey<TKey1, TKey2> KeyCreator(TKey1 key1, TKey2 key2)
        {
            return new MultiKey<TKey1, TKey2>(key1, key2);
        }
    }
}
