using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class FSM_BallController : MonoBehaviour
{
    [Header("Ball feel")]
    [Tooltip("How tightly the ball sticks to the holder each physics step")]
    [Range(5f, 30f)] public float followStrength = 20f;

    [Tooltip("Distance in front of the holder the ball is positioned")]
    public float carryOffset = 0.45f;

    [Tooltip("Drag applied when the ball is loose (no holder)")]
    public float looseDrag = 2.5f;

    [Tooltip("Drag applied when the ball is held (effectively stops it drifting)")]
    public float heldDrag = 30f;

    [Header("Goal detection")]
    [Tooltip("Tag on goal trigger colliders — e.g. 'GoalA' and 'GoalB'")]
    public string goalTagA = "GoalA";
    public string goalTagB = "GoalB";

    [HideInInspector] public FSM_PlayerAgent currentHolder = null;

    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
    }
    void FixedUpdate()
    {
        if (currentHolder == null) return;

        // Calculate where the ball SHOULD be
        Vector2 target = (Vector2)currentHolder.transform.position + currentHolder.facing * carryOffset;

        // Apply that velocity to the Rigidbody so it actually moves!
        rb.velocity = (target - rb.position) * followStrength;
    }

    public void KickTowards(Vector2 targetPos, float power)
    {
        Vector2 dir = (targetPos - rb.position).normalized;
        ReleaseWithForce(dir, power);
    }

    public void ReleaseWithForce(Vector2 direction, float power)
    {
        Release();
        rb.AddForce(direction * power, ForceMode2D.Impulse); // Added the actual force
    }

    public void GiveTo(FSM_PlayerAgent newHolder)
    {
        if (newHolder == null) return;

        currentHolder = newHolder;
        rb.drag = heldDrag;
        newHolder.OnGainBall(this);
    }

    public void Release()
    {
        currentHolder = null;
        rb.drag = looseDrag;
    }

    void OnTriggerEnter2D(Collider2D col)
    {
        var bb = FSM_Blackboard.Instance;
        if (bb == null) return;

        if (col.CompareTag(goalTagA))
        {
            bb.AddGoal(FSM_Blackboard.Team.B);
            bb.TriggerGoalReset();
            ResetBall();
        }
        else if (col.CompareTag(goalTagB))
        {
            bb.AddGoal(FSM_Blackboard.Team.A);
            bb.TriggerGoalReset();
            ResetBall();
        }
    }

    [Header("Reset")]
    public Transform centrePoint;

    public void ResetBall()
    {
        Release();
        // CRITICAL: Stop the ball from moving!
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;

        transform.position = centrePoint != null ? centrePoint.position : Vector3.zero;

        // Optional: Forces the physics engine to stop calculating for 1 frame
        rb.Sleep();
    }
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (currentHolder == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, currentHolder.transform.position);
    }
#endif
}