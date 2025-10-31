using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SelectorNode : BTNode
{
    private List<BTNode> children = new List<BTNode>();
    private int current = 0;
    public SelectorNode(params BTNode[] nodes) { children.AddRange(nodes); current = 0; }
    public override BTStatus Tick()
    {
        while (current < children.Count)
        {
            var status = children[current].Tick();
            if (status == BTStatus.Running) return BTStatus.Running;
            if (status == BTStatus.Success) { current = 0; return BTStatus.Success; }
            current++;
        }
        current = 0;
        return BTStatus.Failure;
    }
}