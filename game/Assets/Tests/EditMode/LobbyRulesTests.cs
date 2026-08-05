using NUnit.Framework;
using Potshot;

namespace Potshot.Tests
{
    public class LobbyRulesTests
    {
        [Test]
        public void Leader_IsLowestClientId() =>
            Assert.That(LobbyRules.PickLeader(new[] { 7, 2, 9 }), Is.EqualTo(2));

        [Test]
        public void Leader_MigratesWhenLowestLeaves() =>
            Assert.That(LobbyRules.PickLeader(new[] { 7, 9 }), Is.EqualTo(7));

        [Test]
        public void Leader_EmptyLobbyIsMinusOne() =>
            Assert.That(LobbyRules.PickLeader(System.Array.Empty<int>()), Is.EqualTo(-1));

        [Test]
        public void Settings_AreClamped()
        {
            Assert.That(LobbyRules.ClampBots(-3), Is.EqualTo(0));
            Assert.That(LobbyRules.ClampBots(99), Is.EqualTo(6));
            Assert.That(LobbyRules.ClampKillTarget(1), Is.EqualTo(5));
            Assert.That(LobbyRules.ClampKillTarget(100), Is.EqualTo(25));
        }
    }
}
