using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace MessageQueueNET.Models
{
    public class Queue : IEnumerable<QueueItem> //: ConcurrentQueue<QueueItem>
    {
        private QueueProperties _queueProperties;
        // _nestedQueue is replaced by TryDelete => all mutations are synchronized
        private volatile ConcurrentQueue<QueueItem> _nestedQueue;
        private readonly object _syncRoot = new object();

        public Queue(string name)
        {
            this.Name = name;

            this.LastAccessUTC = DateTime.UtcNow;
            this.LastModifiedUTC = DateTime.UtcNow;

            _nestedQueue = new ConcurrentQueue<QueueItem>();
            _queueProperties = new QueueProperties();
        }

        public string Name { get; }

        public DateTime LastAccessUTC { get; set; }
        public DateTime LastModifiedUTC { get; set; }

        public bool TryDelete(Guid id)
        {
            lock (_syncRoot)
            {
                if (_nestedQueue.Any(i => i.Id == id))
                {
                    _nestedQueue = new ConcurrentQueue<QueueItem>(_nestedQueue.Where(i => i.Id != id));
                    return true;
                }

                return false;
            }
        }

        public QueueProperties Properties
        {
            get { return _queueProperties; }
            internal set
            {
                _queueProperties = value ?? new QueueProperties();
            }
        }

        public bool TryDequeue(out QueueItem? item)
        {
            lock (_syncRoot)
            {
                return _nestedQueue.TryDequeue(out item);
            }
        }

        public void Enqueue(QueueItem item)
        {
            lock (_syncRoot)
            {
                _nestedQueue.Enqueue(item);
            }
        }

        #region Nested

        public int Count => _nestedQueue.Count;
        public void Clear()
        {
            lock (_syncRoot)
            {
                _nestedQueue.Clear();
            }
        }

        #region IEnumerable<QueueItem>

        public IEnumerator<QueueItem> GetEnumerator() => _nestedQueue.GetEnumerator();
        
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion

        #endregion
    }
}
