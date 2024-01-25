using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Jobs;
using Unity.Burst;

public class FlockCSHandler : MonoBehaviour
{
    #region References
    [SerializeField]
    ComputeShader cs;
    [SerializeField]
    BoxCollider2D box;
    
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

    int numBoids = 100;
    public int currNum = 0;

    [SerializeField]
    List<Flock> flocks = new List<Flock>();
    #endregion

    #region Arrays
    List<bd> boidL;
    List<GameObject> boidObjList;
    #endregion

    Move m;
    TransformAccessArray transformArray;

    #region MONOBEHAVIOUR METHODS

    public void Initialize()
    {
        boundsMinX = box.bounds.min.x;
        boundsMaxX = box.bounds.max.x;
        boundsMinY = box.bounds.min.y;
        boundsMaxY = box.bounds.max.y;

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

        boidL = new();
        boidObjList = new();
        transformArray = new(0);

        CreateFlocks();
    }

    private void Update()
    {
        Calculate();
        m = new Move
        {
            a = boidL.ToNativeArray(Allocator.TempJob)
        };
        JobHandle moveJobHandle = m.Schedule(transformArray);
        moveJobHandle.Complete();
    }

    private void OnDisable()
    {
        boidL.Clear();

        foreach (var item in boidObjList)
        {
            Destroy(item);
        }

        boidObjList.Clear();
        transformArray.Dispose();
        currNum = 0;
    }

    #endregion

    void CreateFlocks()
    {
        AddBoids(flocks[0].numBoids,flocks[0].PrefabBoid);
    }

    void AddBoids(int num, GameObject prefab)
    {
        for (int i = 0; i < num; i++)
        {
            AddBoid(prefab);
        }
    }

    void AddBoid(GameObject prefab)
    {
        //get random x,y value
        float x = Random.Range(boundsMinX, boundsMaxX);
        float y = Random.Range(boundsMinY, boundsMaxY);
        GameObject boidObj = Instantiate(prefab, new Vector3(x, y), Quaternion.identity);
        boidObj.name = $"Boid_{currNum}";

        boidObjList.Add(boidObj);

        currNum++;
        boidL.Add(new bd
        {
            pos = new(x, y),
            dir = Vector3.zero,
            spd = maxSpd,
        });

        transformArray.Add(boidObj.transform);
    }


    #region SHADER METHODS
    void Calculate()
    {
        int kernelIndex = cs.FindKernel("CSMain");
        ComputeBuffer buffer = new(currNum,28);
        buffer.SetData(boidL);

        //setting the data in the shader
        cs.SetBuffer(kernelIndex,"Boid",buffer);

        cs.SetInt("size",currNum);

        cs.SetBool("useAlignmentRule",useAlignmentRule);
        cs.SetBool("useSeparationRule",useSeparationRule);
        cs.SetBool("useCohesionRule",useCohesionRule);


        cs.SetFloat("timeDelta", Time.deltaTime);
        cs.SetFloat("visibility", visibility);
        cs.SetFloat("weightSep",weightSep);
        cs.SetFloat("weightAlign",weightAlign);
        cs.SetFloat("weightCoh",weightCoh);
        cs.SetFloat("sepDistance",sepDist);

        //assigning the correct thread per group
        int tpg = Mathf.CeilToInt(currNum / 8);
        cs.Dispatch(kernelIndex, tpg, tpg, 1);

        bd[] b = new bd[currNum];
        buffer.GetData(b);
        boidL = new(b);

        buffer.Dispose();
    }
    #endregion


    [BurstCompile]
    struct Move : IJobParallelForTransform
    {
        public NativeArray<bd> a;

        public void Execute(int index, TransformAccess transform)
        {
            transform.position = a[index].pos;
        }
    }
}
