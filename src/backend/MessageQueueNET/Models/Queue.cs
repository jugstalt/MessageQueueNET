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
        private bool _removed;

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

        // false, if the queue was marked for removal in the meantime => the caller has to get the queue again
        public bool Enqueue(QueueItem item) => EnqueueRange(new[] { item });

        public bool EnqueueRange(IEnumerable<QueueItem> items)
        {
            lock (_syncRoot)
            {
                if (_removed)
                {
                    return false;
                }

                foreach (var item in items)
                {
                    _nestedQueue.Enqueue(item);
                }

                return true;
            }
        }

        public bool IsRemoved
        {
            get { lock (_syncRoot) { return _removed; } }
        }

        // Marks the queue for removal, if it has no valid items. After that, no items can be enqueued anymore.
        public bool TryMarkForRemoval(Func<QueueItem, bool> isValid)
        {
            lock (_syncRoot)
            {
                if (_nestedQueue.Any(isValid))
                {
                    return false;
                }

                _removed = true;
                return true;
            }
        }

        public void CancelRemoval()
        {
            lock (_syncRoot)
            {
                _removed = false;
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
