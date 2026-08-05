using NUnit.Framework;
using Potshot.Net;

namespace Potshot.Tests
{
    public class NetVersionTests
    {
        [Test]
        public void MatchingVersions_Pass() =>
            Assert.That(VersionAuthenticator.VersionsMatch("1.2.3+dev", "1.2.3+dev"), Is.True);

        [Test]
        public void MismatchedVersions_Fail() =>
            Assert.That(VersionAuthenticator.VersionsMatch("1.2.3+dev", "1.2.4+dev"), Is.False);

        [Test]
        public void MalformedClientVersions_Fail()
        {
            Assert.That(VersionAuthenticator.VersionsMatch(null, "1.0.0"), Is.False);
            Assert.That(VersionAuthenticator.VersionsMatch("", "1.0.0"), Is.False);
            Assert.That(VersionAuthenticator.VersionsMatch("   ", "1.0.0"), Is.False);
        }

        [Test]
        public void Startup_DefaultsToMenuInPlayerBuilds()
        {
            // The kodloki default lives on the Play Online button now.
            var (action, _) = NetBootstrap.ResolveStartup(
                new[] { "/path/to/app" }, isEditor: false);
            Assert.That(action, Is.EqualTo(NetBootstrap.StartupAction.Menu));
        }

        [Test]
        public void Startup_EditorStaysOffline() =>
            Assert.That(NetBootstrap.ResolveStartup(new string[0], isEditor: true).action,
                Is.EqualTo(NetBootstrap.StartupAction.Offline));

        [Test]
        public void Startup_ExplicitArgsWin()
        {
            Assert.That(NetBootstrap.ResolveStartup(
                new[] { "-potshotOffline" }, false).action,
                Is.EqualTo(NetBootstrap.StartupAction.Offline));
            Assert.That(NetBootstrap.ResolveStartup(
                new[] { "-potshotServer" }, false).action,
                Is.EqualTo(NetBootstrap.StartupAction.Server));
            Assert.That(NetBootstrap.ResolveStartup(
                new[] { "-potshotClient", "example.com" }, false).host,
                Is.EqualTo("example.com"));
        }

        [Test]
        public void FishNet_IsPresent()
        {
            // Compilation of Potshot.Net against FishNet.Runtime is itself
            // the real check; this documents the dependency explicitly.
            Assert.That(typeof(FishNet.Managing.NetworkManager), Is.Not.Null);
        }
    }
}
