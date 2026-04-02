using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BallController : MonoBehaviour
{
    public Rigidbody2D rb;
    public float kickForce = 8f;
    public IFootballAgent currentHolder;  

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.drag = 0.5f;
    }

    void Update()
    {
        if (currentHolder != null)
        {
            rb.position = currentHolder.transform.position + (Vector3)(currentHolder.facing * 0.35f);
            rb.velocity = Vector2.zero;
        }

        if (Blackboard.Instance != null)
            Blackboard.Instance.ballPosition = rb.position;
    }

    public void GiveTo(IFootballAgent agent)   
    {
        currentHolder = agent;
        agent.OnGainBall(this);
    }

    public void ReleaseWithForce(Vector2 direction, float power)
    {
        currentHolder = null;
        rb.velocity = direction.normalized * power;
    }

    public void KickTowards(Vector2 target, float power)
    {
        Vector2 dir = (target - (Vector2)transform.position).normalized;
        ReleaseWithForce(dir, power * kickForce);
    }
}