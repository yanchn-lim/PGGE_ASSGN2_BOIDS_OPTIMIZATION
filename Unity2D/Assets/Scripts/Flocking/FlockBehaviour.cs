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
    void Reset()
    {
        flocks = new List<Flock>()
        {
          new Flock()
        };
    }

    void Start()
    {
        // Randomize obstacles placement.
        //obstacles are the asteroid on the map
        for (int i = 0; i < Obstacles.Length; ++i)
        {
            float x = Random.Range(Bounds.bounds.min.x, Bounds.bounds.max.x);
            float y = Random.Range(Bounds.bounds.min.y, Bounds.bounds.max.y);
            Obstacle obs = Obstacles[i].AddComponent<Obstacle>();
            Autonomous autono = Obstacles[i].AddComponent<Autonomous>();
            //Obstacles[i].transform.position = new Vector3(x, y, 0.0f);
            autono.data.Position = new Vector3(x, y, 0.0f);

            autono.data.MaxSpeed = 1.0f;
            obs.mCollider = Obstacles[i].GetComponent<CircleCollider2D>();
            mObstacles.Add(obs);
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
        flock.mAutonomousData.Add(boid.data);
        boid.Initialize();
        boid.data.Position = new Vector3(x, y, 0.0f);
        boid.data.MaxSpeed = flock.maxSpeed;
        boid.data.RotationSpeed = flock.maxRotationSpeed;

    }

    static float Distance(Autonomous a1, Autonomous a2)
    {
        return (a1.transform.position - a2.transform.position).magnitude;
    }

    //maybe use the job system on this part
    void Execute(Flock flock, int i)
    {
        //speed and direction would be incremented earlier so
        //set to zero for now

        //set an instance of each direction to zero
        Vector3 flockDir = Vector3.zero;
        Vector3 separationDir = Vector3.zero;
        Vector3 cohesionDir = Vector3.zero;

        //set speeds to zero
        float speed = 0.0f;
        float separationSpeed = 0.0f;

        int count = 0; //number of boids in this group
        //int separationCount = 0; //not being used?
        Vector3 steerPos = Vector3.zero;

        //goes through the list of boid
        Autonomous curr = flock.mAutonomous[i]; //the current boid we are checking
        for (int j = 0; j < flock.numBoids; ++j)
        {
            Autonomous other = flock.mAutonomous[j];//the boid we are checking against
            
            float dist = (curr.data.Position - other.data.Position).magnitude; //checking the distance between them
            if (i != j && dist < flock.visibility)
            {
                speed += other.data.Speed;
                flockDir += other.data.TargetDirection;
                steerPos += other.data.Position;
                count++;
            }
            if (i != j)
            {
                if (dist < flock.separationDistance)
                {
                    Vector3 targetDirection = (
                      curr.data.Position -
                      other.data.Position).normalized;

                    separationDir += targetDirection;
                    separationSpeed += dist * flock.weightSeparation;
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

        //removed cus doesnt seem to be doing anything
        //if (separationCount > 0)
        //{
        //    separationSpeed = separationSpeed / count;
        //    separationDir = separationDir / separationSpeed;
        //    separationDir.Normalize();
        //}

        curr.data.TargetDirection =
          flockDir * speed * (flock.useAlignmentRule ? flock.weightAlignment : 0.0f) +
          separationDir * separationSpeed * (flock.useSeparationRule ? flock.weightSeparation : 0.0f) +
          (steerPos - curr.data.Position) * (flock.useCohesionRule ? flock.weightCohesion : 0.0f);
    }

    #region TEMP HIDE

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




                    for (int i = 0; i < autonomousList.Count; ++i)
                    {
                        Execute(flock, i);
                        if (i % BatchSize == 0)
                        {
                            yield return null; // wait a frame for every 100 boids processed
                        }
                    }
                    yield return null; //wait a frame after processing one type of boid
                }
            }
            yield return new WaitForSeconds(TickDuration); //wait for the tick duration before looping
        }
    }

    //logic for boid to separate from enemies
    void SeparationWithEnemies_Internal(
      List<Autonomous> boids,
      List<Autonomous> enemies,
      float sepDist,
      float sepWeight)
    {
        for (int i = 0; i < boids.Count; ++i)
        {
            for (int j = 0; j < enemies.Count; ++j)
            {
                float dist = (
                  enemies[j].data.Position -
                  boids[i].data.Position).magnitude;
                if (dist < sepDist)
                {
                    Vector3 targetDirection = (
                      boids[i].data.Position -
                      enemies[j].data.Position).normalized;

                    boids[i].data.TargetDirection += targetDirection;
                    boids[i].data.TargetDirection.Normalize();

                    boids[i].data.TargetSpeed += dist * sepWeight;
                    boids[i].data.TargetSpeed /= 2.0f;
                }
            }
        }
    }

    IEnumerator Coroutine_SeparationWithEnemies()
    {
        while (true)
        {
            foreach (Flock flock in flocks)
            {
                if (!flock.useFleeOnSightEnemyRule || flock.isPredator) continue;

                foreach (Flock enemies in flocks)
                {
                    if (!enemies.isPredator) continue;

                    SeparationWithEnemies_Internal(
                      flock.mAutonomous,
                      enemies.mAutonomous,
                      flock.enemySeparationDistance,
                      flock.weightFleeOnSightEnemy);
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
                    for (int i = 0; i < autonomousList.Count; ++i)
                    {
                        for (int j = 0; j < mObstacles.Count; ++j)
                        {
                            float dist = (
                              mObstacles[j].transform.position -
                              autonomousList[i].data.Position).magnitude;
                            if (dist < mObstacles[j].AvoidanceRadius)
                            {
                                Vector3 targetDirection = (
                                  autonomousList[i].data.Position -
                                  mObstacles[j].transform.position).normalized;

                                autonomousList[i].data.TargetDirection += targetDirection * flock.weightAvoidObstacles;
                                autonomousList[i].data.TargetDirection.Normalize();
                            }
                        }
                    }
                }
                //yield return null;
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

    #endregion

    public struct FlockJob : IJobParallelFor
    {
        public void Execute(int index)
        {
            throw new System.NotImplementedException();
        }
    }
}


