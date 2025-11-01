using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "TacticalKick/TeamTactic")]
public class TeamTactic : ScriptableObject
{
    public Formation formation;
    public string tacticName = "Balanced";
    [Range(-1f, 1f)] public float attackBias = 0f; // +0.25 for Aggressive, -0.25 for Defensive
    public static TeamTactic Aggressive => CreateInstance<TeamTactic>().Init("Aggressive", 0.25f);
    public static TeamTactic Defensive => CreateInstance<TeamTactic>().Init("Defensive", -0.25f);
    public static TeamTactic Balanced => CreateInstance<TeamTactic>().Init("Balanced", 0f);

    TeamTactic Init(string name, float bias) { tacticName = name; attackBias = bias; return this; }
}