using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(fileName = "RoleStats", menuName = "RoleStats/Role Stats", order = 2)]
public class RoleStats : ScriptableObject
{
    public Role role;

    [Header("Core Attributes")]
    public float maxSpeed = 4f;
    public float kickPower = 6f;
    public float shootDistance = 8f;
    public float tackleChance = 0.5f;
    public float pressDistance = 4f;
}