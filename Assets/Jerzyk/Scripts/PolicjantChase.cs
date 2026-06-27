using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class PolicjantChase : MonoBehaviour
{
    [SerializeField] Transform zlodziej;
    [SerializeField] float updateRate = 0.15f;
    [SerializeField] float interceptLookahead = 1.2f;  // seconds ahead to predict thief position
    [SerializeField] Animator animator;

    NavMeshAgent _agent;
    Vector3 _prevZlodziejPos;
    float _timer;
    bool _caught;

    static readonly int SpeedHash = Animator.StringToHash("Speed");

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.updateRotation = false;
        _agent.updateUpAxis = false;
    }

    void OnEnable()
    {
        if (zlodziej != null)
            _prevZlodziejPos = zlodziej.position;
    }

    void Update()
    {
        if (_caught || zlodziej == null) return;

        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            _timer = updateRate;
            UpdateChaseDestination();
        }

        float speed = _agent.velocity.magnitude / _agent.speed;
        if (animator != null)
            animator.SetFloat(SpeedHash, speed);

        if (_agent.velocity.x != 0f)
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (_agent.velocity.x < 0f ? -1f : 1f);
            transform.localScale = scale;
        }

        _prevZlodziejPos = zlodziej.position;
    }

    void UpdateChaseDestination()
    {
        // Estimate thief velocity from position delta
        Vector3 zlodziejVelocity = (zlodziej.position - _prevZlodziejPos) / updateRate;

        // Predict where thief will be in interceptLookahead seconds
        Vector3 predictedPos = zlodziej.position + zlodziejVelocity * interceptLookahead;

        // Verify predicted point is on NavMesh, fall back to current position
        if (NavMesh.SamplePosition(predictedPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            NavMeshPath path = new NavMeshPath();
            if (_agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
            {
                _agent.SetDestination(hit.position);
                return;
            }
        }

        _agent.SetDestination(zlodziej.position);
    }

    public void OnCaught()
    {
        _caught = true;
        _agent.isStopped = true;
    }

    public void OnUncaught()
    {
        _caught = false;
    }
}
