using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;

public class FlockBehaviour : MonoBehaviour
{
    List<Obstacle> mObstacles = new List<Obstacle>();

    [SerializeField]
    GameObject[] Obstacles;

    [SerializeField]
    BoxCollider2D Bounds;

    public float TickDuration = 1.0f;
    public float TickDurationSeparationEnemy = 0.1f;
    public float TickDurationRandom = 1.0f;

    public int BoidIncr = 100;
    public bool useFlocking = false;
    public int BatchSize = 100;

    public List<Flock> flocks = new List<Flock>();
    public QuadTree quadTree;
    public List<Autonomous> allObj = new();
    //public MovementHandler mHandler;
    Rect rect;

    void Reset()
    {
        flocks = new List<Flock>()
        {
          new Flock()
        };
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(rect.center,rect.size);
        //Gizmos.DrawCube(flocks[0].mAutonomous[0].bounds.center, flocks[0].mAutonomous[0].bounds.size);
    }

    void Start()
    {
        Vector2 pos = new(Bounds.transform.position.x - (Bounds.bounds.size.x/2)
            , Bounds.transform.position.y - (Bounds.bounds.size.y/2));
        rect = new(pos, Bounds.bounds.size);
        quadTree = new(rect, new(flocks[0].visibility, flocks[0].visibility));

        // Randomize obstacles placement.
        //obstacles are the asteroid on the map
        for (int i = 0; i < Obstacles.Length; ++i)
        {
            float x = Random.Range(Bounds.bounds.min.x, Bounds.bounds.max.x);
            float y = Random.Range(Bounds.bounds.min.y, Bounds.bounds.max.y);
            Obstacle obs = Obstacles[i].AddComponent<Obstacle>();
            Autonomous autono = Obstacles[i].AddComponent<Autonomous>();
            Obstacles[i].transform.position = new Vector3(x, y, 0.0f);
            autono.data.Position = Obstacles[i].transform.position;
            autono.Initialize();
            autono.data.MaxSpeed = 1.0f;
            autono.LateInit();
            autono.type = AutonomousType.OBSTACLE;
            obs.mCollider = Obstacles[i].GetComponent<CircleCollider2D>();
            mObstacles.Add(obs);

            allObj.Add(autono);

            //quadTree.Insert(autono,quadTree.Root);
        }

        //creates 2 flock of ships
        //flock 1 is friendly ships which spawn 100 at the start
        //flock 2 is enemy ships which spawns 20 at the start
        foreach (Flock flock in flocks)
        {
            CreateFlock(flock);
        }

        //boid rules
        StartCoroutine(Coroutine_Flocking());

        StartCoroutine(Coroutine_Random());
        StartCoroutine(Coroutine_AvoidObstacles());
        StartCoroutine(Coroutine_SeparationWithEnemies());

        //random movement of the obstacles on the map
        StartCoroutine(Coroutine_Random_Motion_Obstacles());
    }

    //create a flock of the ships
    void CreateFlock(Flock flock)
    {
        //spawn each ship in a random location
        for (int i = 0; i < flock.numBoids; ++i)
        {
            float x = Random.Range(Bounds.bounds.min.x, Bounds.bounds.max.x);
            float y = Random.Range(Bounds.bounds.min.y, Bounds.bounds.max.y);

            AddBoid(x, y, flock);
        }
    }

    void Update()
    {
        //check input for player
        HandleInputs();


        Rule_CrossBorder();
        Rule_CrossBorder_Obstacles();
    }

    void HandleInputs()
    {
        if (EventSystem.current.IsPointerOverGameObject() ||
           enabled == false)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            AddBoids(BoidIncr);
        }

