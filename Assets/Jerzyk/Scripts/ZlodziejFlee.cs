using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class ZlodziejFlee : MonoBehaviour
{
    [SerializeField] Transform policjant;
    [SerializeField] float fleeDistance = 8f;
    [SerializeField] float updateRate = 0.2f;
    [SerializeField] Animator animator;

    NavMeshAgent _agent;
    float _timer;
    bool _caught;

    static readonly int SpeedHash = Animator.StringToHash("Speed");

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.updateRotation = false;
        _agent.updateUpAxis = false;
    }

    void Update()
    {
        if (_caught || policjant == null) return;

        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            _timer = updateRate;
            UpdateFleeDestination();
        }

        float speed = _agent.velocity.magnitude / _agent.speed;
        if (animator != null)
            animator.SetFloat(SpeedHash, speed);

        // Flip sprite based on movement direction
        if (_agent.velocity.x != 0f)
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (_agent.velocity.x < 0f ? -1f : 1f);
            transform.localScale = scale;
        }
    }

    void UpdateFleeDestination()
    {
        Vector3 dirAway = (transform.position - policjant.position).normalized;
        Vector3 fleeTarget = transform.position + dirAway * fleeDistance;

        // Sample valid NavMesh point near the flee target
        if (NavMesh.SamplePosition(fleeTarget, out NavMeshHit hit, fleeDistance, NavMesh.AllAreas))
            _agent.SetDestination(hit.position);
    }

    public void OnCaught()
    {
        _caught = true;
        _agent.isStopped = true;
        if (animator != null)
            animator.SetFloat(SpeedHash, 0f);
    }

    public void OnUncaught()
    {
        _caught = false;
    }
}
