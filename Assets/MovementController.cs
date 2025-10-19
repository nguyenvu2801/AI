using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovementController : MonoBehaviour
{
    public float moveSpeed = 3f;
    Coroutine followRoutine;
    public bool IsFollowingPath => followRoutine != null;
    public void FollowPath(List<Vector3> path)
    {
        if (followRoutine != null) StopCoroutine(followRoutine);
        followRoutine = StartCoroutine(FollowPathCoroutine(path));
    }

    IEnumerator FollowPathCoroutine(List<Vector3> path)
    {
        foreach (var waypoint in path)
        {
            while ((transform.position - waypoint).sqrMagnitude > 0.01f)
            {
                transform.position = Vector3.MoveTowards(transform.position, waypoint, moveSpeed * Time.deltaTime);
                yield return null;
            }
            // small pause between nodes (optional)
            yield return null;
        }

        followRoutine = null;
    }
}
