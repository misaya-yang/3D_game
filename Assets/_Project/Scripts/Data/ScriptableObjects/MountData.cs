using UnityEngine;

namespace Wendao.Data
{
    [CreateAssetMenu(fileName = "Mount_New", menuName = "问道/坐骑/MountData")]
    public class MountData : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public float SpeedMultiplier;
        public bool CanFly;
        public int RequiredRealm;
        public GameObject Prefab;
    }
}
