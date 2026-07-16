using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Wendao.Systems.World
{
    /// <summary>
    /// Runtime navigation data for the collider-authoritative Cangwu greybox.
    /// Visual dressing remains collider-free and cannot change walkable routes.
    /// </summary>
    public sealed class CangwuNavigationSurface : MonoBehaviour
    {
        public const float SurfaceWidth = 46f;
        public const float SurfaceDepth = 54f;
        public const float SurfaceHeight = 10f;
        public const float VoxelSize = 0.12f;

        private readonly List<NavMeshBuildSource> _sources =
            new List<NavMeshBuildSource>();
        private readonly List<NavMeshBuildMarkup> _markups =
            new List<NavMeshBuildMarkup>();

        private NavMeshData _data;
        private NavMeshDataInstance _instance;

        public bool IsBuilt => _data != null && _instance.valid;
        public int BuildSourceCount => _sources.Count;
        public Bounds WorldBounds { get; private set; }

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
            settings.voxelSize = VoxelSize;
            settings.overrideTileSize = true;
            settings.tileSize = 128;
            WorldBounds = new Bounds(
                transform.position + new Vector3(0f, 2f, 2f),
                new Vector3(SurfaceWidth, SurfaceHeight, SurfaceDepth));
            _data = NavMeshBuilder.BuildNavMeshData(
                settings,
                _sources,
                WorldBounds,
                Vector3.zero,
                Quaternion.identity);
            if (_data == null)
            {
                return false;
            }

            _data.name = "NavMesh_Cangwu_Runtime";
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
