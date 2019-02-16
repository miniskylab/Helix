using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Helix.Core
{
    public class ConcurrentSet<T> : ICollection<T>
    {
        readonly ConcurrentDictionary<T, bool> _concurrentDictionary;

        public int Count => _concurrentDictionary.Count;

        public bool IsReadOnly => false;

        public ConcurrentSet() { _concurrentDictionary = new ConcurrentDictionary<T, bool>(); }

        public void Add(T item)
        {
            if (item == null) throw new ArgumentNullException();
            if (!_concurrentDictionary.TryAdd(item, false))
                throw new InvalidOperationException("Cannot add the same item twice.");
        }

        public void Clear() { _concurrentDictionary.Clear(); }

        public bool Contains(T item)
        {
            if (item == null) throw new ArgumentNullException();
            return _concurrentDictionary.ContainsKey(item);
        }

        public void CopyTo(T[] array, int arrayIndex) { _concurrentDictionary.Keys.CopyTo(array, arrayIndex); }

        public IEnumerator<T> GetEnumerator() { return _concurrentDictionary.Keys.GetEnumerator(); }

        public bool Remove(T item)
        {
            if (item == null) throw new ArgumentNullException();
            return _concurrentDictionary.TryRemove(item, out _);
        }

        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
    }
}