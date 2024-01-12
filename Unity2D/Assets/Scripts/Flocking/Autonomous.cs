using System.Collections;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;

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

    public AutonomousData data;

    public SpriteRenderer spriteRenderer;
    public AutonomousType type;
    public Rect bounds;

    public int id;

    #region Start functions
    // Start is called before the first frame update
    void Start()
    {
        //data.Speed = 0.0f;
        //SetRandomSpeed();
        //SetRandomDirection();

        //data = new(MaxSpeed, Speed, TargetSpeed, RotationSpeed, accel, TargetDirection, transform.position);
    }

    void SetRandomSpeed()
    {
        float speed = Random.Range(0.0f, data.MaxSpeed);
        data.Speed = speed;
    }

    void SetRandomDirection()
    {
        float angle = 30.0f;// Random.Range(-180.0f, 180.0f);
        Vector2 dir = new Vector2(Mathf.Cos(Mathf.Deg2Rad * angle), Mathf.Sin(Mathf.Deg2Rad * angle));//, 0.0f);
        dir.Normalize();
        data.TargetDirection = dir;
    }

    public void SetColor(Color c)
    {
        spriteRenderer.color = c;
    }

    public void Initialize()
    {
        data = new();
        data.Speed = 0.0f;
        SetRandomDirection();

        //data = new(MaxSpeed, Speed, TargetSpeed, RotationSpeed, accel, TargetDirection, transform.position);
    }

    public void LateInit()
    {
        SetRandomSpeed();

        //StartCoroutine(Move());
        //data.Speed = 100f;
    }
    #endregion

    // Update is called once per frame
    private void Update()
    {
        bounds.position = new(transform.position.x-(bounds.width/2),transform.position.y-(bounds.height/2));
        //MoveObj();
        //data.Position = transform.position;
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(new(bounds.center.x,bounds.center.y),new(bounds.size.x,bounds.size.y));
    }

    private void MoveObj()
    {
        
        //transform.position = data.Position;

        //Vector3 targetDirection = data.TargetDirection;
        //targetDirection.Normalize();

        //Vector3 rotatedVectorToTarget =
        //  Quaternion.Euler(0, 0, 90) *
        //  targetDirection;

        //Quaternion targetRotation = Quaternion.LookRotation(
        //  forward: Vector3.forward,
        //  upwards: rotatedVectorToTarget);

        //transform.rotation = Quaternion.RotateTowards(
        //  transform.rotation,
        //  targetRotation,
        //  data.RotationSpeed * Time.deltaTime);

        //data.Speed = data.Speed + ((data.TargetSpeed - data.Speed) / 10.0f) * Time.deltaTime;

        //if (data.Speed > data.MaxSpeed)
        //    data.Speed = data.MaxSpeed;

        transform.Translate(Vector3.right * data.Speed * Time.deltaTime, Space.Self);
        data.Position = transform.position;
    }
}


[System.Serializable,BurstCompile]
public struct AutonomousData
{
    public float MaxSpeed;
    public float Speed;
    public float TargetSpeed;
    public float RotationSpeed;
    public Vector2 Accel;
    public Vector3 TargetDirection;
    public Vector3 Position;

    public AutonomousData(float maxSpd, float spd, float tarSpd, float rotSpd, Vector2 accel, Vector3 tarDir,Vector3 pos)
    {
        MaxSpeed = maxSpd;
        Speed = spd;
        TargetSpeed = tarSpd;
        RotationSpeed = rotSpd;
        Accel = accel;
        TargetDirection = tarDir;
        Position = pos;
    }
}

public struct BoidData
{
    public Vector3 pos;
    public Vector3 dir;
    public float spd;
}

public enum AutonomousType
{
    OBSTACLE,
    FRIENDLY,
    ENEMY
}
