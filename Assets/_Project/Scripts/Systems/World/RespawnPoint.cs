using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Wendao.Systems.World
{
    public sealed class RespawnPoint : MonoBehaviour
    {
        [SerializeField] private string _id = string.Empty;

        public string Id => _id ?? string.Empty;

        public void Configure(string id)
        {
            _id = id?.Trim() ?? string.Empty;
        }

        public static bool TryFindNearest(
            Vector3 origin,
            Scene scene,
            out RespawnPoint nearest)
        {
            nearest = null;
            float nearestDistance = float.PositiveInfinity;
            RespawnPoint[] points = UnityEngine.Object.FindObjectsByType<RespawnPoint>(FindObjectsInactive.Exclude);
            for (int index = 0; index < points.Length; index++)
            {
                RespawnPoint candidate = points[index];
                if (candidate == null
                    || !candidate.isActiveAndEnabled
                    || string.IsNullOrWhiteSpace(candidate.Id)
                    || candidate.gameObject.scene != scene)
                {
                    continue;
                }

                float distance = (candidate.transform.position - origin).sqrMagnitude;
                if (distance < nearestDistance
                    || (Mathf.Approximately(distance, nearestDistance)
                        && nearest != null
                        && string.Compare(
                            candidate.Id,
                            nearest.Id,
                            StringComparison.Ordinal) < 0))
                {
                    nearest = candidate;
                    nearestDistance = distance;
                }
            }

            return nearest != null;
        }
    }
}
