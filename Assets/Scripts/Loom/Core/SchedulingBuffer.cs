using System;

namespace Loom.Core
{
    /// <summary>
    /// Owns a fixed-capacity first-in, first-out ring buffer for scheduler hot paths.
    /// </summary>
    /// <typeparam name="T">The value type stored by the scheduler.</typeparam>
    /// <remarks>The buffer has one single-threaded owner and performs no synchronization.</remarks>
    public sealed class SchedulingBuffer<T>
        where T : struct
    {
        private readonly T[] items;
        private int headIndex;
        private int tailIndex;

        /// <summary>
        /// Allocates the complete bounded storage used for the lifetime of the buffer.
        /// </summary>
        /// <param name="capacity">The positive maximum number of unread items.</param>
        public SchedulingBuffer(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(capacity),
                    capacity,
                    "Scheduling buffer capacity must be positive.");
            }

            items = new T[capacity];
        }

        public int Capacity => items.Length;

        public int Count { get; private set; }

        public bool IsEmpty => Count == 0;

        public bool IsFull => Count == items.Length;

        /// <summary>
        /// Appends an item unless the buffer is full. Existing unread items are never overwritten.
        /// </summary>
        public bool TryEnqueue(T item)
        {
            if (IsFull)
            {
                return false;
            }

            items[tailIndex] = item;
            tailIndex = IncrementAndWrap(tailIndex);
            Count++;
            return true;
        }

        /// <summary>
        /// Copies the oldest unread item without removing it.
        /// </summary>
        public bool TryPeek(out T item)
        {
            if (IsEmpty)
            {
                item = default;
                return false;
            }

            item = items[headIndex];
            return true;
        }

        /// <summary>
        /// Copies and removes the oldest unread item.
        /// </summary>
        public bool TryDequeue(out T item)
        {
            if (IsEmpty)
            {
                item = default;
                return false;
            }

            item = items[headIndex];
            items[headIndex] = default;
            headIndex = IncrementAndWrap(headIndex);
            Count--;
            return true;
        }

        /// <summary>
        /// Removes every unread item while retaining the preallocated storage.
        /// </summary>
        public void Clear()
        {
            Array.Clear(items, 0, items.Length);
            headIndex = 0;
            tailIndex = 0;
            Count = 0;
        }

        private int IncrementAndWrap(int index)
        {
            index++;
            return index == items.Length ? 0 : index;
        }
    }
}
