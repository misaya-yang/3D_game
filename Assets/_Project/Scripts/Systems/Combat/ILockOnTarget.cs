using UnityEngine;

namespace Wendao.Systems.Combat
{
    public interface ILockOnTarget : IDamageable
    {
        bool CanLockOn { get; }
        Transform LockOnTransform { get; }
        float LockOnDisengageRange { get; }
    }
}
