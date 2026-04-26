using Rewired;
using UnityEngine;

namespace CardsUnity
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private float _moveSpeed  = 10f;
        [SerializeField] private float _zoomSpeed  = 5f;
        [SerializeField] private float _minHeight  = 2f;
        [SerializeField] private float _maxHeight  = 50f;
        [SerializeField] private float _baseHeight = 10f;
        [SerializeField] private Camera _camera;

        private Player   _player;
        private Vector3  _dragWorldOrigin;
        private readonly Plane _groundPlane = new Plane(Vector3.up, Vector3.zero);

        private void Update()
        {
            if (_player == null)
            {
                if (!ReInput.isReady) return;
                _player = ReInput.players.GetPlayer(0);
            }

            float horizontal = 0f;
            float vertical   = 0f;

            if (_player.GetButton("MoveRight")) horizontal += 1f;
            if (_player.GetButton("MoveLeft"))  horizontal -= 1f;
            if (_player.GetButton("MoveUp"))    vertical   += 1f;
            if (_player.GetButton("MoveDown"))  vertical   -= 1f;

            float heightScale = transform.position.y / _baseHeight;
            float scaledMove  = _moveSpeed * heightScale;
            Vector3 moveDelta = new Vector3(horizontal, 0f, vertical) * (scaledMove * Time.deltaTime);
            transform.position += moveDelta;

            HandleDrag();

            float zoom = 0f;
            if (_player.GetButton("ZoomIn"))  zoom -= 1f;
            if (_player.GetButton("ZoomOut")) zoom += 1f;

            float scaledZoom = _zoomSpeed * (transform.position.y / _baseHeight);
            float newY = Mathf.Clamp(transform.position.y + zoom * scaledZoom * Time.deltaTime, _minHeight, _maxHeight);
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }

        private void HandleDrag()
        {
            if (_player.GetButtonDown("MoveTrigger"))
                _dragWorldOrigin = ScreenToGroundPoint(Input.mousePosition);

            if (_player.GetButton("MoveTrigger"))
            {
                Vector3 currentGroundPoint = ScreenToGroundPoint(Input.mousePosition);
                Vector3 diff = _dragWorldOrigin - currentGroundPoint;
                transform.position += new Vector3(diff.x, 0f, diff.z);
            }
        }

        private Vector3 ScreenToGroundPoint(Vector3 screenPos)
        {
            Ray ray = _camera.ScreenPointToRay(screenPos);
            if (_groundPlane.Raycast(ray, out float distance))
                return ray.GetPoint(distance);
            return _dragWorldOrigin;
        }
    }
}
