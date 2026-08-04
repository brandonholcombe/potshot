using System.Text.RegularExpressions;
using NUnit.Framework;
using Potshot;

namespace Potshot.Tests
{
    public class GameVersionTests
    {
        [Test]
        public void Version_IsSemverWithOptionalSuffix()
        {
            StringAssert.IsMatch(
                @"^\d+\.\d+\.\d+(\+[0-9A-Za-z.-]+)?$",
                GameVersion.Version);
        }

        [Test]
        public void Version_IsNotPlaceholder()
        {
            Assert.That(GameVersion.Version, Is.Not.EqualTo("0.0.0"));
        }
    }
}
