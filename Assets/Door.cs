using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Door : MonoBehaviour
{
    public Blackboard blackboard;
    public Collider2D doorCollider;
    public SpriteRenderer spriteRenderer;

    void Awake()
    {
        if (blackboard == null)
        {
            var agent = FindObjectOfType<Blackboard>();
            blackboard = agent;
        }
        if (doorCollider == null) doorCollider = GetComponent<Collider2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        if (blackboard != null)
            blackboard.doorPosition = transform.position;
    }

    void Update()
    {
        if (blackboard != null && blackboard.hasKey)
        {
            // unlock
            if (doorCollider != null) doorCollider.enabled = false;
            // optional visual
            if (spriteRenderer != null) spriteRenderer.color = Color.green;
        }
    }
}
