using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class ConditionNode : BTNode
{
    public System.Func<Blackboard, bool> condition;
    public ConditionNode(System.Func<Blackboard, bool> cond) { condition = cond; }
    public override NodeState Tick(Blackboard blackboard) => condition(blackboard) ? NodeState.Success : NodeState.Failure;
}
