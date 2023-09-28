
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using Random = Unity.Mathematics.Random;
using Unity.Transforms;

partial struct BlockCreateSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BlockCubeData>();
        state.RequireForUpdate<SpawnConfig>();
    }
    
    
    public void OnUpdate(ref SystemState state)
    {
        state.Enabled = false;
        
        var config = SystemAPI.GetSingleton<SpawnConfig>();
        int zcount = 0;

        List<BlockCubeData> allCubeDatas = new List<BlockCubeData>();

        foreach (var (cubeData, entity) in
                 SystemAPI.Query<RefRO<BlockCubeData>>().WithEntityAccess())
        {
            var cubeSize = cubeData.ValueRO.Size;
            var maxx = (int)cubeSize.x;
            var maxy = (int)cubeSize.y;
            var maxz = (int)cubeSize.z;
            
            zcount += maxx *  maxy * maxz;
            
            allCubeDatas.Add(cubeData.ValueRO);
        }
        
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var zombies = new NativeArray<Entity>(zcount, Allocator.Temp);
            
        ecb.Instantiate(config.BlockPrefab, zombies);

        var startIndex = 0;
        
        foreach (var cubeData in allCubeDatas)
        {
            startIndex = CreatePoints(cubeData, ref zombies, ref ecb, startIndex);
        }
        

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
        zombies.Dispose();
    }


    public int CreatePoints(BlockCubeData cubeData
        ,ref NativeArray<Entity> zombies
        ,ref EntityCommandBuffer  ecb, int start)
    {
        var cubeSize = cubeData.Size;
        var maxx = (int)cubeSize.x;
        var maxy = (int)cubeSize.y;
        var maxz = (int)cubeSize.z;

        int count = maxx * maxy * maxz;
        
        //var query = SystemAPI.QueryBuilder().WithAll<LocalToWorld>().Build();
        // An EntityQueryMask provides an efficient test of whether a specific entity would
        // be selected by an EntityQuery.
        //var queryMask = query.GetEntityQueryMask();
        int i = 0, j = 0, k = 0;
        
        for(int index = start; index < start + count; index ++)
        {
            var zombie = zombies[index];
            // Every root entity instantiated from a prefab has a LinkedEntityGroup component, which
            // is a list of all the entities that make up the prefab hierarchy.
            // ecb.SetComponentForLinkedEntityGroup(tank, queryMask,
            //     new URPMaterialPropertyBaseColor { Value = RandomColor(ref random) });
            //ecb.SetComponent(zombie, new LocalToWorld {Value = });
                
            var spawnLoc = LocalTransform.FromPosition(cubeData.Start + new float3(i , j, k));
            ecb.SetComponent(zombie, spawnLoc);
            ecb.SetComponent(zombie, new Zombie()
            {
                Speed = 0,
                AccSpeed = 0,
                PreVelocity = float3.zero,
                PreLoction = float3.zero,
                PreAngle =  0,
                AngleSpeed = 0,
                mass = ZombieMovingSystem.MAX_MASS,
            });

            i += 1;
            if (i >= maxx)
            {
                i = 0;
                j += 1;

                if (j >= maxy)
                {
                    k += 1;
                    j = 0;
                }
            }
        }
        
        return start + count;
    }
    
}