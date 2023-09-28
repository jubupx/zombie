using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

public class TargetConfigAuthoring : MonoBehaviour
{
    class Baker : Baker<TargetConfigAuthoring>
    {
        public override void Bake(TargetConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            //var centerPos = authoring.transform.position;
            
            AddComponent(entity, new TargetConfigData
            {
                
            });
        }
    }
}

public struct TargetConfigData : IComponentData 
{
    
}
