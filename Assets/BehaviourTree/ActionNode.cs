using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActionNode : BTNode
{
    private Func<BTStatus> action;
    public ActionNode(Func<BTStatus> actionFunc) { action = actionFunc; }
    public override BTStatus Tick() => action();
}
