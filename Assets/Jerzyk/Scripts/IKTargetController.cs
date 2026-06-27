using Rewired;
using UnityEngine;

public class IKTargetController : MonoBehaviour
{
    [Header("IK Targets")]
    public Transform leftHandTarget;
    public Transform rightHandTarget;
    public Transform leftLegTarget;
    public Transform rightLegTarget;

    [Header("IK Anchors (first bone of each chain)")]
    public Transform leftHandAnchor;
    public Transform rightHandAnchor;
    public Transform leftLegAnchor;
    public Transform rightLegAnchor;

    [Header("Rewired Action Names")]
    public string leftHandHorizontalAction  = "LeftHand_Horizontal";
    public string leftHandVerticalAction    = "LeftHand_Vertical";
    public string rightHandHorizontalAction = "RightHand_Horizontal";
    public string rightHandVerticalAction   = "RightHand_Vertical";
    public string leftLegHorizontalAction   = "LeftLeg_Horizontal";
    public string leftLegVerticalAction     = "LeftLeg_Vertical";
    public string rightLegHorizontalAction  = "RightLeg_Horizontal";
    public string rightLegVerticalAction    = "RightLeg_Vertical";

    [Header("Settings")]
    public int playerId = 0;
    [Tooltip("Units per second")]
    public float moveSpeed = 2f;

    [Header("Max Radius per Target")]
    public float leftHandMaxRadius  = 1f;
    public float rightHandMaxRadius = 1f;
    public float leftLegMaxRadius   = 1f;
    public float rightLegMaxRadius  = 1f;

    private Player _player;

    private void Start()
    {
        _player = ReInput.players.GetPlayer(playerId);
    }

    private void Update()
    {
        MoveTarget(leftHandTarget,  leftHandAnchor,  leftHandHorizontalAction,  leftHandVerticalAction,  leftHandMaxRadius);
        MoveTarget(rightHandTarget, rightHandAnchor, rightHandHorizontalAction, rightHandVerticalAction, rightHandMaxRadius);
        MoveTarget(leftLegTarget,   leftLegAnchor,   leftLegHorizontalAction,   leftLegVerticalAction,   leftLegMaxRadius);
        MoveTarget(rightLegTarget,  rightLegAnchor,  rightLegHorizontalAction,  rightLegVerticalAction,  rightLegMaxRadius);
    }

    private void MoveTarget(Transform target, Transform anchor, string axisH, string axisV, float maxRadius)
    {
        if (target == null || anchor == null) return;

        float h = _player.GetAxis(axisH);
        float v = _player.GetAxis(axisV);

        Vector3 delta = new Vector3(h, v, 0f) * (moveSpeed * Time.deltaTime);
        Vector3 newPos = target.position + delta;

        Vector3 offset = newPos - anchor.position;
        if (offset.magnitude > maxRadius)
            offset = offset.normalized * maxRadius;

        target.position = anchor.position + offset;
    }
}
