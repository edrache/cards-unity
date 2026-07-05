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
        [Header("Grab Reposition")]
        [SerializeField] private float grabDistanceFromCamera = 1.5f;
        [SerializeField] private float repositionWhenFartherThan = 2f;
        [SerializeField] private float grabRepositionDuration = 0.2f;
        [SerializeField] private GrabRepositionEase grabRepositionEase = GrabRepositionEase.EaseOutCubic;

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
        private bool isRepositioningDragger;
        private float repositionElapsed;
        private Vector3 repositionStartPosition;

        private enum GrabRepositionEase
        {
            Linear,
            EaseOutQuad,
            EaseInOutQuad,
            EaseOutCubic,
            EaseInOutCubic
        }

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
            isRepositioningDragger = false;
            repositionElapsed = 0f;

            if (wasDragging)
                DragEnded?.Invoke();

            if (wasHovering || wasDragging)
                HoverEnded?.Invoke();
        }

        private void OnValidate()
        {
            hoverRadiusPixels = Mathf.Max(0f, hoverRadiusPixels);
            grabDistanceFromCamera = Mathf.Max(0f, grabDistanceFromCamera);
            repositionWhenFartherThan = Mathf.Max(0f, repositionWhenFartherThan);
            grabRepositionDuration = Mathf.Max(0f, grabRepositionDuration);
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

            BeginDraggerReposition(pointerScreenPosition, grabWorldPosition);

            DragStarted?.Invoke(pointerScreenPosition);
        }

        private void UpdateDrag(Vector2 pointerScreenPosition)
        {
            Vector3 cameraPosition = pointerCamera.transform.position;
            Vector3 cameraDelta = cameraPosition - lastPointerCameraPosition;
            dragPlanePoint += cameraDelta;
            dragPlaneNormal = -pointerCamera.transform.forward;
            lastPointerCameraPosition = cameraPosition;

            if (isRepositioningDragger)
            {
                UpdateDraggerReposition(pointerScreenPosition);
                DragUpdated?.Invoke(pointerScreenPosition);
                return;
            }

            Ray ray = pointerCamera.ScreenPointToRay(pointerScreenPosition);
            if (SockGrabMath.TryGetPointOnPlane(ray, dragPlanePoint, dragPlaneNormal, out Vector3 worldPoint))
                dragger.position = worldPoint;

            DragUpdated?.Invoke(pointerScreenPosition);
        }

        private void EndDrag()
        {
            if (TryGetActiveHandle(out SockGrabAttachmentHandle handle) && handle.Attachment != null)
                handle.Attachment.enabled = false;

            isRepositioningDragger = false;
            repositionElapsed = 0f;
            stateMachine.EndDrag();
            DragEnded?.Invoke();
        }

        private void BeginDraggerReposition(Vector2 pointerScreenPosition, Vector3 grabWorldPosition)
        {
            repositionStartPosition = grabWorldPosition;
            repositionElapsed = 0f;
            bool hasTargetPoint = TryGetPointAtCameraDistance(pointerScreenPosition, out Vector3 targetPoint);
            float distanceFromCamera = Vector3.Distance(pointerCamera.transform.position, grabWorldPosition);

            if (distanceFromCamera <= repositionWhenFartherThan)
            {
                isRepositioningDragger = false;
                dragPlanePoint = dragger.position;
                return;
            }

            if (grabRepositionDuration <= 0f || !hasTargetPoint)
            {
                isRepositioningDragger = false;
                if (hasTargetPoint)
                    dragger.position = targetPoint;
                dragPlanePoint = dragger.position;
                return;
            }

            isRepositioningDragger = true;
        }

        private void UpdateDraggerReposition(Vector2 pointerScreenPosition)
        {
            if (!TryGetPointAtCameraDistance(pointerScreenPosition, out Vector3 targetPoint))
            {
                isRepositioningDragger = false;
                dragPlanePoint = dragger.position;
                return;
            }

            repositionElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(repositionElapsed / grabRepositionDuration);
            float easedT = EvaluateEase(t);
            dragger.position = Vector3.LerpUnclamped(repositionStartPosition, targetPoint, easedT);

            if (t >= 1f)
            {
                isRepositioningDragger = false;
                dragPlanePoint = dragger.position;
            }
        }

        private bool TryGetPointAtCameraDistance(Vector2 pointerScreenPosition, out Vector3 worldPoint)
        {
            if (pointerCamera == null)
            {
                worldPoint = dragger != null ? dragger.position : Vector3.zero;
                return false;
            }

            Ray ray = pointerCamera.ScreenPointToRay(pointerScreenPosition);
            worldPoint = ray.GetPoint(grabDistanceFromCamera);
            return true;
        }

        private float EvaluateEase(float t)
        {
            switch (grabRepositionEase)
            {
                case GrabRepositionEase.EaseOutQuad:
                    return 1f - (1f - t) * (1f - t);
                case GrabRepositionEase.EaseInOutQuad:
                    return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
                case GrabRepositionEase.EaseOutCubic:
                    return 1f - Mathf.Pow(1f - t, 3f);
                case GrabRepositionEase.EaseInOutCubic:
                    return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
                default:
                    return t;
            }
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
