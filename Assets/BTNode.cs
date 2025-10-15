using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
public enum NodeState { Success, Failure, Running }
public abstract class BTNode
{
    public abstract NodeState Tick(Blackboard blackboard);
}