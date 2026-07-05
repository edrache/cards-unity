using System;
using System.Collections.Generic;
using Obi;
using UnityEngine;

namespace CardsUnity
{
    public class SockDragController : MonoBehaviour
    {
        [SerializeField] private ObiActor sockActor;
        [SerializeField] private Camera pointerCamera;
        [SerializeField] private Transform dragger;
        [SerializeField] private float hoverRadiusPixels = 60f;

        public Transform Dragger => dragger;

        public event Action<Vector2> HoverStarted;
        public event Action<Vector2> HoverUpdated;
        public event Action HoverEnded;
        public event Action<Vector2> DragStarted;
        public event Action<Vector2> DragUpdated;
        public event Action DragEnded;

        private readonly List<SockGrabAttachmentHandle> handles = new List<SockGrabAttachmentHandle>();
        private readonly HashSet<ObiParticleAttachment> registeredAttachments = new HashSet<ObiParticleAttachment>();
        private readonly HashSet<ObiActor> registeredActors = new HashSet<ObiActor>();
        private Vector2[] handleScreenPositions = Array.Empty<Vector2>();
        private SockGrabStateMachine stateMachine = new SockGrabStateMachine();
        private Vector3 dragPlanePoint;
        private Vector3 dragPlaneNormal;
        private Vector3 lastPointerCameraPosition;

        private void Awake()
        {
            RegisterSockActor(sockActor);
            DisableAllAttachments();
        }

        private void OnDisable()
        {
            bool wasDragging = stateMachine.CurrentState == SockGrabState.Dragging;
            bool wasHovering = stateMachine.CurrentState == SockGrabState.Hovering;

            DisableAllAttachments();
            stateMachine = new SockGrabStateMachine();

            if (wasDragging)
                DragEnded?.Invoke();

            if (wasHovering || wasDragging)
                HoverEnded?.Invoke();
        }

        private void OnValidate()
        {
            hoverRadiusPixels = Mathf.Max(0f, hoverRadiusPixels);
        }

        private void Update()
        {
            if (!HasRuntimeDependencies())
                return;

            Vector2 pointerScreenPosition = Input.mousePosition;

            if (stateMachine.CurrentState != SockGrabState.Dragging)
                UpdateHover(pointerScreenPosition);

            if (Input.GetMouseButtonDown(0) && stateMachine.CurrentState == SockGrabState.Hovering)
            {
                BeginDrag(pointerScreenPosition);
                return;
            }

            if (stateMachine.CurrentState != SockGrabState.Dragging)
                return;

            if (Input.GetMouseButtonUp(0))
            {
                EndDrag();
                return;
            }

            UpdateDrag(pointerScreenPosition);
        }

        private bool HasRuntimeDependencies()
        {
            if (pointerCamera == null || dragger == null)
                return false;

            EnsureScreenPositionBuffer();
            return handles.Count > 0;
        }

        public void RegisterSockActor(ObiActor actor)
        {
            if (actor == null || !registeredActors.Add(actor))
                return;

            ObiParticleAttachment[] attachments = actor.GetComponents<ObiParticleAttachment>();
            for (int i = 0; i < attachments.Length; i++)
            {
                ObiParticleAttachment attachment = attachments[i];
                if (attachment == null || !registeredAttachments.Add(attachment))
                    continue;

                handles.Add(new SockGrabAttachmentHandle(attachment));
            }

            EnsureScreenPositionBuffer();
        }

        public void BindSockActorToDragger(ObiActor actor)
        {
            if (actor == null || dragger == null)
                return;

            ObiParticleAttachment[] attachments = actor.GetComponents<ObiParticleAttachment>();
            for (int i = 0; i < attachments.Length; i++)
            {
                if (attachments[i] != null)
                    attachments[i].target = dragger;
            }

            RegisterSockActor(actor);
        }

        public void UnregisterSockActor(ObiActor actor)
        {
            if (actor == null || !registeredActors.Remove(actor))
                return;

            ObiParticleAttachment[] attachments = actor.GetComponents<ObiParticleAttachment>();
            for (int i = handles.Count - 1; i >= 0; i--)
            {
                ObiParticleAttachment attachment = handles[i].Attachment;
                for (int j = 0; j < attachments.Length; j++)
                {
                    if (attachment != attachments[j])
                        continue;

                    registeredAttachments.Remove(attachment);
                    handles.RemoveAt(i);
                    break;
                }
            }

            EnsureScreenPositionBuffer();
        }

