using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using ZES.Interfaces.Net;

namespace ZES.Infrastructure
{
    /// <inheritdoc />
    [JsonArray]
    public class JsonList<T> : ICollection<T>, IJsonResult
    {
        private readonly List<T> _list = new List<T>();

        /// <inheritdoc />
        public int Count => _list.Count;

        /// <inheritdoc />
        public bool IsReadOnly => false;

        /// <inheritdoc />
        public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <inheritdoc />
        public void Add(T item) => _list.Add(item);

        /// <inheritdoc />
        public void Clear() => _list.Clear();

        /// <inheritdoc />
        public bool Contains(T item) => _list.Contains(item);

        /// <inheritdoc />
        public void CopyTo(T[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);

        /// <inheritdoc />
        public bool Remove(T item) => _list.Remove(item);

        /// <inheritdoc />
        public string RequestorId { get; set; }
    }
}