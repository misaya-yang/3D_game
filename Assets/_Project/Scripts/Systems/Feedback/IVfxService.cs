using UnityEngine;

namespace Wendao.Systems.Feedback
{
    public interface IVfxService
    {
        string LastPlayedVfxId { get; }
        int PlayCount { get; }
        int ActiveCount { get; }

        GameObject Play(
            string vfxId,
            Vector3 position,
            Quaternion rotation,
            float duration = 2f);
        GameObject PlayAttached(
            string vfxId,
            Transform parent,
            float duration);
        void Stop(GameObject instance);
    }
}
