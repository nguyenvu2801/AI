using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BallController : MonoBehaviour
{
    public Rigidbody2D rb;
    public float kickForce = 8f;
    public IFootballAgent currentHolder;

    private bool isKinematicWhenHeld = true; // new

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.drag = 0.5f;
        rb.isKinematic = false; // start free
    }

    // NEW: better to use LateUpdate for visual following
    void LateUpdate()
    {
        if (currentHolder != null)
        {
            // Kinematic bodies should be moved via transform.position
            transform.position = currentHolder.transform.position + (Vector3)(currentHolder.facing * 0.35f);
        }

        if (Blackboard.Instance != null)
            Blackboard.Instance.ballPosition = transform.position;
    }

    public void GiveTo(IFootballAgent agent)
    {
        currentHolder = agent;
        rb.isKinematic = true;           
        rb.velocity = Vector2.zero;
        agent.OnGainBall(this);
    }

    public void ReleaseWithForce(Vector2 direction, float power)
    {
        currentHolder = null;
        rb.isKinematic = false;          
        rb.velocity = direction.normalized * power;
    }

    public void KickTowards(Vector2 target, float power)
    {
        Vector2 dir = (target - (Vector2)transform.position).normalized;
        ReleaseWithForce(dir, power * kickForce);
    }
}