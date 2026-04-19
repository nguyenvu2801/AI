using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BallController : MonoBehaviour
{
    public Rigidbody2D rb;
    public float kickForce = 8f;
    public IFootballAgent currentHolder;

    private float ignoreSameHolderUntil = 0f;   // prevent immediate re-pickup

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.drag = 0.5f;
        rb.isKinematic = false;   // start free
    }

    void LateUpdate()
    {
        if (currentHolder != null)
        {
            // When held, move via Transform (kinematic bodies ignore physics position)
            transform.position = currentHolder.transform.position + (Vector3)(currentHolder.facing * 0.35f);
            rb.velocity = Vector2.zero;   // safety
        }
        if (Blackboard.Instance != null)
            Blackboard.Instance.ballPosition = transform.position;
    }

    public void GiveTo(IFootballAgent agent)
    {
        if (agent == null) return;

        currentHolder = agent;
        rb.isKinematic = true;
        rb.velocity = Vector2.zero;

        //tiny delay before this player can pick it up again
        ignoreSameHolderUntil = Time.time + 0.12f;

        agent.OnGainBall(this);
    }

    public void ReleaseWithForce(Vector2 direction, float power)
    {
        if (currentHolder == null) return;

        // Clear holder 
        var oldHolder = currentHolder;
        currentHolder = null;

        rb.isKinematic = false;
        rb.velocity = direction.normalized * Mathf.Max(power, 0.1f);   // prevent zero-velocity kick

        ignoreSameHolderUntil = Time.time + 0.18f;   // grace period

        //  visual spin
        rb.angularVelocity = Random.Range(-220f, 220f);
    }

    public void KickTowards(Vector2 target, float power)
    {
        if (currentHolder == null) return;

        Vector2 dir = (target - (Vector2)transform.position).normalized;
        ReleaseWithForce(dir, power * kickForce);
    }


    void OnTriggerEnter2D(Collider2D col)
    {
        if (Time.time < ignoreSameHolderUntil) return;
        if (currentHolder != null) return;   

        var agent = col.GetComponent<IFootballAgent>();
        if (agent != null)
        {
            GiveTo(agent);
        }
    }
}