using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KeyPickup : MonoBehaviour
{
    public Blackboard blackboard;
    void Awake()
    {
        if (blackboard == null)
        {
            var agent = FindObjectOfType<Blackboard>();
            blackboard = agent;
        }
        // Set key position so AI knows where it is (if we want that)
        if (blackboard != null)
            blackboard.keyPosition = transform.position;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var bb = other.GetComponent<Blackboard>();
        if (bb != null)
        {
            bb.hasKey = true;
            Debug.Log("Key collected!");
            Destroy(gameObject);
        }
    }
}
