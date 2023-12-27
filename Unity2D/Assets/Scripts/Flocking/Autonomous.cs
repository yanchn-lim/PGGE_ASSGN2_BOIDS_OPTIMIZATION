using System.Collections;
using UnityEngine;

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
        SetRandomSpeed();
        SetRandomDirection();

        //data = new(MaxSpeed, Speed, TargetSpeed, RotationSpeed, accel, TargetDirection, transform.position);
    }
    #endregion

    // Update is called once per frame
    public void Update()
    {
        transform.position = data.Position;

        Vector3 targetDirection = data.TargetDirection;
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
          data.RotationSpeed * Time.deltaTime);

        data.Speed = data.Speed + ((data.TargetSpeed - data.Speed) / 10.0f) * Time.deltaTime;

        if (data.Speed > data.MaxSpeed)
            data.Speed = data.MaxSpeed;

        transform.Translate(Vector3.right * data.Speed * Time.deltaTime, Space.Self);
        data.Position = transform.position;
    }

    #region hide
    private void FixedUpdate()
    {
    }

    private IEnumerator Coroutine_LerpTargetSpeed(
      float start,
      float end,
      float seconds = 2.0f)
    {
        float elapsedTime = 0;
        while (elapsedTime < seconds)
        {
            data.Speed = Mathf.Lerp(
              start,
              end,
              (elapsedTime / seconds));
            elapsedTime += Time.deltaTime;

            yield return null;
        }
        data.Speed = end;
    }

    private IEnumerator Coroutine_LerpTargetSpeedCont(
    float seconds = 2.0f)
    {
        float elapsedTime = 0;
        while (elapsedTime < seconds)
        {
            data.Speed = Mathf.Lerp(
              data.Speed,
              data.TargetSpeed,
              (elapsedTime / seconds));
            elapsedTime += Time.deltaTime;

            yield return null;
        }
        data.Speed = data.TargetSpeed;
    }

    static public Vector3 GetRandom(Vector3 min, Vector3 max)
    {
        return new Vector3(
          Random.Range(min.x, max.x),
          Random.Range(min.y, max.y),
          Random.Range(min.z, max.z));
    }
    #endregion
}

[System.Serializable]
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
