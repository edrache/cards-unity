using System;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class SchedulingBufferTests
    {
        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(int.MinValue)]
        public void ConstructorRejectsNonPositiveCapacity(int capacity)
        {
            ArgumentOutOfRangeException exception =
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => new Core.SchedulingBuffer<int>(capacity));

            Assert.That(exception.ParamName, Is.EqualTo("capacity"));
        }

        [Test]
        public void NewBufferExposesEmptyBoundedState()
        {
            var buffer = new Core.SchedulingBuffer<int>(3);

            Assert.That(buffer.Capacity, Is.EqualTo(3));
            Assert.That(buffer.Count, Is.Zero);
            Assert.That(buffer.IsEmpty, Is.True);
            Assert.That(buffer.IsFull, Is.False);
        }

        [Test]
        public void BufferDequeuesItemsInInsertionOrder()
        {
            var buffer = new Core.SchedulingBuffer<int>(3);

            Assert.That(buffer.TryEnqueue(10), Is.True);
            Assert.That(buffer.TryEnqueue(20), Is.True);
            Assert.That(buffer.TryEnqueue(30), Is.True);

            Assert.That(buffer.TryDequeue(out int first), Is.True);
            Assert.That(buffer.TryDequeue(out int second), Is.True);
            Assert.That(buffer.TryDequeue(out int third), Is.True);
            Assert.That(first, Is.EqualTo(10));
            Assert.That(second, Is.EqualTo(20));
            Assert.That(third, Is.EqualTo(30));
            Assert.That(buffer.IsEmpty, Is.True);
        }

        [Test]
        public void FullBufferRejectsNewItemsWithoutOverwritingUnreadData()
        {
            var buffer = new Core.SchedulingBuffer<int>(2);

            Assert.That(buffer.TryEnqueue(10), Is.True);
            Assert.That(buffer.TryEnqueue(20), Is.True);
            Assert.That(buffer.IsFull, Is.True);
            Assert.That(buffer.TryEnqueue(30), Is.False);
            Assert.That(buffer.Count, Is.EqualTo(2));

            Assert.That(buffer.TryDequeue(out int first), Is.True);
            Assert.That(buffer.TryDequeue(out int second), Is.True);
            Assert.That(first, Is.EqualTo(10));
            Assert.That(second, Is.EqualTo(20));
        }

        [Test]
        public void EmptyBufferReturnsFalseAndDefaultValues()
        {
            var buffer = new Core.SchedulingBuffer<int>(2);

            Assert.That(buffer.TryPeek(out int peeked), Is.False);
            Assert.That(buffer.TryDequeue(out int dequeued), Is.False);
            Assert.That(peeked, Is.Zero);
            Assert.That(dequeued, Is.Zero);
            Assert.That(buffer.Count, Is.Zero);
        }

        [Test]
        public void PeekDoesNotRemoveTheOldestItem()
        {
            var buffer = new Core.SchedulingBuffer<int>(2);
            buffer.TryEnqueue(10);
            buffer.TryEnqueue(20);

            Assert.That(buffer.TryPeek(out int firstPeek), Is.True);
            Assert.That(buffer.TryPeek(out int secondPeek), Is.True);
            Assert.That(firstPeek, Is.EqualTo(10));
            Assert.That(secondPeek, Is.EqualTo(10));
            Assert.That(buffer.Count, Is.EqualTo(2));
        }

        [Test]
        public void BufferPreservesOrderAcrossRepeatedIndexWraparound()
        {
            var buffer = new Core.SchedulingBuffer<int>(3);

            for (int value = 0; value < 30; value++)
            {
                Assert.That(buffer.TryEnqueue(value), Is.True);
                Assert.That(buffer.TryDequeue(out int dequeued), Is.True);
                Assert.That(dequeued, Is.EqualTo(value));
            }

            buffer.TryEnqueue(30);
            buffer.TryEnqueue(31);
            Assert.That(buffer.TryDequeue(out int first), Is.True);
            buffer.TryEnqueue(32);
            buffer.TryEnqueue(33);

            Assert.That(first, Is.EqualTo(30));
            Assert.That(buffer.TryDequeue(out int second), Is.True);
            Assert.That(buffer.TryDequeue(out int third), Is.True);
            Assert.That(buffer.TryDequeue(out int fourth), Is.True);
            Assert.That(second, Is.EqualTo(31));
            Assert.That(third, Is.EqualTo(32));
            Assert.That(fourth, Is.EqualTo(33));
        }

        [Test]
        public void ClearRemovesUnreadItemsAndRetainsCapacityForReuse()
        {
            var buffer = new Core.SchedulingBuffer<int>(3);
            buffer.TryEnqueue(10);
            buffer.TryEnqueue(20);

            buffer.Clear();

            Assert.That(buffer.Capacity, Is.EqualTo(3));
            Assert.That(buffer.Count, Is.Zero);
            Assert.That(buffer.IsEmpty, Is.True);
            Assert.That(buffer.TryDequeue(out _), Is.False);
            Assert.That(buffer.TryEnqueue(30), Is.True);
            Assert.That(buffer.TryDequeue(out int reused), Is.True);
            Assert.That(reused, Is.EqualTo(30));
        }

        [Test]
        public void EnqueueCopiesValueTypeState()
        {
            var buffer = new Core.SchedulingBuffer<TestValue>(1);
            var value = new TestValue(10);

            buffer.TryEnqueue(value);
            value.Number = 20;

            Assert.That(buffer.TryDequeue(out TestValue stored), Is.True);
            Assert.That(stored.Number, Is.EqualTo(10));
        }

        [Test]
        public void SingleItemBufferSupportsEveryBoundaryTransition()
        {
            var buffer = new Core.SchedulingBuffer<int>(1);

            Assert.That(buffer.TryEnqueue(10), Is.True);
            Assert.That(buffer.IsFull, Is.True);
            Assert.That(buffer.TryEnqueue(20), Is.False);
            Assert.That(buffer.TryDequeue(out int value), Is.True);
            Assert.That(value, Is.EqualTo(10));
            Assert.That(buffer.IsEmpty, Is.True);
        }

        [Test]
        public void EnqueueAndDequeueHotPathAllocatesNoManagedMemory()
        {
            var buffer = new Core.SchedulingBuffer<int>(8);
            buffer.TryEnqueue(0);
            buffer.TryDequeue(out _);

            long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
            int checksum = 0;
            bool allOperationsSucceeded = true;

            for (int iteration = 0; iteration < 10_000; iteration++)
            {
                allOperationsSucceeded &= buffer.TryEnqueue(iteration);
                allOperationsSucceeded &= buffer.TryDequeue(out int value);
                checksum += value;
            }

            long afterBytes = GC.GetAllocatedBytesForCurrentThread();

            Assert.That(allOperationsSucceeded, Is.True);
            Assert.That(checksum, Is.EqualTo(49_995_000));
            Assert.That(afterBytes - beforeBytes, Is.Zero);
        }

        private struct TestValue
        {
            public TestValue(int number)
            {
                Number = number;
            }

            public int Number { get; set; }
        }
    }
}
