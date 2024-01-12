using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;

public class FlockHandler : MonoBehaviour
{
    #region REFERENCES
    public FlockBehaviour fb;
    #endregion

    #region VARIABLES
    public float boundsMinX;
    public float boundsMaxX;
    public float boundsMinY;
    public float boundsMaxY;

    [SerializeField]
    List<Flock> flocks = new List<Flock>();

    #endregion

    #region JOB VARIABLES
    int maxNumOfBoids = 30000;
    int friendlyCount = 0;
    int enemyCount = 0;
    int obstacleCount = 0;

    TransformAccessArray transformArray;
    NativeArray<BoidData> boidDataF_NA;
    NativeArray<BoidData> boidDataE_NA;

    MoveJob moveJob;
    FlockJob flockJob;
    RandMoveJob randMoveJob;

    JobHandle moveJobHandle;
    JobHandle flockJobHandle;
    JobHandle randMoveJobHandle;
    #endregion

    #region Initialize Methods
    void Initialize()
    {

        transformArray = new();
        //creating the native array with a huge size, set it to persistent
        //so it persists until its removal
        boidDataF_NA = new(maxNumOfBoids, Allocator.Persistent);
        boidDataE_NA = new(maxNumOfBoids, Allocator.Persistent);


    }

    //creating the different flocks at the start
    void CreateFlocks()
    {
        for (int i = 0; i < flocks.Count; i++)
        {
            AddBoids(flocks[i].PrefabBoid, flocks[i].numBoids,flocks[i].isPredator);
        }
    }

    //adding the number of boids at the start
    void AddBoids(GameObject prefab,int n,bool pred)
    {
        for (int i = 0; i < n; i++)
        {
            //get random x,y value
            float x = Random.Range(boundsMinX, boundsMaxX);
            float y = Random.Range(boundsMinY, boundsMaxY);
            GameObject boidObj = Instantiate(prefab,new Vector3(x,y),Quaternion.identity);
            transformArray.Add(boidObj.transform);
            
            //assign the boids into the correct types
            if (pred)
            {
                boidDataE_NA[enemyCount] = new BoidData
                {
                    pos = new(x, y),
                    dir = Vector3.zero,
                    spd = flocks[i].maxSpeed
                };

                enemyCount++;
            }
            else
            {
                boidDataF_NA[friendlyCount] = new BoidData
                {
                    pos = new(x, y),
                    dir = Vector3.zero,
                    spd = flocks[i].maxSpeed
                };

                friendlyCount++;
            }
        }
    }
    #endregion

    //add friendly boids
    void ManualAddBoids_F(int n)
    {
        //wait for all job to complete first before adding more boids
        //as we are modifying the buffers being used in the jobs
        WaitForAllJobComplete();
        GameObject prefab = flocks[0].PrefabBoid;

        for (int i = 0; i < n; i++)
        {
            float x = Random.Range(boundsMinX, boundsMaxX);
            float y = Random.Range(boundsMinY, boundsMaxY);
            GameObject boidObj = Instantiate(prefab, new Vector3(x, y), Quaternion.identity);
            transformArray.Add(boidObj.transform);
            boidDataF_NA[friendlyCount] = new BoidData
            {
                pos = new(x, y),
                dir = Vector3.zero,
                spd = flocks[i].maxSpeed
            };

            friendlyCount++;
        }
    }

    #region Coroutine Methods

    void BeginMovement()
    {
        StartCoroutine(Coroutine_Move());
        StartCoroutine(Coroutine_Align());
        StartCoroutine(Coroutine_Random());
    }

    IEnumerator Coroutine_Move()
    {
        while (true)
        {


            yield return null;
        }
    }

    IEnumerator Coroutine_Align()
    {
        while (true)
        {
            foreach (var flock in flocks)
            {
                if (flock.useAlignmentRule)
                {

                }
                yield return null;
            }
        }
    }

