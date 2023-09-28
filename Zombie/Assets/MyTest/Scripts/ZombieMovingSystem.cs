using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using Random = Unity.Mathematics.Random;
using Unity.Transforms;

partial struct ZombieMovingSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<TargetConfigData>();
        state.RequireForUpdate<Zombie>();
    }

    class EntityCache
    {
        public Entity id;
        public float3 pos;
        public float3 oldPos;
        public float mass;
        public float g;
        public bool ignore;
    }


    private static List<EntityCache> ArrEntitys = new List<EntityCache>();
    static Dictionary<long, List<EntityCache>> MapEntitys = new Dictionary<long, List<EntityCache>>();
    

    private static int GRIDX = 100000000;
    private static int GRIDZ = 100000;
    private static int GRIDY = 1;
    
    private static float INTERACTION_RADIUS = 2f;
    private static float INTERACTION_RADIUS_SQ = 4.0f;
    private static float GRID_SIZE = 1f;
    private static int INTERACTION_GRID_COUNT = 2;
    private static float STIFFNESS = 20;
    private static float STIFFNESS_NEAR = 50;
    private static float REST_DENSITY = 0;
    public static float MAX_MASS = 3;
    
    static long PositionKey(float3 pos)
    {
        return (long)pos.x * GRIDX + (long)pos.z * GRIDZ + (long)pos.y * GRIDY;
    }
    
    static void ClearEntityMap()
    {
        ArrEntitys.Clear();
        foreach (var grid in MapEntitys)
        {
            grid.Value.Clear();
        }
    }
    
    static EntityCache PushEntity2Map(Entity zombie, float3 p, float3 oldP, float m)
    {
        long key = PositionKey(p);
        List<EntityCache> grids;
        if (!MapEntitys.TryGetValue(key, out grids))
        {
            grids = new List<EntityCache>();
            MapEntitys.Add(key, grids);
        }
        
        var cache = new EntityCache() { id = zombie, pos = p , oldPos =  oldP, mass = m, ignore = false};
        
        grids.Add(cache);
        ArrEntitys.Add(cache);
        
        return cache;
    }
    
    static float Gradient(float3 np, float3 p)
    {
        var lsq  = math.lengthsq(np - p);
        if (lsq > INTERACTION_RADIUS_SQ) return 0;
        return math.max(0, (1.0f - math.sqrt(lsq) / INTERACTION_RADIUS));
    }
    
    static List<EntityCache> GetNeighboursWithGradients( EntityCache zombieCache
        , out float pDensity
        , out float pDensityNear)
    {
        float density = 0;
        float nearDensity = 0;
        
        List<EntityCache> neighbours = new List<EntityCache>();

        var p = zombieCache.pos;
        long gx = (long)p.x; long gz = (long)p.z; long gy = (long)p.y;
        
        for (long i = gx - INTERACTION_GRID_COUNT; i <= gx + INTERACTION_GRID_COUNT; i++)
        {
            if(i < 0) continue;
            for (long j = gz - INTERACTION_GRID_COUNT; j <= gz + INTERACTION_GRID_COUNT; j++)
            {
                if(j < 0) continue;
                for (long k = gy - INTERACTION_GRID_COUNT; k <= gy + INTERACTION_GRID_COUNT; k++)
                {
                    if(k < 0) continue;
                    long key = i * GRIDX + j * GRIDZ + k * GRIDY;
                    
                    List<EntityCache> Rets;
                    if (MapEntitys.TryGetValue(key, out Rets)) 
                    {
                        for (int ci = 0; ci < Rets.Count; ci ++) 
                        {
                            var cache = Rets[ci];
                            if(cache.id == zombieCache.id) continue;
                            var g = Gradient(cache.pos, p);
                            if (g == 0) continue;
                            cache.g = g;
                            
                            ///计算 mass
                            var m = cache.mass;
                            density += g * g * m;
                            nearDensity += g * g * g * m;
                            
                            neighbours.Add(cache);
                        }
                    }
                }
            }
        }
        
        pDensity = STIFFNESS * (density - REST_DENSITY) * zombieCache.mass;
        pDensityNear = STIFFNESS_NEAR * nearDensity * zombieCache.mass;
        
        return neighbours;
    }
    
    static void Relax(List<EntityCache> neighbors, 
        EntityCache zombieCache
        , float pDensity
         , float pDensityNear
         , float dt, int debugI = 0)
    {
        for (int k = 0; k < neighbors.Count; k++) {
            var n = neighbors[k];
            var g = n.g;
            var nPos = n.pos;
  
            var magnitude = pDensity * g + pDensityNear * g * g;
             // const f = state.color[i] === state.color[n] ? .99 : 1;
            var f = 1.0f;
            var d = math.normalize(nPos - zombieCache.pos) * (magnitude * f * dt * dt);

            var ratej = 0.5f;
            var ratei = 0.5f;
            
            var massI = zombieCache.mass;
            var massJ = n.mass;
            
            var mt = massI + massJ;
            
            ratej = massJ / mt;
            ratei = massI / mt;
            
            zombieCache.pos -= d * ratej;
            n.pos += d * ratei;
            
        }
    }

    static void Contain(EntityCache zombieCache, float3 aimLocation)
    {
        if (zombieCache.pos.y < 0) zombieCache.pos.y = 0;
        
        /*if (zombieCache.pos.z > aimLocation.z)
        {
            zombieCache.pos.z = aimLocation.z;
        }

        var sizeX = 10.0f;
        var min = aimLocation.x - sizeX;
        var max = aimLocation.x + sizeX;

        if (zombieCache.pos.x > max) zombieCache.pos.x = max;
        else if (zombieCache.pos.x < min) zombieCache.pos.x = min;*/

    }
    
    //[BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var configE = SystemAPI.GetSingletonEntity<TargetConfigData>();
        var config = SystemAPI.GetComponentRO<LocalTransform>(configE);

        var aimLocation = config.ValueRO.Position;
        
        var dt = SystemAPI.Time.DeltaTime;
        var et = SystemAPI.Time.ElapsedTime;

        if (dt > 0.02f) dt = 0.02f;

        ///重力加速度
        float3 gf = new float3(0, -100.0f, 0);

        ClearEntityMap();

        foreach (var (transform, zombie, entity) in
                 SystemAPI.Query<RefRW<LocalTransform>,RefRW<Zombie>>().WithEntityAccess())
        {
            ///不可移动物件不计算速度以及位置
            if (zombie.ValueRO.mass >= MAX_MASS)
            {
                var blockE = PushEntity2Map(entity, transform.ValueRO.Position, transform.ValueRO.Position, zombie.ValueRO.mass);
                blockE.ignore = true;
                continue;
            }
            
            var currPos = transform.ValueRO.Position;
            var dir = aimLocation - currPos;
            
            ///分离Y轴速度
            var dirY = dir.y;
            dir.y = 0;
            
            
            //var dir = new float3(0, 0, 1.0f);
            var scaleByDis = math.length(dir) / 10.0f;
            dir = math.normalize(dir);
            var speedV = zombie.ValueRO.AccSpeed * dt;
            var accV = dir * speedV;
            var currV = zombie.ValueRO.Speed * dir + accV * scaleByDis;

            var PreVelocity = zombie.ValueRW.PreVelocity;
           
            ///分离Y轴速度
            var yv = PreVelocity.y;
            var gv = (gf * dt);
            gv.y += yv;
            PreVelocity.y = 0;
            
            PreVelocity = currV  + PreVelocity * 0.90f +  gv;

            // 限制速度 会失去活力
            //if (math.lengthsq(PreVelocity) > 100.0f)
            //{
            //    PreVelocity = math.normalize(PreVelocity) * 10.0f;
            //}
            
            zombie.ValueRW.PreLoction = currPos;
            var newPos = currPos + PreVelocity * dt;
            
            ///不能限到地底下
            if (newPos.y < 0) newPos.y = 0;

            transform.ValueRW.Position = newPos;
            
            ///位置写入
            PushEntity2Map(entity, transform.ValueRO.Position, currPos, zombie.ValueRO.mass);

            ///计算ZOMBIE摇晃
            var angle = zombie.ValueRO.AngleSpeed + zombie.ValueRO.PreAngle;
            var limitAngle = math.PI * 0.2f;
            
            if (angle > limitAngle)
            {
                zombie.ValueRW.AngleSpeed *= -1.0f;
                angle = limitAngle;
            }
            else if (angle < limitAngle * -1.0f)
            {
                zombie.ValueRW.AngleSpeed *= -1.0f;
                angle = limitAngle * -1.0f;
            }
            
            zombie.ValueRW.PreAngle = angle;
            
            var rot = quaternion.LookRotationSafe(math.normalize(PreVelocity), new float3(0,1.0f, 0));
            transform.ValueRW.Rotation = math.mul(rot, quaternion.RotateX(angle));
        }

        ///计算约束
        for (int i = 0; i < ArrEntitys.Count; i++)
        {
            var zombiecache = ArrEntitys[i];
            
            if(zombiecache.ignore) continue;

            float pDensity = 0;
            float pDensityNear = 0;
            var neighbours = GetNeighboursWithGradients(zombiecache, out pDensity
                , out pDensityNear);
            Relax(neighbours, zombiecache, pDensity, pDensityNear, dt, i);
           // if(i == 0)
           //     Debug.Log("get nb count = " + neighbours.Count +" pDensity = " + pDensity + " pDensityNear =" + pDensityNear);
        }
        
        ///更新速度
        for (int i = 0; i < ArrEntitys.Count; i++)
        {
            var zombiecache = ArrEntitys[i];
            if(zombiecache.ignore) continue;
            
            // Contain(zombiecache, aimLocation);
            
            var localt = SystemAPI.GetComponentRW<LocalTransform>(zombiecache.id);
            localt.ValueRW.Position = zombiecache.pos;
            
            var newv = (zombiecache.pos - zombiecache.oldPos) / dt;
            var Zombie = SystemAPI.GetComponentRW<Zombie>(zombiecache.id);
            
            Zombie.ValueRW.PreVelocity = newv;

            if (i == 0)
            {
              //  Debug.Log("v=" + newv + "len=" + math.length(newv));
            }
        }
    }
}
