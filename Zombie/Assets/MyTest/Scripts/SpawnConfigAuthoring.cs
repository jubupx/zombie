using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

public class SpawnConfigAuthoring : MonoBehaviour
{
    public GameObject ZombiePrefab;
    public GameObject BlockPrefab;
    
    public int ZombieCount = 5;
    public float Radus = 10.0f;
    public float ZombieSpeed = 1.0f;
    public float ZombieAccSpeed = 6.0f;
    
    class Baker : Baker<SpawnConfigAuthoring>
    {
        public override void Bake(SpawnConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            var centerPos = authoring.transform.position;
            
            AddComponent(entity, new SpawnConfig
            {
                ZombiePrefab = GetEntity(authoring.ZombiePrefab, TransformUsageFlags.Dynamic),
                BlockPrefab = GetEntity(authoring.BlockPrefab, TransformUsageFlags.Dynamic),
                DefaultZombieCount = authoring.ZombieCount,
                Center = new float3(centerPos.x, centerPos.y, centerPos.z),
                Radus = authoring.Radus,
                ZombieSpeed = authoring.ZombieSpeed,
                zAccSpeed =  authoring.ZombieAccSpeed,
                Xrandom = new Random(12300000),
            });
        }
    }
}

public struct SpawnConfig : IComponentData 
{
    public Entity ZombiePrefab;
    public Entity BlockPrefab;
    public int DefaultZombieCount;
    public float LatestSpawnTime;
    public float3 Center;
    public float Radus;
    public float ZombieSpeed;
    public float zAccSpeed;
    public Random Xrandom;
}


public struct SpawnRequest : IComponentData
{
    public int ZombieCount;
}
