using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

public class BlockCube : MonoBehaviour
{
    class Baker : Baker<BlockCube>
    {
        public override void Bake(BlockCube authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            var centerPos = authoring.transform.position;
            var Scale3d = authoring.transform.localScale;
            
            AddComponent(entity, new BlockCubeData
            {
                Start = centerPos - Scale3d * 0.5f,
                Size =  Scale3d,
            });
        }
    }
}

public struct BlockCubeData : IComponentData
{
    public float3 Start;
    public float3 Size;
}