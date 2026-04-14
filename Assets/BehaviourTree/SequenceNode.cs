using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SequenceNode : BTNode
{
    private List<BTNode> children = new List<BTNode>();
    public SequenceNode(params BTNode[] nodes) { children.AddRange(nodes); }
    public override BTStatus Tick()
    {
        for (int i = 0; i < children.Count; i++)
        {
            var status = children[i].Tick();
            if (status != BTStatus.Success) return status; // Running or Failure stops here
        }
        return BTStatus.Success;
    }
}
