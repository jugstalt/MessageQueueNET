using System;

namespace MessageQueueNET.Models
{
    public class QueueItem
    {
        public QueueItem()
        {
            this.Id = Guid.NewGuid();
            ResetCreationDate();
        }

        public Guid Id { get; set; }
        public DateTime CreationDateUTC { get; set; }
        public DateTime? DequeuedUTC { get; set; }
        public string Message { get; set; } = "";
        public string DequeuingClientId { get; set; } = "";
        // authenticated user that dequeued the item (empty if authentication is disabled)
        public string DequeuingUser { get; set; } = "";

        private void ResetCreationDate()
        {
            this.CreationDateUTC = DateTime.UtcNow;
        }

        // Keeps the original CreationDateUTC, so ItemLifetimeSeconds is not reset when an
        // unconfirmed item is re-enqueued.
        public QueueItem CloneForUnconfirmed(string clientId, string? user = null)
        {
            return new QueueItem()
            {
                Id = this.Id,
                Message = this.Message,
                CreationDateUTC = this.CreationDateUTC,
                DequeuedUTC = DateTime.UtcNow,
                DequeuingClientId = clientId,
                DequeuingUser = user ?? ""
            };
        }

        // Items persisted by older versions have no DequeuedUTC; there CreationDateUTC was the dequeue time.
        public DateTime UnconfirmedSinceUTC => DequeuedUTC ?? CreationDateUTC;

        public bool IsValid(Queue queue)
        {
            if (queue.Properties.ItemLifetimeSeconds > 0 &&
                (DateTime.UtcNow - this.CreationDateUTC).TotalSeconds > queue.Properties.ItemLifetimeSeconds)
            {
                return false;
            }

            return true;
        }
    }
}
