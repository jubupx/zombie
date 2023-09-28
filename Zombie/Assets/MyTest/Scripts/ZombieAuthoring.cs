using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

public class ZombieAuthoring : MonoBehaviour
{
    class Baker : Baker<ZombieAuthoring>
    {
        public override void Bake(ZombieAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<Zombie>(entity);
        }
    }
}


public struct Zombie : IComponentData
{
    public float Speed;
    public float AccSpeed;
    public float3 PreVelocity;
    public float3 PreLoction;
    public float PreAngle;
    public float AngleSpeed;
    public float mass;
}