        private void EnsureScreenPositionBuffer()
        {
            if (handleScreenPositions.Length != handles.Count)
                handleScreenPositions = new Vector2[handles.Count];
        }

        private void UpdateHover(Vector2 pointerScreenPosition)
        {
            for (int i = 0; i < handles.Count; i++)
                handleScreenPositions[i] = GetHandleScreenPosition(handles[i]);

            SockGrabState previousState = stateMachine.CurrentState;
            int previousHandleIndex = stateMachine.ActiveHandleIndex;
            int hoveredIndex = SockGrabHoverDetector.FindClosestHandleWithinRadius(pointerScreenPosition, handleScreenPositions, hoverRadiusPixels);
            stateMachine.OnPointerMoved(hoveredIndex);

            if (previousState != SockGrabState.Hovering && stateMachine.CurrentState == SockGrabState.Hovering)
            {
                HoverStarted?.Invoke(pointerScreenPosition);
                return;
            }

            if (previousState == SockGrabState.Hovering && stateMachine.CurrentState == SockGrabState.Idle)
            {
                HoverEnded?.Invoke();
                return;
            }

            if (stateMachine.CurrentState == SockGrabState.Hovering)
            {
                if (previousHandleIndex != stateMachine.ActiveHandleIndex)
                    HoverStarted?.Invoke(pointerScreenPosition);
                else
                    HoverUpdated?.Invoke(pointerScreenPosition);
            }
        }

        private void BeginDrag(Vector2 pointerScreenPosition)
        {
            if (!TryGetActiveHandle(out SockGrabAttachmentHandle handle))
                return;

            Vector3 grabWorldPosition = handle.GetWorldPosition();
            dragPlanePoint = grabWorldPosition;
            dragPlaneNormal = -pointerCamera.transform.forward;
            lastPointerCameraPosition = pointerCamera.transform.position;

            dragger.position = grabWorldPosition;
            handle.Attachment.enabled = true;
            if (!stateMachine.TryBeginDrag())
            {
                handle.Attachment.enabled = false;
                return;
            }

            DragStarted?.Invoke(pointerScreenPosition);
        }

        private void UpdateDrag(Vector2 pointerScreenPosition)
        {
            Vector3 cameraPosition = pointerCamera.transform.position;
            Vector3 cameraDelta = cameraPosition - lastPointerCameraPosition;
            dragPlanePoint += cameraDelta;
            dragPlaneNormal = -pointerCamera.transform.forward;
            lastPointerCameraPosition = cameraPosition;

            Ray ray = pointerCamera.ScreenPointToRay(pointerScreenPosition);
            if (SockGrabMath.TryGetPointOnPlane(ray, dragPlanePoint, dragPlaneNormal, out Vector3 worldPoint))
                dragger.position = worldPoint;

            DragUpdated?.Invoke(pointerScreenPosition);
        }

        private void EndDrag()
        {
            if (TryGetActiveHandle(out SockGrabAttachmentHandle handle) && handle.Attachment != null)
                handle.Attachment.enabled = false;

            stateMachine.EndDrag();
            DragEnded?.Invoke();
        }

        private bool TryGetActiveHandle(out SockGrabAttachmentHandle handle)
        {
            int activeHandleIndex = stateMachine.ActiveHandleIndex;
            if (activeHandleIndex >= 0 && activeHandleIndex < handles.Count)
            {
                handle = handles[activeHandleIndex];
                return handle != null && handle.Attachment != null;
            }

            handle = null;
            return false;
        }

        private void DisableAllAttachments()
        {
            for (int i = 0; i < handles.Count; i++)
            {
                ObiParticleAttachment attachment = handles[i]?.Attachment;
                if (attachment != null)
                    attachment.enabled = false;
            }
        }

        private Vector2 GetHandleScreenPosition(SockGrabAttachmentHandle handle)
        {
            if (handle == null)
                return new Vector2(float.PositiveInfinity, float.PositiveInfinity);

            Vector3 screenPosition = pointerCamera.WorldToScreenPoint(handle.GetWorldPosition());
            if (screenPosition.z <= 0f)
                return new Vector2(float.PositiveInfinity, float.PositiveInfinity);

            return new Vector2(screenPosition.x, screenPosition.y);
        }

    }
}
