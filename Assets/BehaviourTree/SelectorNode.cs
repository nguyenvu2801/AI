using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SelectorNode : BTNode
{
    private List<BTNode> children = new List<BTNode>();
    public SelectorNode(params BTNode[] nodes) { children.AddRange(nodes); }
    public override BTStatus Tick()
    {
        for (int i = 0; i < children.Count; i++)
        {
            var status = children[i].Tick();
            if (status != BTStatus.Failure) return status; // Running or Success stops here
        }
        return BTStatus.Failure;
    }
}