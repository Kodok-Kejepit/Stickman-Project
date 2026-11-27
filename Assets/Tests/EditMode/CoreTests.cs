using NUnit.Framework;
using Stickman.Core;

namespace Stickman.Tests
{
    public sealed class FrameRateTests
    {
        [TestCase(60.0, 0, 60)]
        [TestCase(90.0, 0, 90)]
        [TestCase(120.0, 0, 120)]
        [TestCase(59.94, 0, 60)]
        [TestCase(144.0, 0, 120)]
        [TestCase(0.0, 0, 60)]
        [TestCase(double.NaN, 0, 60)]
        [TestCase(120.0, 60, 60)]
        [TestCase(120.0, 90, 90)]
        [TestCase(60.0, 120, 60)]
        [TestCase(24.0, 0, 30)]
        public void ChooseTarget(double refreshHz, int preferred, int expected)
        {
            Assert.That(FrameRate.ChooseTarget(refreshHz, preferred), Is.EqualTo(expected));
        }
    }

    public sealed class FrameStatsTests
    {
        [Test]
        public void EmptyStatsReportZero()
        {
            var s = new FrameStats(10);
            Assert.That(s.AverageFps(), Is.EqualTo(0));
            Assert.That(s.PercentileMs(99), Is.EqualTo(0));
        }

        [Test]
        public void AverageFpsFromSteadyFrames()
        {
            var s = new FrameStats(100);
            for (int i = 0; i < 100; i++) s.Add(1f / 60f);
            Assert.That(s.AverageFps(), Is.EqualTo(60.0).Within(0.01));
        }

        [Test]
        public void P99CatchesTheSpike()
        {
            var s = new FrameStats(100);
            for (int i = 0; i < 99; i++) s.Add(0.010f);
            s.Add(0.050f);
            Assert.That(s.PercentileMs(50), Is.EqualTo(10.0).Within(0.01));
            Assert.That(s.PercentileMs(99), Is.EqualTo(10.0).Within(0.01));
            Assert.That(s.PercentileMs(100), Is.EqualTo(50.0).Within(0.01));
        }

        [Test]
        public void SamplesBeyondCapacityAreIgnored()
        {
            var s = new FrameStats(3);
            for (int i = 0; i < 10; i++) s.Add(0.016f);
            Assert.That(s.Count, Is.EqualTo(3));
        }
    }

    public sealed class CommandLineTests
    {
        static readonly string[] Args = { "Game.x86_64", "-screen-width", "1600", "-autoshot", "/tmp/shots", "-touch-ui" };

        [Test]
        public void ReadsFlagValue()
        {
            Assert.That(CommandLine.Value(Args, "-autoshot"), Is.EqualTo("/tmp/shots"));
            Assert.That(CommandLine.Value(Args, "-screen-width"), Is.EqualTo("1600"));
        }

        [Test]
        public void MissingOrValuelessFlagGivesNull()
        {
            Assert.That(CommandLine.Value(Args, "-selftest"), Is.Null);
            Assert.That(CommandLine.Value(Args, "-touch-ui"), Is.Null);
            Assert.That(CommandLine.Value(new[] { "-autoshot", "-touch-ui" }, "-autoshot"), Is.Null);
            Assert.That(CommandLine.Value(null, "-autoshot"), Is.Null);
        }

        [Test]
        public void HasFlag()
        {
            Assert.That(CommandLine.Has(Args, "-touch-ui"), Is.True);
            Assert.That(CommandLine.Has(Args, "-selftest"), Is.False);
        }
    }
}
