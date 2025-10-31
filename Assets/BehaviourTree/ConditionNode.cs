using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConditionNode : BTNode
{
    private Func<bool> condition;
    public ConditionNode(Func<bool> cond) { condition = cond; }
    public override BTStatus Tick() => condition() ? BTStatus.Success : BTStatus.Failure;
}
