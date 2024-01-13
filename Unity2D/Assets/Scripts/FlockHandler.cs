using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

public class FlockHandler : MonoBehaviour
{
    #region REFERENCES
    //public FlockBehaviour fb;
    [SerializeField]
    BoxCollider2D box;

    [SerializeField]
    GameObject[] obstacles;
    #endregion

    #region Customizable Variables
    [SerializeField]
    int BoidNumAdd;
    #endregion

    #region VARIABLES
    float boundsMinX;
    float boundsMaxX;
    float boundsMinY;
    float boundsMaxY;

    float maxSpd;
    float rotationSpd;
    float sepDist;
    float weightSep;
    float weightAlign;
    float weightCoh;
    float weightRand;
    float weightAvoidObs;
    float visibility;

    bool useRandomRule;
    bool useCohesionRule;
    bool useAlignmentRule;
    bool useSeparationRule;
    bool bounceWall;

    [SerializeField]
    List<Flock> flocks = new List<Flock>();
    #endregion

    #region JOB VARIABLES
    int maxNumOfBoids = 30000;
    public int boidCount = 0;
    public int friendlyCount = 0;
    public int enemyCount = 0;
    int obstacleCount = 0;
    int batchNum = 2000;

    TransformAccessArray transformArray;

    [NativeDisableContainerSafetyRestriction]
    NativeArray<ObstacleData> obstacle_NA;
    NativeArray<BoidData> boidData_NA; //data for all boids
    NativeArray<BoidData> boidData_NA_dbl; //double buffer
    [NativeDisableContainerSafetyRestriction]
    NativeArray<Vector3> boidTargetDir;
    [NativeDisableContainerSafetyRestriction]
    NativeArray<Vector3> boidTargetVel;

    NativeArray<BoidData> boidData_NA_E; //data for enemies

    MoveJob moveJob;
    FlockJob flockJob;
    RandMoveJob randMoveJob;

    JobHandle moveJobHandle;
    JobHandle flockJobHandle;
    JobHandle randMoveJobHandle;
    #endregion

    #region MonoBehaviour Methods
    private void Start()
    {
        Initialize();
    }

    private void Update()
    {
        WaitForAllJobComplete();

        HandleInputs();
        boidData_NA_dbl.CopyFrom(boidData_NA);
        moveJob = new MoveJob
        {
            maxSpd = maxSpd,
            spd = maxSpd,
            deltaTime = Time.deltaTime,
            rotationSpd = rotationSpd,
            boids = boidData_NA,
            boidTargetDir = boidTargetDir,
            boidTargetVel = boidTargetVel,
            enemies = boidData_NA_E,
            obstacle = obstacle_NA,
            obstacleCount = obstacleCount,
            boundsMinX = boundsMinX,
            boundsMaxX = boundsMaxX,
            boundsMinY = boundsMinY,
            boundsMaxY = boundsMaxY,
            weightAvoidObstacles = weightAvoidObs,
            sepDist = sepDist,
            sepWeight = weightSep,
            bounceOffWall = bounceWall,
            friendlyCount = friendlyCount
        };
        moveJobHandle = moveJob.Schedule(transformArray);
        JobHandle.ScheduleBatchedJobs();

        //Debug.Log(boidData_NA[0].dir);
    }
    #endregion

