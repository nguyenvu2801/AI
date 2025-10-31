using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BTStatus { Success, Failure, Running }

public abstract class BTNode
{
    public abstract BTStatus Tick();
}