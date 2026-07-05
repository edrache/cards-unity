using Obi;
using UnityEngine;
using CardsUnity.UI;

namespace CardsUnity
{
    public class SockGrabAutoBinder : MonoBehaviour
    {
        [SerializeField] private SockDragController dragController;
        [SerializeField] private ObiActor sockActor;

        public static SockDragController FindPreferredController()
        {
            SockGrabCursorUI cursorUi = FindFirstObjectByType<SockGrabCursorUI>();
            if (cursorUi != null && cursorUi.DragController != null)
                return cursorUi.DragController;

            return FindFirstObjectByType<SockDragController>();
        }

        private void Awake()
        {
            BindIfNeeded();
        }

        private void OnDestroy()
        {
            if (dragController != null && sockActor != null)
                dragController.UnregisterSockActor(sockActor);
        }

        public void BindIfNeeded()
        {
            if (dragController == null)
                dragController = FindPreferredController();

            if (sockActor == null)
                sockActor = GetComponentInChildren<ObiActor>(true);

            if (dragController == null || sockActor == null)
                return;

            dragController.BindSockActorToDragger(sockActor);
        }
    }
}
