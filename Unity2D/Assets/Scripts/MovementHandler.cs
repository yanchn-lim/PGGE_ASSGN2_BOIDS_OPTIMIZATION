using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;

public class MovementHandler : MonoBehaviour
{
    public FlockBehaviour fb;

    public void Update()
    {
        Transform[] transformArray = new Transform[fb.allObj.Count];
        NativeArray<AutonomousData> dArray = new(fb.allObj.Count, Allocator.TempJob);

        for (int i = 0; i < fb.allObj.Count; i++)
        {
            transformArray[i] = fb.allObj[i].transform;
            dArray[i] = fb.allObj[i].data;
        }

        if (transformArray.Length > 0)
        {
            TransformAccessArray tArray = new(transformArray);

            MoveJob job = new(dArray, Time.deltaTime);
            JobHandle handle = job.Schedule(tArray);
            handle.Complete();

            for (int i = 0; i < fb.allObj.Count; i++)
            {
                fb.allObj[i].data = dArray[i];
            }

            dArray.Dispose();
            tArray.Dispose();
        }
        else
        {
            Debug.Log("transform array is empty");
        }

    }

    public IEnumerator Move()
    {
        while (true)
        {
            Transform[] transformArray = new Transform[fb.allObj.Count];
            NativeArray<AutonomousData> dArray = new(fb.allObj.Count,Allocator.TempJob);

            for (int i = 0; i < fb.allObj.Count; i++)
            {
                transformArray[i] = fb.allObj[i].transform;
                dArray[i] = fb.allObj[i].data;
            }

            if(transformArray.Length > 0)
            {
                TransformAccessArray tArray = new(transformArray);

                MoveJob job = new(dArray, Time.deltaTime);
                JobHandle handle = job.Schedule(tArray);
                handle.Complete();

                for (int i = 0; i < fb.allObj.Count; i++)
                {
                    fb.allObj[i].data = dArray[i];
                }

                dArray.Dispose();
                tArray.Dispose();
            }
            else
            {
                Debug.Log("transform array is empty");
            }


            yield return null;
        }
    }

    [BurstCompile]
    struct MoveJob : IJobParallelForTransform
    {
        NativeArray<AutonomousData> obj;
        float deltaTime;

        public void Execute(int index, TransformAccess transform)
        {
            
            AutonomousData data = obj[index];

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
              data.RotationSpeed * deltaTime * 10);

            data.Speed = data.Speed + ((data.TargetSpeed - data.Speed) / 10.0f) * deltaTime;

            if (data.Speed > data.MaxSpeed)
                data.Speed = data.MaxSpeed;


            Vector3 dir = Quaternion.Euler(0, 0, transform.rotation.eulerAngles.z) * Vector3.right;
            transform.position +=  data.Speed * deltaTime * dir;
            data.Position = transform.position;
        }

        public MoveJob(NativeArray<AutonomousData> obj,float time)
        {
            this.obj = obj;
            deltaTime = time;
        }
    }
}
