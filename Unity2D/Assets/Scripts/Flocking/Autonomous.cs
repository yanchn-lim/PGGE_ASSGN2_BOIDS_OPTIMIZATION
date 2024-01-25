using Unity.Burst;
using UnityEngine;
using Unity.Mathematics;


public class Autonomous : MonoBehaviour
{
    public float MaxSpeed = 10.0f;
    public float Speed
    {
        get;
        private set;
    } = 0.0f;

    public Vector2 accel = new Vector2(0.0f, 0.0f);
    public float TargetSpeed = 0.0f;
    public Vector3 TargetDirection = Vector3.zero;
    public float RotationSpeed = 0.0f;

    public SpriteRenderer spriteRenderer;
    //public AutonomousType type;
    public Rect bounds;

    public FlockBehaviour baseFlock;

    #region Start functions
    // Start is called before the first frame update
    void Start()
    {
        //SetRandomSpeed();
        //SetRandomDirection();
    }

    void SetRandomSpeed()
    {
        Speed = UnityEngine.Random.Range(0.0f, MaxSpeed);
    }

    void SetRandomDirection()
    {
        float angle = 30.0f;// Random.Range(-180.0f, 180.0f);
        Vector2 dir = new Vector2(Mathf.Cos(Mathf.Deg2Rad * angle), Mathf.Sin(Mathf.Deg2Rad * angle));//, 0.0f);
        dir.Normalize();
        TargetDirection = dir;
    }

    public void SetColor(Color c)
    {
        spriteRenderer.color = c;
    }

    public void Initialize()
    {
        SetRandomDirection();
    }

    public void LateInit()
    {
        SetRandomSpeed();
    }
    #endregion

    // Update is called once per frame
    private void Update()
    {
        if (baseFlock == null) return;

        if(baseFlock.isActiveAndEnabled)
            MoveObj();
    }

    private void MoveObj()
    {
        Vector3 targetDirection = TargetDirection;
        targetDirection.Normalize();

        Vector3 rotatedVectorToTarget =
          Quaternion.Euler(0, 0, 90) *
          targetDirection;

        Quaternion targetRotation = Quaternion.LookRotation(
          forward: Vector3.forward,
          upwards: rotatedVectorToTarget);

        transform.rotation = Quaternion.RotateTowards(
          transform.rotation,
          targetRotation,
          RotationSpeed * Time.deltaTime);

        Speed = Speed + ((TargetSpeed - Speed) / 10.0f) * Time.deltaTime;

        if (Speed > MaxSpeed)
            Speed = MaxSpeed;

        transform.Translate(Vector3.right * Speed * Time.deltaTime, Space.Self);

        //transform.Translate(Vector3.right * data.Speed * Time.deltaTime, Space.Self);
    }
}

[BurstCompile]
public struct BoidData
{
    public Vector3 pos;
    public Vector3 dir;
    public float spd;
    public BoidType type;
}

public struct bd
{
    public Vector3 pos;
    public Vector3 dir;
    public float spd;
}


public enum BoidType
{
    FRIENDLY,
    ENEMY
}
