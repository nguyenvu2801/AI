using UnityEngine;

public interface IFootballAgent
{
    Blackboard.Team team { get; }
    Role role { get; }
    Rigidbody2D rb { get; }
    Vector2 facing { get; }
    Transform transform { get; }
    void OnGainBall(BallController ball);
}