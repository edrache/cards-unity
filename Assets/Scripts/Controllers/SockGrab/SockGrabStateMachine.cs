namespace CardsUnity
{
    public class SockGrabStateMachine
    {
        public SockGrabState CurrentState { get; private set; } = SockGrabState.Idle;

        public int ActiveHandleIndex { get; private set; } = -1;

        public void OnPointerMoved(int hoveredHandleIndex)
        {
            if (CurrentState == SockGrabState.Dragging)
                return;

            if (hoveredHandleIndex >= 0)
            {
                CurrentState = SockGrabState.Hovering;
                ActiveHandleIndex = hoveredHandleIndex;
            }
            else
            {
                CurrentState = SockGrabState.Idle;
                ActiveHandleIndex = -1;
            }
        }

        public bool TryBeginDrag()
        {
            if (CurrentState != SockGrabState.Hovering)
                return false;

            CurrentState = SockGrabState.Dragging;
            return true;
        }

        public void EndDrag()
        {
            if (CurrentState != SockGrabState.Dragging)
                return;

            CurrentState = SockGrabState.Idle;
            ActiveHandleIndex = -1;
        }
    }
}
