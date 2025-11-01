using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "Formation", menuName = "Soccer/Formation", order = 1)]
public class Formation : ScriptableObject
{
    [Header("Info")]
    public string formationName = "4-3-3";
    [TextArea(3, 5)]
    public string description = "Standard attacking formation.";

    [Header("Relative Positions (from teamCenter, for Team A)")]
    public Vector2[] positions = new Vector2[11]; // Edit these in Inspector! See examples below

    [Header("Roles")]
    public Role[] roles = new Role[11];
}
