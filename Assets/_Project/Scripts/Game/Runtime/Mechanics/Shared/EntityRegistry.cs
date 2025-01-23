using System;
using System.Collections.Generic;
using UnityEngine;

namespace _Project.Mechanics.Shared
{
    /// <summary>Generic list of live entities of type <typeparamref name="T"/>, with nearest-point lookup and
    /// register/unregister events. Feature registries (CellRegistry/MacroVirusRegistry/MicroVirusRegistry) each
    /// compose one internally instead of duplicating this logic. Plain C#, not a MonoBehaviour.</summary>
    public sealed class EntityRegistry<T> where T : Component
    {
        private readonly List<T> _entities = new();

        public IReadOnlyList<T> Entities => _entities;
        public int Count => _entities.Count;

        public event Action<T> OnRegistered;
        public event Action<T> OnUnregistered;

        public void Register(T entity)
        {
            if (entity == null || _entities.Contains(entity))
                return;

            _entities.Add(entity);
            OnRegistered?.Invoke(entity);
        }

        public void Unregister(T entity)
        {
            if (entity == null || !_entities.Remove(entity))
                return;

            OnUnregistered?.Invoke(entity);
        }

        public T FindNearest(Vector3 point)
        {
            return FindNearest(point, out _);
        }

        public T FindNearest(Vector3 point, out float sqrDistance)
        {
            T nearest = null;
            float minSqrDistance = Mathf.Infinity;

            for (int i = 0; i < _entities.Count; i++)
            {
                if (_entities[i] == null)
                    continue;

                float candidateSqrDistance = (_entities[i].transform.position - point).sqrMagnitude;
                if (candidateSqrDistance < minSqrDistance)
                {
                    minSqrDistance = candidateSqrDistance;
                    nearest = _entities[i];
                }
            }

            sqrDistance = minSqrDistance;
            return nearest;
        }
    }
}
