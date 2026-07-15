using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wendao.Systems.World;

namespace Wendao.Tests.EditMode.Systems
{
    public sealed class SceneLoadProgressTests
    {
        [Test]
        public void ReportsAreMonotonicClampedAndReachExactlyOne()
        {
            var progress = new SceneLoadProgress();
            var observed = new List<float>();
            progress.Changed += observed.Add;

            progress.Begin();
            progress.Report(0.25f);
            progress.Report(0.10f);
            progress.Report(float.NaN);
            progress.Report(0.75f);
            progress.Report(1f);
            progress.Report(2f);

            Assert.That(observed, Is.EqualTo(new[] { 0f, 0.25f, 0.75f, 1f }));
            Assert.That(progress.Value, Is.EqualTo(1f));
            Assert.That(progress.Sequence, Is.EqualTo(1));
            for (int i = 1; i < observed.Count; i++)
            {
                Assert.That(observed[i], Is.GreaterThanOrEqualTo(observed[i - 1]));
            }
        }

        [Test]
        public void ThrowingProgressSubscriberDoesNotBlockLaterSubscriber()
        {
            var progress = new SceneLoadProgress();
            var laterCalls = 0;
            LogAssert.Expect(LogType.Exception, new Regex("expected progress failure"));
            progress.Changed += _ =>
                throw new InvalidOperationException("expected progress failure");
            progress.Changed += _ => laterCalls++;

            progress.Begin();

            Assert.That(laterCalls, Is.EqualTo(1));
        }
    }
}
