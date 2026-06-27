using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class ZlodziejFlee : MonoBehaviour
{
    [SerializeField] Transform policjant;
    [SerializeField] float updateRate = 0.25f;
    [SerializeField] float fleeRadius = 10f;
    [SerializeField] int candidateCount = 12;
    [SerializeField] float dangerRadius = 5f;   // below this distance thief panics and uses more candidates
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

        if (_agent.velocity.x != 0f)
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (_agent.velocity.x < 0f ? -1f : 1f);
            transform.localScale = scale;
        }
    }

    void UpdateFleeDestination()
    {
        Vector3 myPos = transform.position;
        Vector3 threatPos = policjant.position;
        float threatDist = Vector3.Distance(myPos, threatPos);

        // More candidates when policjant is close
        int samples = threatDist < dangerRadius ? candidateCount * 2 : candidateCount;

        Vector3 bestPoint = myPos;
        float bestScore = float.MinValue;

        for (int i = 0; i < samples; i++)
        {
            // Spread candidates evenly around 360°, offset by index so we cover all directions
            float angle = (360f / samples) * i;
            Vector3 dir = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            Vector3 candidate = myPos + dir * fleeRadius;

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, fleeRadius * 0.5f, NavMesh.AllAreas))
                continue;

            // Check the path is actually reachable
            NavMeshPath path = new NavMeshPath();
            if (!_agent.CalculatePath(hit.position, path))
                continue;
            if (path.status != NavMeshPathStatus.PathComplete)
                continue;

            float score = Score(hit.position, threatPos);
            if (score > bestScore)
            {
                bestScore = score;
                bestPoint = hit.position;
            }
        }

        _agent.SetDestination(bestPoint);
    }

    float Score(Vector3 candidate, Vector3 threatPos)
    {
        // Distance from threat — further is better
        float distFromThreat = Vector3.Distance(candidate, threatPos);

        // Alignment with "away" direction — dot product in [−1, 1], scaled up
        Vector3 awayDir = (transform.position - threatPos).normalized;
        Vector3 candidateDir = (candidate - transform.position).normalized;
        float alignment = Vector3.Dot(awayDir, candidateDir); // −1..1

        return distFromThreat + alignment * 3f;
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
