using NUnit.Framework;
using Potshot;
using UnityEngine;

namespace Potshot.Tests
{
    /// <summary>Feel targets from docs/gameplay.md, asserted so blind
    /// tuning can't drift silently.</summary>
    public class TankSpecTests
    {
        TankSpec Load()
        {
            var spec = Resources.Load<TankSpec>("Specs/TankSpec");
            Assert.That(spec, Is.Not.Null,
                "TankSpec.asset missing — run PrefabFactory.CreateAll");
            return spec;
        }

        [Test]
        public void TopSpeed_MatchesFeelTarget() =>
            Assert.That(Load().topSpeed, Is.EqualTo(6f).Within(0.001f));

        [Test]
        public void Accel_ReachesTopSpeedInPoint4Seconds()
        {
            var spec = Load();
            Assert.That(spec.topSpeed / spec.accel, Is.EqualTo(0.4f).Within(0.05f),
                "0→top time drifted from the gameplay.md feel target");
        }

        [Test]
        public void TurretRate_MatchesFeelTarget() =>
            Assert.That(Load().turretDegPerSec, Is.EqualTo(360f).Within(0.001f));
    }
}