    void HandleInputs()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ManualAddBoids_F(BoidNumAdd);
        }
    }

    void Initialize()
    {
        transformArray = new(0);

        obstacleCount = obstacles.Length;

        //creating the native array with a huge size, set it to persistent
        //so it persists until its removal
        obstacle_NA = new(obstacleCount, Allocator.Persistent);
        boidData_NA = new(maxNumOfBoids, Allocator.Persistent);
        boidData_NA_dbl = new(maxNumOfBoids, Allocator.Persistent);
        boidTargetDir = new(maxNumOfBoids, Allocator.Persistent);
        boidTargetVel = new(maxNumOfBoids, Allocator.Persistent);
        boidData_NA_E = new(maxNumOfBoids, Allocator.Persistent);
        

        #region Bounds settings
        boundsMinX = box.bounds.min.x;
        boundsMaxX = box.bounds.max.x;
        boundsMinY = box.bounds.min.y;
        boundsMaxY = box.bounds.max.y;
        #endregion

        #region Flock Settings
        Flock f = flocks[0];
        maxSpd = f.maxSpeed;
        rotationSpd = f.maxRotationSpeed;
        sepDist = f.separationDistance;
        weightSep = f.weightSeparation;
        weightAlign = f.weightAlignment;
        weightCoh = f.weightCohesion;
        weightRand = f.weightRandom;
        weightAvoidObs = f.weightAvoidObstacles;
        visibility = f.visibility;
        useRandomRule = f.useRandomRule;
        useCohesionRule = f.useCohesionRule;
        useAlignmentRule = f.useAlignmentRule;
        useSeparationRule = f.useSeparationRule;
        bounceWall = f.bounceWall;
        #endregion

        for (int i = 0; i < obstacleCount; i++)
        {
            float rad = obstacles[i].GetComponent<CircleCollider2D>().radius * 3 * 1.5f;
            RandomDisperseObs(obstacles[i]);
            obstacle_NA[i] = new ObstacleData
            {
                pos = obstacles[i].transform.position,
                avoidRad = rad
            };
        }

        CreateFlocks();

        BeginMovement();
    }

    void RandomDisperseObs(GameObject obs)
    {
        float x = Random.Range(boundsMinX, boundsMaxX);
        float y = Random.Range(boundsMinY, boundsMaxY);
        
        obs.transform.position = new(x, y);
    }

    #region Add Boids
    //creating the different flocks at the start
    void CreateFlocks()
    {
        for (int i = 0; i < flocks.Count; i++)
        {
            AddBoids(flocks[i].PrefabBoid, flocks[i].numBoids,flocks[i].isPredator);
        }
    }

    //adding the number of boids at the start
    void AddBoids(GameObject prefab,int n,bool isPredator)
    {
        for (int i = 0; i < n; i++)
        {
            //get random x,y value
            float x = Random.Range(boundsMinX, boundsMaxX);
            float y = Random.Range(boundsMinY, boundsMaxY);
            GameObject boidObj = Instantiate(prefab,new Vector3(x,y),Quaternion.identity);
            transformArray.Add(boidObj.transform);

            boidData_NA[boidCount] = new BoidData
            {
                pos = new(x, y),
                dir = Vector3.zero,
                spd = maxSpd,
                type = isPredator? BoidType.ENEMY:BoidType.FRIENDLY
            };

            if (isPredator)
            {
                boidData_NA_E[enemyCount] = new BoidData
                {
                    pos = new(x, y),
                    dir = Vector3.zero,
                    spd = maxSpd,
                    type = BoidType.ENEMY
                };
                boidObj.name = "BoidE_" + enemyCount;

                enemyCount++;
            }
            else
            {
                boidObj.name = "BoidF_" + friendlyCount;
                friendlyCount++;
            }

            boidCount++;
        }
    }

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
            boidData_NA[boidCount] = new BoidData
            {
                pos = new(x, y),
                dir = Vector3.zero,
                spd = flocks[0].maxSpeed,
                type = BoidType.FRIENDLY
            };
            boidObj.name = "BoidF_" + friendlyCount;
            friendlyCount++;
            boidCount++;
        }
    }
    #endregion

    #region Coroutine Methods

    void BeginMovement()
    {
        //StartCoroutine(Coroutine_Move());
        StartCoroutine(Coroutine_Align());
        StartCoroutine(Coroutine_Random());
    }

    IEnumerator Coroutine_Move()
    {
        while (true)
        {
            moveJob = new MoveJob
            {
                maxSpd = maxSpd,
                spd = maxSpd,
                deltaTime = Time.deltaTime,
                rotationSpd = rotationSpd,
                boids = boidData_NA,
                boidTargetDir = boidTargetDir,
                obstacle = obstacle_NA,
                obstacleCount = obstacleCount,
                boundsMinX = boundsMinX,
                boundsMaxX = boundsMaxX,
                boundsMinY = boundsMinY,
                boundsMaxY = boundsMaxY,
                weightAvoidObstacles = weightAvoidObs,
                bounceOffWall = bounceWall           
            };

            moveJobHandle = moveJob.Schedule(transformArray);

            yield return null;
        }
    }

    IEnumerator Coroutine_Align()
    {
        while (true)
        {
            if (useAlignmentRule)
            {
                flockJob = new FlockJob
                {
                    boids = boidData_NA,
                    boidTargetVel = boidTargetVel,
                    visibility = visibility,
                    separationDistance = sepDist,
                    weightSeparation = weightSep,
                    weightAlignment = weightAlign,
                    weightCohesion = weightCoh,
                    boidCount = boidCount,
                    useAlignmentRule = useAlignmentRule,
                    useCohesionRule = useCohesionRule,
                    useSeparationRule = useSeparationRule
                };

                flockJobHandle = flockJob.Schedule(transformArray.length, batchNum);
            }

            yield return new WaitForSeconds(0.2f);
        }
    }

    IEnumerator Coroutine_Random()
    {
        while (true)
        {
            if (useRandomRule)
            {
                randMoveJob = new RandMoveJob
                {
                    targetDir = boidTargetDir,
                    boids = boidData_NA,
                    maxSpd = maxSpd,
                    weightRand = weightRand,
                    weightSep = weightSep,
                    random = new(78070)
                };

                randMoveJobHandle = randMoveJob.Schedule(transformArray);
            }

            //change to random tick
            yield return new WaitForSeconds(Random.Range(0,5f));
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
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<BoidData> boids;
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<Vector3> boidTargetDir;
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<Vector3> boidTargetVel;
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<ObstacleData> obstacle;

        [NativeDisableContainerSafetyRestriction]
        public NativeArray<BoidData> enemies;

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
        public float sepDist;
        public float sepWeight;
        #endregion

        public bool bounceOffWall;
        public int friendlyCount;

        public void Execute(int index, TransformAccess transform)
        {
            BoidType type = boids[index].type;
            Vector3 dir = boidTargetDir[index] + boidTargetVel[index];

            dir.Normalize();
            //apply the rule and check if they should replace the direction
            dir = CrossBorder(dir, transform);
            dir = AvoidObstacle(dir, transform);
            dir = AvoidEnemies(dir, transform, index);
            dir.Normalize();

            //unsure yet
            Vector3 rotatedVectorToTarget =
              Quaternion.Euler(0, 0, 90) *
              dir;

            //create a rotation for the boid
            Quaternion targetRotation = Quaternion.LookRotation(
              forward: Vector3.forward,
              upwards: rotatedVectorToTarget);

            //rotate the boid to the desired direction
            transform.rotation = Quaternion.RotateTowards(
              transform.rotation,
              targetRotation,
              rotationSpd * deltaTime);

            Vector3 pos = transform.position;

            //do the translation of the boid according to the direction and spd
            pos += spd * deltaTime * (transform.rotation * Vector3.right);
            transform.position = pos;

            //assign the data back into the array
            boids[index] = new BoidData
            {
                pos = pos,
                spd = spd,
                dir = dir,
                type = boids[index].type
            };


            if(type == BoidType.ENEMY)
            {
                enemies[index - friendlyCount] = new BoidData
                {
                    pos = pos,
                    spd = spd,
                    dir = dir
                };
            }
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
                float dist = (obstacle[i].pos - transform.position).magnitude;

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

        Vector3 AvoidEnemies(Vector3 tarDir, TransformAccess transform,int index)
        {
            for (int j = 0; j < enemies.Length; j++)
            {
                float dist = (
                  enemies[j].pos -
                  transform.position).magnitude;
                if (dist < sepDist)
                {
                    Vector3 avoidDirection = ((transform.position - enemies[j].pos) + boids[index].dir).normalized;

                    float speed = boids[index].spd + dist * sepWeight;
                    speed /= 2.0f;

                    tarDir += avoidDirection * sepWeight;       
                }

            }
            return tarDir;
        }
    }
    [BurstCompile]
    struct FlockJob : IJobParallelFor
    {
        [NativeDisableContainerSafetyRestriction]
        [ReadOnly]
        public NativeArray<BoidData> boids;
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<Vector3> boidTargetVel;

        #region Values
        [ReadOnly]
        public float visibility;
        [ReadOnly]
        public float separationDistance;
        [ReadOnly]
        public float weightSeparation;
        [ReadOnly]
        public float weightAlignment;
        [ReadOnly]
        public float weightCohesion;
        [ReadOnly]
        public int boidCount;
        #endregion

        #region Rules
        [ReadOnly]
        public bool useAlignmentRule;
        [ReadOnly]
        public bool useSeparationRule;
        [ReadOnly]
        public bool useCohesionRule;
        #endregion

        public void Execute(int index)
        {
            Vector3 flockDir = Vector3.zero;
            Vector3 separationDir = Vector3.zero;
            Vector3 steerPos = Vector3.zero;

            float speed = 0.0f;
            float separationSpeed = 0.0f;
            int count = 0;

            //goes through the list of boid
            for (int j = 0; j < boidCount; ++j)
            {
                float dist = (boids[index].pos - boids[j].pos).magnitude; //checking the distance between them
                if (index != j && dist < visibility)
                {
                    speed += boids[j].spd;
                    flockDir += boids[j].dir;
                    steerPos += boids[j].pos;
                    count++;
                }

                if (index != j && dist < separationDistance)
                {

                    Vector3 targetDirection = (
                      boids[index].pos -
                      boids[j].pos).normalized;

                    separationDir += targetDirection;
                    separationSpeed += dist * weightSeparation;
                    
                }
            }

            if (count > 0)
            {
                speed = speed / count;
                flockDir = flockDir / count;
                flockDir.Normalize();

                steerPos = steerPos / count;
            }


            Vector3 dir = flockDir * speed * (useAlignmentRule ? weightAlignment : 0.0f) +
                          separationDir * separationSpeed * (useSeparationRule ? weightSeparation : 0.0f) +
                          (steerPos - boids[index].pos) * (useCohesionRule ? weightCohesion : 0.0f);
            
            
            boidTargetVel[index] = dir;
        }
    }
    [BurstCompile]
    struct RandMoveJob : IJobParallelForTransform
    {
        public NativeArray<Vector3> targetDir;
        public NativeArray<BoidData> boids;

        public float maxSpd;
        public float weightRand;
        public float weightSep;
        public Unity.Mathematics.Random random;

        public void Execute(int index, TransformAccess transform)
        {
            float rand = random.NextFloat(0.0f, 1.0f);
            targetDir[index].Normalize();
            float angle = Mathf.Atan2(targetDir[index].y, targetDir[index].x);

            if (rand > 0.5f)
            {
                angle += Mathf.Deg2Rad * 45.0f;
            }
            else
            {
                angle -= Mathf.Deg2Rad * 45.0f;
            }
            Vector3 dir = Vector3.zero;
            dir.x = Mathf.Cos(angle);
            dir.y = Mathf.Sin(angle);

            targetDir[index] += dir * weightRand;
            targetDir[index].Normalize();

            float boidSpd = boids[index].spd;
            float speed = random.NextFloat(1.0f, maxSpd);
            boidSpd += speed * weightSep;
            boidSpd /= 2.0f;

            boids[index] = new BoidData
            {
                dir = targetDir[index],
                spd = boidSpd,
                pos = boids[index].pos
            };
        }
    }

    #endregion
}
