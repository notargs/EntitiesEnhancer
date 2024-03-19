using System.Collections.Generic;
using Unity.Collections;

namespace Unity.Entities
{
    public record struct EntityManager
    {
        public T GetComponentObject<T>(Entity entity) => default;
        public T GetComponentData<T>(Entity entity) => default;
        public RefRW<T> GetComponentDataRW<T>(Entity entity) => default;
    }

    public record struct RefRW<T>(T[] A, int I)
    {
    }
    
    public record struct EntityQueryBuilder(Allocator allocator)
    {
        public EntityQueryBuilder WithAll<T>() => this;
        public EntityQueryBuilder WithAll<T1, T2>() => this;
        public EntityQueryBuilder WithAll<T1, T2, T3>() => this;
        public EntityQueryBuilder WithAll<T1, T2, T3, T4>() => this;
        public EntityQueryBuilder WithNone<T>() => this;
        public EntityQueryBuilder WithNone<T1, T2>() => this;
        public EntityQueryBuilder WithNone<T1, T2, T3>() => this;
        public EntityQueryBuilder WithNone<T1, T2, T3, T4>() => this;
        public EntityQueryBuilder WithAny<T>() => this;
        public EntityQueryBuilder WithAny<T1, T2>() => this;
        public EntityQueryBuilder WithAny<T1, T2, T3>() => this;
        public EntityQueryBuilder WithAny<T1, T2, T3, T4>() => this;
        public EntityQueryBuilder Build(EntityManager entityManager) => this;
        public Entity[] ToEntityArray(Allocator allocator) => [];
        public T[] ToComponentDataArray<T>(Allocator allocator) => [];
        public void CopyFromComponentDataArray<T>(T[] array) { }
        
        public T GetSingleton<T>() => default;
        public RefRW<T> GetSingletonRW<T>() => default;
        public Entity GetSingletonEntity() => default;
    }

    public record struct Entity;

    public interface IComponentData;
}

namespace Unity.Collections
{
    public enum Allocator
    {
        Temp
    }
}