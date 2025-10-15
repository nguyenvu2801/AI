using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class ActionNode : BTNode
{
    public System.Func<Blackboard, NodeState> action;
    public ActionNode(System.Func<Blackboard, NodeState> act) { action = act; }
    public override NodeState Tick(Blackboard blackboard) => action(blackboard);
}
