using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BallController : MonoBehaviour
{
    public Rigidbody2D rb;
    public float kickForce = 8f;
    public PlayerAgent currentHolder;
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f; // Fix: Disable gravity
        rb.drag = 0.5f; // Fix: Add drag for natural slowdown
    }

    void Update()
    {
        if (currentHolder != null)
        {
            // Ball follows holder (small offset)
            rb.position = currentHolder.transform.position + (Vector3)(currentHolder.facing * 0.35f);
            rb.velocity = Vector2.zero;
        }
        else
        {
            // Rolling physics (drag handles slowdown)
        }
        if (Blackboard.Instance != null) Blackboard.Instance.ballPosition = rb.position;
    }

    public void GiveTo(PlayerAgent agent)
    {
        currentHolder = agent;
        agent.OnGainBall(this);
        Debug.Log("aaaa");
    }

    public void ReleaseWithForce(Vector2 direction, float power)
    {
        currentHolder = null;
        rb.velocity = direction.normalized * power;
    }

    public void KickTowards(Vector2 target, float power)
    {
        Debug.Log("aa");
        Vector2 dir = (target - (Vector2)transform.position).normalized;
        ReleaseWithForce(dir, power * kickForce);
    }
}