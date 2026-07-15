using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Wendao.Systems.World
{
    public sealed class BlackwindNavigationSurface : MonoBehaviour
    {
        private readonly List<NavMeshBuildSource> _sources =
            new List<NavMeshBuildSource>();
        private readonly List<NavMeshBuildMarkup> _markups =
            new List<NavMeshBuildMarkup>();

        private NavMeshData _data;
        private NavMeshDataInstance _instance;

        public bool IsBuilt => _data != null && _instance.valid;

        private void OnDisable()
        {
            RemoveData();
        }

        public bool Rebuild()
        {
            RemoveData();
            _sources.Clear();
            _markups.Clear();
            NavMeshBuilder.CollectSources(
                transform,
                ~0,
                NavMeshCollectGeometry.PhysicsColliders,
                0,
                _markups,
                _sources);
            if (_sources.Count == 0 || NavMesh.GetSettingsCount() <= 0)
            {
                return false;
            }

            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(0);
            settings.overrideVoxelSize = true;
            settings.voxelSize = 0.12f;
            settings.overrideTileSize = true;
            settings.tileSize = 128;
            var bounds = new Bounds(
                new Vector3(0f, 2f, 45f),
                new Vector3(28f, 10f, 120f));
            _data = NavMeshBuilder.BuildNavMeshData(
                settings,
                _sources,
                bounds,
                Vector3.zero,
                Quaternion.identity);
            if (_data == null)
            {
                return false;
            }

            _data.name = "NavMesh_Blackwind_Runtime";
            _instance = NavMesh.AddNavMeshData(_data);
            return _instance.valid;
        }

        private void RemoveData()
        {
            if (_instance.valid)
            {
                _instance.Remove();
            }

            _instance = default;
            _data = null;
        }
    }
}
