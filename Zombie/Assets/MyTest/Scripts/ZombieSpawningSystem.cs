using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using Random = Unity.Mathematics.Random;
using Unity.Transforms;

partial struct ZombieSpawningSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SpawnConfig>();
    }
 
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Enabled = false;
        
        var config = SystemAPI.GetSingletonRW<SpawnConfig>();
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var zombies = new NativeArray<Entity>(config.ValueRO.DefaultZombieCount, Allocator.Temp);
        ecb.Instantiate(config.ValueRO.ZombiePrefab, zombies);
        
        //var query = SystemAPI.QueryBuilder().WithAll<LocalToWorld>().Build();
        // An EntityQueryMask provides an efficient test of whether a specific entity would
        // be selected by an EntityQuery.
        //var queryMask = query.GetEntityQueryMask();
        
        foreach (var zombie in zombies)
        {
            // Every root entity instantiated from a prefab has a LinkedEntityGroup component, which
            // is a list of all the entities that make up the prefab hierarchy.
            // ecb.SetComponentForLinkedEntityGroup(tank, queryMask,
            //     new URPMaterialPropertyBaseColor { Value = RandomColor(ref random) });
            //ecb.SetComponent(zombie, new LocalToWorld {Value = });

            var spawnLoc = ComputeTransform(ref config.ValueRW);
            var zSpeed = config.ValueRO.ZombieSpeed;
            var zAccSpeed = config.ValueRO.zAccSpeed;
            var zAngle = config.ValueRW.Xrandom.NextFloat() * math.PI * 0.2f;
            var zAngleSpeed = config.ValueRW.Xrandom.NextFloat() * 0.15f + 0.05f;

            zSpeed = zSpeed * 0.4f + zSpeed * 0.6f * config.ValueRW.Xrandom.NextFloat();
            
            ecb.SetComponent(zombie, spawnLoc);
            ecb.SetComponent(zombie, new Zombie()
            {
                Speed = zSpeed,
                AccSpeed = zAccSpeed,
                PreVelocity = float3.zero,
                PreLoction = spawnLoc.Position,
                PreAngle =  zAngle,
                AngleSpeed = zAngleSpeed,
                mass = 1,
            });
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
        zombies.Dispose();
    }
    
    public LocalTransform ComputeTransform(ref SpawnConfig config)
    {
        var halfR = config.Xrandom.NextFloat() * config.Radus;
        var angle = config.Xrandom.NextFloat() * math.PI * 2.0f;
        
        float x = math.cos(angle) * halfR + config.Center.x;
        float z = math.sin(angle) * halfR + config.Center.z;
        float y = 0 + config.Center.y;
        
       // Debug.Log( string.Format("create {0} {1} {2} {3} {4}", halfR, angle, x, y, z));
        
        /*
         float ObjectScale = 1.0f;
         
        float4x4 M = float4x4.TRS(
            new float3(x, y, z) + config.Center,
            quaternion.identity,
            new float3(ObjectScale));
        
        return M;
        */

        return LocalTransform.FromPosition(x, y, z);
    }
}
