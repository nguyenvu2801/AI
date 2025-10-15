using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class SelectorNode : BTNode
{
    private List<BTNode> children = new List<BTNode>();
    public SelectorNode(params BTNode[] nodes) { children.AddRange(nodes); }
    public override NodeState Tick(Blackboard blackboard)
    {
        foreach (var child in children)
        {
            var state = child.Tick(blackboard);
            if (state == NodeState.Running) return NodeState.Running;
            if (state == NodeState.Success) return NodeState.Success;
        }
        return NodeState.Failure;
    }
}