    IEnumerator Coroutine_Random()
    {
        while (true)
        {
            foreach (var flock in flocks)
            {
                if (flock.useRandomRule)
                {

                }
                yield return null;
            }
        }
    }

    #endregion

    void WaitForAllJobComplete()
    {
        moveJobHandle.Complete();
        flockJobHandle.Complete();
        randMoveJobHandle.Complete();
    }

    #region Jobs
    [BurstCompile]
    struct MoveJob : IJobParallelForTransform
    {
        NativeArray<BoidData> boid;
        NativeArray<ObstacleData> obstacle;

        #region SPEED VARIABLES
        public float maxSpd;
        public float spd;
        public float deltaTime;
        public float rotationSpd;
        #endregion

        #region GAME INFO
        public int obstacleCount;

        public float boundsMinX;
        public float boundsMaxX;
        public float boundsMinY;
        public float boundsMaxY;

        public float weightAvoidObstacles;
        #endregion

        public bool bounceOffWall;

        public void Execute(int index, TransformAccess transform)
        {
            Vector3 dir = boid[index].dir;


            Vector3 rotatedVectorToTarget =
              Quaternion.Euler(0, 0, 90) *
              dir;

            Quaternion targetRotation = Quaternion.LookRotation(
              forward: Vector3.forward,
              upwards: rotatedVectorToTarget);

            transform.rotation = Quaternion.RotateTowards(
              transform.rotation,
              targetRotation,
              rotationSpd * Time.deltaTime);

        }

        Vector3 CrossBorder(Vector3 tarDir, TransformAccess transform)
        {
            if (bounceOffWall)
            {
                //if rule turned on, the transform's target direction is reflected to turn
                //the opposite direction of the boundary it is close to

                if (transform.position.x + 5.0f > boundsMaxX)
                {
                    tarDir.x = -1.0f;
                }
                if (transform.position.x - 5.0f < boundsMinX)
                {
                    tarDir.x = 1.0f;
                }
                if (transform.position.y + 5.0f > boundsMaxY)
                {
                    tarDir.y = -1.0f;
                }
                if (transform.position.y - 5.0f < boundsMinY)
                {
                    tarDir.y = 1.0f;
                }
                tarDir.Normalize();
            }
            else
            {
                //if the rule is not turned on, it puts the transform on the other end of the boundary
                Vector3 pos = transform.position;
                if (transform.position.x > boundsMaxX)
                {
                    pos.x = boundsMinX;
                }
                if (transform.position.x < boundsMinX)
                {
                    pos.x = boundsMaxX;
                }
                if (transform.position.y > boundsMaxY)
                {
                    pos.y = boundsMinY;
                }
                if (transform.position.y < boundsMinY)
                {
                    pos.y = boundsMaxY;
                }
                transform.position = pos;
            }

            return tarDir;
        }

        Vector3 AvoidObstacle(Vector3 tarDir,TransformAccess transform)
        {
            for (int i = 0; i < obstacleCount; i++)
            {
                //get the distance between the current transform and the object
                float dist = (
                  obstacle[i].pos -
                  transform.position).magnitude;

                //check if the distance is less than the avoidance radius
                if (dist < obstacle[i].avoidRad)
                {
                    //get the direction of the transform from the obstacle
                    Vector3 avoidDir = (transform.position - obstacle[i].pos).normalized;

                    //add it into the target direction multiplied by the weight of avoidance
                    tarDir += (avoidDir * weightAvoidObstacles);

                    //normalize it
                    tarDir.Normalize();                    
                }
            }

            //return the calculated target direction with the avoidance of obstacle
            return tarDir;
        }
    }

    [BurstCompile]
    struct FlockJob : IJobParallelFor
    {
        public void Execute(int index)
        {
            throw new System.NotImplementedException();
        }
    }

    [BurstCompile]
    struct RandMoveJob : IJobParallelForTransform
    {
        public void Execute(int index, TransformAccess transform)
        {
            throw new System.NotImplementedException();
        }
    }

    #endregion
}
