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
        public void FishNet_IsPresent()
        {
            // Compilation of Potshot.Net against FishNet.Runtime is itself
            // the real check; this documents the dependency explicitly.
            Assert.That(typeof(FishNet.Managing.NetworkManager), Is.Not.Null);
        }
    }
}
