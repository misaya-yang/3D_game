using UnityEngine;
using Wendao.Core;

namespace Wendao.Tests.EditMode
{
    public sealed class PoolProbe : MonoBehaviour, IPoolable
    {
        public int TakenCalls;
        public int ReturnedCalls;

        public void OnTakenFromPool()
        {
            TakenCalls++;
        }

        public void OnReturnedToPool()
        {
            ReturnedCalls++;
        }
    }
}
