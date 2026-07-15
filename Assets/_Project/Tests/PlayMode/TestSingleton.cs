using Wendao.Core;

namespace Wendao.Tests.PlayMode
{
    public sealed class TestSingleton : Singleton<TestSingleton>
    {
        public int AwakeHookCalls { get; private set; }

        protected override void OnSingletonAwake()
        {
            AwakeHookCalls++;
        }
    }
}

