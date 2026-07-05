using NUnit.Framework;

namespace CardsUnity.Tests
{
    public class SockGrabStateMachineTests
    {
        [Test]
        public void InitialState_IsIdle()
        {
            var sm = new SockGrabStateMachine();
            Assert.AreEqual(SockGrabState.Idle, sm.CurrentState);
            Assert.AreEqual(-1, sm.ActiveHandleIndex);
        }

        [Test]
        public void OnPointerMoved_WithHoveredHandle_EntersHovering()
        {
            var sm = new SockGrabStateMachine();
            sm.OnPointerMoved(2);
            Assert.AreEqual(SockGrabState.Hovering, sm.CurrentState);
            Assert.AreEqual(2, sm.ActiveHandleIndex);
        }

        [Test]
        public void OnPointerMoved_WithNoHoveredHandle_ReturnsToIdle()
        {
            var sm = new SockGrabStateMachine();
            sm.OnPointerMoved(2);
            sm.OnPointerMoved(-1);
            Assert.AreEqual(SockGrabState.Idle, sm.CurrentState);
            Assert.AreEqual(-1, sm.ActiveHandleIndex);
        }

        [Test]
        public void TryBeginDrag_WhileHovering_EntersDraggingAndReturnsTrue()
        {
            var sm = new SockGrabStateMachine();
            sm.OnPointerMoved(1);
            bool started = sm.TryBeginDrag();
            Assert.IsTrue(started);
            Assert.AreEqual(SockGrabState.Dragging, sm.CurrentState);
            Assert.AreEqual(1, sm.ActiveHandleIndex);
        }

        [Test]
        public void TryBeginDrag_WhileIdle_ReturnsFalseAndStaysIdle()
        {
            var sm = new SockGrabStateMachine();
            bool started = sm.TryBeginDrag();
            Assert.IsFalse(started);
            Assert.AreEqual(SockGrabState.Idle, sm.CurrentState);
        }

        [Test]
        public void OnPointerMoved_WhileDragging_DoesNotChangeActiveHandle()
        {
            var sm = new SockGrabStateMachine();
            sm.OnPointerMoved(1);
            sm.TryBeginDrag();
            sm.OnPointerMoved(-1);
            Assert.AreEqual(SockGrabState.Dragging, sm.CurrentState);
            Assert.AreEqual(1, sm.ActiveHandleIndex);
        }

        [Test]
        public void EndDrag_WhileDragging_ReturnsToIdle()
        {
            var sm = new SockGrabStateMachine();
            sm.OnPointerMoved(1);
            sm.TryBeginDrag();
            sm.EndDrag();
            Assert.AreEqual(SockGrabState.Idle, sm.CurrentState);
            Assert.AreEqual(-1, sm.ActiveHandleIndex);
        }

        [Test]
        public void EndDrag_WhileNotDragging_DoesNothing()
        {
            var sm = new SockGrabStateMachine();
            sm.OnPointerMoved(1);
            sm.EndDrag();
            Assert.AreEqual(SockGrabState.Hovering, sm.CurrentState);
            Assert.AreEqual(1, sm.ActiveHandleIndex);
        }
    }
}
