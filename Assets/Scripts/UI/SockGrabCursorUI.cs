using UnityEngine;

namespace CardsUnity.UI
{
    public class SockGrabCursorUI : MonoBehaviour
    {
        [SerializeField] private SockDragController dragController;
        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private RectTransform hoverCursorVisual;
        [SerializeField] private RectTransform dragCursorVisual;
        [SerializeField] private Camera uiCamera;

        public SockDragController DragController => dragController;

        private void OnEnable()
        {
            if (dragController == null || hoverCursorVisual == null || dragCursorVisual == null)
                return;

            dragController.HoverStarted += HandleHoverStarted;
            dragController.HoverUpdated += HandleHoverUpdated;
            dragController.HoverEnded += HandleHoverEnded;
            dragController.DragStarted += HandleDragStarted;
            dragController.DragUpdated += HandleDragUpdated;
            dragController.DragEnded += HandleDragEnded;

            hoverCursorVisual.gameObject.SetActive(false);
            dragCursorVisual.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (dragController == null)
                return;

            dragController.HoverStarted -= HandleHoverStarted;
            dragController.HoverUpdated -= HandleHoverUpdated;
            dragController.HoverEnded -= HandleHoverEnded;
            dragController.DragStarted -= HandleDragStarted;
            dragController.DragUpdated -= HandleDragUpdated;
            dragController.DragEnded -= HandleDragEnded;
        }

        private void HandleHoverStarted(Vector2 screenPosition)
        {
            hoverCursorVisual.gameObject.SetActive(true);
            PositionAt(hoverCursorVisual, screenPosition);
        }

        private void HandleHoverUpdated(Vector2 screenPosition)
        {
            PositionAt(hoverCursorVisual, screenPosition);
        }

        private void HandleHoverEnded()
        {
            hoverCursorVisual.gameObject.SetActive(false);
        }

        private void HandleDragStarted(Vector2 screenPosition)
        {
            hoverCursorVisual.gameObject.SetActive(false);
            dragCursorVisual.gameObject.SetActive(true);
            PositionAt(dragCursorVisual, screenPosition);
        }

        private void HandleDragUpdated(Vector2 screenPosition)
        {
            PositionAt(dragCursorVisual, screenPosition);
        }

        private void HandleDragEnded()
        {
            dragCursorVisual.gameObject.SetActive(false);
        }

        private void PositionAt(RectTransform visual, Vector2 screenPosition)
        {
            if (visual == null || canvasRect == null)
                return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, uiCamera, out Vector2 localPoint))
                visual.anchoredPosition = localPoint;
        }
    }
}
