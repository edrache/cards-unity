using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class PolicjantChase : MonoBehaviour
{
    [SerializeField] Transform zlodziej;
    [SerializeField] float catchDistance = 0.8f;
    [SerializeField] Animator animator;

    NavMeshAgent _agent;
    bool _caught;

    static readonly int SpeedHash = Animator.StringToHash("Speed");

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        if (_caught || zlodziej == null) return;

        _agent.SetDestination(zlodziej.position);

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

        if (Vector3.Distance(transform.position, zlodziej.position) <= catchDistance)
            Catch();
    }

    void Catch()
    {
        _caught = true;
        _agent.isStopped = true;
        Debug.Log("Zlodziej zlапany!");

        ZlodziejFlee flee = zlodziej.GetComponent<ZlodziejFlee>();
        if (flee != null) flee.OnCaught();
    }
}
