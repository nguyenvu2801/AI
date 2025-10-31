using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BallController : MonoBehaviour
{
    public Rigidbody2D rb;
    public float kickForce = 8f;
    public PlayerAgent currentHolder;
    void Awake() { rb = GetComponent<Rigidbody2D>(); }

    void Update()
    {
        if (currentHolder != null)
        {
            // ball follows holder (small offset)
            rb.position = currentHolder.transform.position + (Vector3)(currentHolder.facing * 0.35f);
            rb.velocity = Vector2.zero;
        }
        else
        {
            // rolling physics
        }
        if (Blackboard.Instance != null) Blackboard.Instance.ballPosition = rb.position;
    }

    public void GiveTo(PlayerAgent agent)
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