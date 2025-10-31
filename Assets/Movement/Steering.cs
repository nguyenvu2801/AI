using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Steering
{
    public static Vector2 Seek(Vector2 fromPos, Vector2 targetPos, Rigidbody2D rb, float maxSpeed, float slowingDistance = 0.5f)
    {
        Vector2 desired = targetPos - fromPos;
        float distance = desired.magnitude;
        if (distance <= 0.01f) return Vector2.zero;
        float speed = maxSpeed;
        if (distance < slowingDistance) speed = maxSpeed * (distance / slowingDistance);
        desired = desired.normalized * speed;
        Vector2 steer = desired - rb.velocity;
        return steer;
    }
}
