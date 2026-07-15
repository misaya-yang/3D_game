using System;
using UnityEngine;

namespace Wendao.Core
{
    /// <summary>
    /// MonoBehaviour base that contains initialization failures at the component boundary.
    /// Gameplay classes override <see cref="SafeStart"/> instead of Unity's Start message.
    /// </summary>
    public abstract class SafeBehaviour : MonoBehaviour
    {
        protected void Start()
        {
            try
            {
                SafeStart();
            }
            catch (Exception exception)
            {
                enabled = false;
                Debug.LogException(exception, this);

                try
                {
                    OnSafeStartFailed(exception);
                }
                catch (Exception failureHandlerException)
                {
                    Debug.LogException(failureHandlerException, this);
                }
            }
        }

        protected virtual void SafeStart()
        {
        }

        protected virtual void OnSafeStartFailed(Exception exception)
        {
        }
    }
}
