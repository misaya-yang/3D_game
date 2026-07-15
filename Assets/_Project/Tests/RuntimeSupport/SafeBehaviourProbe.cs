using System;
using Wendao.Core;

namespace Wendao.Tests.EditMode
{
    public sealed class SafeBehaviourProbe : SafeBehaviour
    {
        public bool ThrowOnStart;
        public int SafeStartCalls;
        public Exception ObservedFailure;

        public void InvokeStartForTest()
        {
            base.Start();
        }

        protected override void SafeStart()
        {
            SafeStartCalls++;
            if (ThrowOnStart)
            {
                throw new InvalidOperationException("expected safe start failure");
            }
        }

        protected override void OnSafeStartFailed(Exception exception)
        {
            ObservedFailure = exception;
        }
    }
}
