using UnityEngine;
using Wendao.Core;

namespace Wendao.Systems.World
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class BlackwindDungeonDoor : MonoBehaviour
    {
        [SerializeField, Range(1, 4)] private int _requiredFloor = 1;
        private Renderer _renderer;
        private Collider _collider;

        public int RequiredFloor => _requiredFloor;
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _collider = GetComponent<Collider>();
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        public void Configure(int requiredFloor)
        {
            _requiredFloor = Mathf.Clamp(requiredFloor, 1, 4);
            _renderer = GetComponent<Renderer>();
            _collider = GetComponent<Collider>();
            Refresh();
        }

        public void Refresh()
        {
            IsOpen = ServiceLocator.TryGet<IBlackwindDungeonService>(
                    out IBlackwindDungeonService dungeon)
                && dungeon.IsFloorComplete(_requiredFloor);
            if (_renderer != null)
            {
                _renderer.enabled = !IsOpen;
            }

            if (_collider != null)
            {
                _collider.enabled = !IsOpen;
            }
        }
    }
}