        if (Input.GetKeyDown(KeyCode.X))
        {
            ///Debug.Log(quadTree.FindNode(quadTree.Root, flocks[0].mAutonomous[0].transform.position).depth);

            //Debug.Log(flocks[0].mAutonomous[0].name);
            //Debug.Log(Bounds.bounds.Contains(flocks[0].mAutonomous[0].transform.position));
            //quadTree.Insert(flocks[0].mAutonomous[0], quadTree.Root);

            //quadTree.FindObjInRange(quadTree.Root, flocks[0].mAutonomous[0], AutonomousType.FRIENDLY);
        }
    }

    //add the specified number of boids at random locations
    //increase the number of boids in the count
    void AddBoids(int count)
    {
        for (int i = 0; i < count; ++i)
        {
            float x = Random.Range(Bounds.bounds.min.x, Bounds.bounds.max.x);
            float y = Random.Range(Bounds.bounds.min.y, Bounds.bounds.max.y);

            AddBoid(x, y, flocks[0]);

        }
        flocks[0].numBoids += count;
    }

    //add the boid into the game
    //boid is spawned and a Autonomous component is added
    //Autonomous component automates the boid logic
    void AddBoid(float x, float y, Flock flock)
    {
        GameObject obj = Instantiate(flock.PrefabBoid);
        obj.name = "Boid_" + flock.name + "_" + flock.mAutonomous.Count;
        Autonomous boid = obj.GetComponent<Autonomous>();
        flock.mAutonomous.Add(boid);

        //add the data of the boid into the list of data
        boid.Initialize();
        //assign the data
        boid.data.Position = new Vector3(x, y, 0.0f);
        boid.data.MaxSpeed = flock.maxSpeed;
        boid.data.RotationSpeed = flock.maxRotationSpeed;
        boid.bounds = new(boid.transform.position,new(flock.visibility,flock.visibility));
        //assign the boid's type
        boid.type = flock.isPredator ? AutonomousType.ENEMY : AutonomousType.FRIENDLY;
        
        //Late initialize after everything
        boid.LateInit();

        allObj.Add(boid);

        quadTree.Insert(boid, quadTree.Root);

    }

    //BOID RULE : FLOCK TOGETHER
    IEnumerator Coroutine_Flocking()
    {
        while (true)
        {
            if (useFlocking)
            {
                foreach (Flock flock in flocks)
                {
                    //goes through the list of boids
                    List<Autonomous> autonomousList = flock.mAutonomous;
                    NativeArray<AutonomousData> dataNativeList = new NativeArray<AutonomousData>(flock.mAutonomous.Count,Allocator.TempJob);

                    for (int i = 0; i < flock.mAutonomous.Count; i++)
                    {
                        var data = autonomousList[i].data;
                        dataNativeList[i] = data;
                    }

                    FlockJob job = new(dataNativeList, flock.visibility, flock.separationDistance, flock.weightSeparation, flock.weightAlignment, flock.weightCohesion
                        , flock.useAlignmentRule, flock.useSeparationRule, flock.useCohesionRule);

                    JobHandle jobHandle = job.Schedule(dataNativeList.Length, dataNativeList.Length);
                    jobHandle.Complete();
                    for (int i = 0; i < dataNativeList.Length; i++)
                    {
                        flock.mAutonomous[i].data = dataNativeList[i];
                    }

                    dataNativeList.Dispose();

                    //yield return null; //wait a frame after processing one type of boid
                }
            }
            yield return null;
            //yield return new WaitForSeconds(TickDuration); //wait for the tick duration before looping
        }
    }

    IEnumerator Coroutine_SeparationWithEnemies()
    {
        while (true)
        {
            foreach (Flock flock in flocks)
            {
                if (!flock.useFleeOnSightEnemyRule || flock.isPredator) continue;
                NativeArray<AutonomousData> nativeAutoArray = new(flock.mAutonomous.Count, Allocator.TempJob);

                for (int i = 0; i < flock.mAutonomous.Count; i++)
                {
                    nativeAutoArray[i] = flock.mAutonomous[i].data;
                }

                foreach (Flock enemies in flocks)
                {
                    if (!enemies.isPredator) continue;

                    NativeArray<AutonomousData> nativeEnemyArray = new(enemies.mAutonomous.Count, Allocator.TempJob);

                    for (int i = 0; i < enemies.mAutonomous.Count; i++)
                    {
                        nativeEnemyArray[i] = enemies.mAutonomous[i].data;
                    }

                    AvoidEnemiesJob job = new(nativeAutoArray,nativeEnemyArray,flock.enemySeparationDistance,flock.weightFleeOnSightEnemy);
                    JobHandle jobHandle = job.Schedule(nativeAutoArray.Length,nativeAutoArray.Length);
                    jobHandle.Complete();

                    for (int i = 0; i < nativeAutoArray.Length; i++)
                    {
                        flock.mAutonomous[i].data = nativeAutoArray[i];
                    }

                    nativeAutoArray.Dispose();
                    nativeEnemyArray.Dispose();

                }
                //yield return null;
            }
            yield return null;
        }
    }

    IEnumerator Coroutine_AvoidObstacles()
    {
        while (true)
        {
            foreach (Flock flock in flocks)
            {
                if (flock.useAvoidObstaclesRule)
                {
                    List<Autonomous> autonomousList = flock.mAutonomous;
                    NativeArray<AutonomousData> nativeAutoArray = new NativeArray<AutonomousData>(autonomousList.Count, Allocator.TempJob);
                    NativeArray<AutonomousData> nativeObstacleArray = new NativeArray<AutonomousData>(mObstacles.Count, Allocator.TempJob);
                    NativeArray<float> nativeFloatArray = new NativeArray<float>(mObstacles.Count, Allocator.TempJob);
                    for (int i = 0; i < flock.mAutonomous.Count; i++)
                    {
                        var data = autonomousList[i].data;
                        nativeAutoArray[i] = data;
                    }

                    for (int i = 0; i < mObstacles.Count; i++)
                    {
                        nativeObstacleArray[i] = mObstacles[i].GetComponent<Autonomous>().data;
                        nativeFloatArray[i] = mObstacles[i].AvoidanceRadius;
                    }

                    AvoidObstacleJob job = new(nativeAutoArray,nativeObstacleArray,nativeFloatArray,flock.weightAvoidObstacles);
                    JobHandle jobHandle = job.Schedule(nativeAutoArray.Length,nativeAutoArray.Length);
                    jobHandle.Complete();

                    for (int i = 0; i < nativeAutoArray.Length; i++)
                    {
                        flock.mAutonomous[i].data = nativeAutoArray[i];
                    }

                    nativeAutoArray.Dispose();
                    nativeObstacleArray.Dispose();
                    nativeFloatArray.Dispose();
                }
                yield return null;
            }
            yield return null;
        }
    }

    IEnumerator Coroutine_Random_Motion_Obstacles()
    {
        while (true)
        {
            for (int i = 0; i < Obstacles.Length; ++i)
            {
                Autonomous autono = Obstacles[i].GetComponent<Autonomous>();
                float rand = Random.Range(0.0f, 1.0f);
                autono.data.TargetDirection.Normalize();
                float angle = Mathf.Atan2(autono.data.TargetDirection.y, autono.data.TargetDirection.x);

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

                autono.data.TargetDirection += dir * 0.1f;
                autono.data.TargetDirection.Normalize();
                //Debug.Log(autonomousList[i].TargetDirection);

                float speed = Random.Range(1.0f, autono.data.MaxSpeed);
                autono.data.TargetSpeed += speed;
                autono.data.TargetSpeed /= 2.0f;
            }
            yield return new WaitForSeconds(2.0f);
        }
    }

    IEnumerator Coroutine_Random()
    {
        while (true)
        {
            foreach (Flock flock in flocks)
            {
                if (flock.useRandomRule)
                {
                    List<Autonomous> autonomousList = flock.mAutonomous;
                    for (int i = 0; i < autonomousList.Count; ++i)
                    {
                        float rand = Random.Range(0.0f, 1.0f);
                        autonomousList[i].data.TargetDirection.Normalize();
                        float angle = Mathf.Atan2(autonomousList[i].data.TargetDirection.y, autonomousList[i].data.TargetDirection.x);

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

                        autonomousList[i].data.TargetDirection += dir * flock.weightRandom;
                        autonomousList[i].data.TargetDirection.Normalize();
                        //Debug.Log(autonomousList[i].TargetDirection);

                        float speed = Random.Range(1.0f, autonomousList[i].data.MaxSpeed);
                        autonomousList[i].data.TargetSpeed += speed * flock.weightSeparation;
                        autonomousList[i].data.TargetSpeed /= 2.0f;
                    }
                }
                //yield return null;
            }
            yield return new WaitForSeconds(TickDurationRandom);
        }
    }

    void Rule_CrossBorder_Obstacles()
    {
        for (int i = 0; i < Obstacles.Length; ++i)
        {
            Autonomous autono = Obstacles[i].GetComponent<Autonomous>();
            Vector3 pos = autono.data.Position;
            if (autono.data.Position.x > Bounds.bounds.max.x)
            {
                pos.x = Bounds.bounds.min.x;
                
            }
            if (autono.data.Position.x < Bounds.bounds.min.x)
            {
                pos.x = Bounds.bounds.max.x;
            }
            if (autono.data.Position.y > Bounds.bounds.max.y)
            {
                pos.y = Bounds.bounds.min.y;
            }
            if (autono.data.Position.y < Bounds.bounds.min.y)
            {
                pos.y = Bounds.bounds.max.y;
            }
            autono.data.Position = pos;
        }

        //for (int i = 0; i < Obstacles.Length; ++i)
        //{
        //  Autonomous autono = Obstacles[i].GetComponent<Autonomous>();
        //  Vector3 pos = autono.transform.position;
        //  if (autono.transform.position.x + 5.0f > Bounds.bounds.max.x)
        //  {
        //    autono.TargetDirection.x = -1.0f;
        //  }
        //  if (autono.transform.position.x - 5.0f < Bounds.bounds.min.x)
        //  {
        //    autono.TargetDirection.x = 1.0f;
        //  }
        //  if (autono.transform.position.y + 5.0f > Bounds.bounds.max.y)
        //  {
        //    autono.TargetDirection.y = -1.0f;
        //  }
        //  if (autono.transform.position.y - 5.0f < Bounds.bounds.min.y)
        //  {
        //    autono.TargetDirection.y = 1.0f;
        //  }
        //  autono.TargetDirection.Normalize();
        //}
    }

    void Rule_CrossBorder()
    {
        foreach (Flock flock in flocks)
        {
            List<Autonomous> autonomousList = flock.mAutonomous;
            if (flock.bounceWall)
            {
                for (int i = 0; i < autonomousList.Count; ++i)
                {
                    Vector3 pos = autonomousList[i].data.Position;
                    if (autonomousList[i].data.Position.x + 5.0f > Bounds.bounds.max.x)
                    {
                        autonomousList[i].data.TargetDirection.x = -1.0f;
                    }
                    if (autonomousList[i].data.Position.x - 5.0f < Bounds.bounds.min.x)
                    {
                        autonomousList[i].data.TargetDirection.x = 1.0f;
                    }
                    if (autonomousList[i].data.Position.y + 5.0f > Bounds.bounds.max.y)
                    {
                        autonomousList[i].data.TargetDirection.y = -1.0f;
                    }
                    if (autonomousList[i].data.Position.y - 5.0f < Bounds.bounds.min.y)
                    {
                        autonomousList[i].data.TargetDirection.y = 1.0f;
                    }
                    autonomousList[i].data.TargetDirection.Normalize();
                }
            }
            else
            {
                for (int i = 0; i < autonomousList.Count; ++i)
                {
                    Vector3 pos = autonomousList[i].data.Position;
                    if (autonomousList[i].data.Position.x > Bounds.bounds.max.x)
                    {
                        pos.x = Bounds.bounds.min.x;
                    }
                    if (autonomousList[i].data.Position.x < Bounds.bounds.min.x)
                    {
                        pos.x = Bounds.bounds.max.x;
                    }
                    if (autonomousList[i].data.Position.y > Bounds.bounds.max.y)
                    {
                        pos.y = Bounds.bounds.min.y;
                    }
                    if (autonomousList[i].data.Position.y < Bounds.bounds.min.y)
                    {
                        pos.y = Bounds.bounds.max.y;
                    }
                    autonomousList[i].data.Position = pos;
                }
            }
        }
    }

    #region JOBS
    [BurstCompile]
    public struct FlockJob : IJobParallelFor
    {
        NativeArray<AutonomousData> dataList;

        [ReadOnly]
        float visibility;
        [ReadOnly]
        float separationDistance;
        [ReadOnly]
        float weightSeparation;
        [ReadOnly]
        float weightAlignment;
        [ReadOnly]
        float weightCohesion;
        [ReadOnly]
        bool useAlignmentRule;
        [ReadOnly]
        bool useSeparationRule;
        [ReadOnly]
        bool useCohesionRule;

        public void Execute(int index)
        {
            Vector3 flockDir = Vector3.zero;
            Vector3 separationDir = Vector3.zero;
            Vector3 cohesionDir = Vector3.zero;

            float speed = 0.0f;
            float separationSpeed = 0.0f;

            int count = 0; 
            Vector3 steerPos = Vector3.zero;

            //goes through the list of boid
            AutonomousData curr = dataList[index]; //the current boid we are checking

            for (int j = 0; j < dataList.Length; ++j)
            {
                AutonomousData other = dataList[j];//the boid we are checking against

                float dist = (curr.Position - other.Position).magnitude; //checking the distance between them
                if (index != j && dist < visibility)
                {
                    speed += other.Speed;
                    flockDir += other.TargetDirection;
                    steerPos += other.Position;
                    count++;
                }
                if (index != j)
                {
                    if (dist < separationDistance)
                    {
                        Vector3 targetDirection = (
                          curr.Position -
                          other.Position).normalized;

                        separationDir += targetDirection;
                        separationSpeed += dist * weightSeparation;
                    }
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
                          (steerPos - curr.Position) * (useCohesionRule ? weightCohesion : 0.0f);

            curr.TargetDirection = dir;
            curr.TargetDirection.Normalize();
            
            dataList[index] = new(curr.MaxSpeed,curr.Speed,curr.TargetSpeed,curr.RotationSpeed,curr.Accel,curr.TargetDirection,curr.Position);
        }

        public FlockJob(NativeArray<AutonomousData> data,float vis, float sepD, float weiS, float weiA, float weiC, bool aRule,bool sRule,bool cRule)
        {
            dataList = data;
            visibility = vis;
            separationDistance = sepD;
            weightSeparation = weiS;
            weightAlignment = weiA;
            weightCohesion = weiC;
            useAlignmentRule = aRule;
            useSeparationRule = sRule;
            useCohesionRule = cRule;
        }
    }

    [BurstCompile]
    public struct AvoidObstacleJob : IJobParallelFor
    {
        NativeArray<AutonomousData> autonomousList;
        [ReadOnly]
        NativeArray<AutonomousData> mObstacles;
        [ReadOnly]
        NativeArray<float> avoidanceRadiusArray;
        [ReadOnly]
        float weightAvoidObstacles;

        public void Execute(int index)
        {
            for (int j = 0; j < mObstacles.Length; ++j)
            {
                float dist = (
                  mObstacles[j].Position -
                  autonomousList[index].Position).magnitude;

                if (dist < avoidanceRadiusArray[j])
                {
                    Vector3 targetDirection = (
                      autonomousList[index].Position -
                      mObstacles[j].Position).normalized;


                    Vector3 dir = autonomousList[index].TargetDirection;
                    dir += targetDirection * weightAvoidObstacles;
                    dir.Normalize();

                    autonomousList[index] = new(autonomousList[index].MaxSpeed, autonomousList[index].Speed,
                        autonomousList[index].TargetSpeed, autonomousList[index].RotationSpeed, autonomousList[index].Accel, 
                        dir, autonomousList[index].Position);
                }
            }
        }

        public AvoidObstacleJob(
            NativeArray<AutonomousData> autonomousList,
            NativeArray<AutonomousData> mObstacles,
            NativeArray<float> avoidanceRadiusArray,
            float weightAvoidObstacles
            )
        {
            this.autonomousList = autonomousList;
            this.mObstacles = mObstacles;
            this.avoidanceRadiusArray = avoidanceRadiusArray;
            this.weightAvoidObstacles = weightAvoidObstacles;
        }
    }

    [BurstCompile]
    public struct AvoidEnemiesJob : IJobParallelFor
    {
        NativeArray<AutonomousData> boids;

        //set these to readonly to avoid and modification
        [ReadOnly]
        NativeArray<AutonomousData> enemies;
        [ReadOnly]
        float sepDist;
        [ReadOnly]
        float sepWeight;

        public void Execute(int index)
        {   
            //took the internal loop from the original method and implemented as a job
            //most of the working are still the same with some modified parts to fit into
            //the job system
            for (int j = 0; j < enemies.Length; ++j)
            {
                float dist = (
                  enemies[j].Position -
                  boids[index].Position).magnitude;
                if (dist < sepDist)
                {
                    Vector3 targetDirection = ((boids[index].Position -enemies[j].Position) + boids[index].TargetDirection).normalized;

                    float speed = boids[index].TargetSpeed + dist * sepWeight;
                    speed /= 2.0f;

                    //create a new boid data with the modified variables
                    boids[index] = new(boids[index].MaxSpeed, boids[index].Speed,
                        speed, boids[index].RotationSpeed, boids[index].Accel,
                        targetDirection, boids[index].Position);
                }
            }
        }

        public AvoidEnemiesJob(NativeArray<AutonomousData> boids, NativeArray<AutonomousData> enemies,float sepDist,float sepWeight)
        {
            this.boids = boids;
            this.enemies = enemies;
            this.sepDist = sepDist;
            this.sepWeight = sepWeight;
        }
    }
    #endregion
}


