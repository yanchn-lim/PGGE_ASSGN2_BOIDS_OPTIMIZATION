using UnityEngine;
using Unity.Burst;

public class Obstacle : MonoBehaviour
{
    public float AvoidanceRadiusMultFactor = 1.5f;
    public float AvoidanceRadius
    {
        get
        {
            return mCollider.radius * 3 * AvoidanceRadiusMultFactor;
        }
    }

    public CircleCollider2D mCollider;
}

[BurstCompile]
public struct ObstacleData
{
    public Vector3 pos;
    public float avoidRad;
}
