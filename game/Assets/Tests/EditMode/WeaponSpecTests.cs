using NUnit.Framework;
using Potshot;
using UnityEngine;

namespace Potshot.Tests
{
    public class WeaponSpecTests
    {
        static WeaponSpec Load(string id)
        {
            var spec = Resources.Load<WeaponSpec>($"Specs/Weapons/{id}");
            Assert.That(spec, Is.Not.Null, $"{id} spec missing — run PrefabFactory.CreateAll");
            return spec;
        }

        [Test]
        public void AllFourWeapons_Exist()
        {
            foreach (var id in new[] { "cannon", "spread", "mortar", "mg" })
                Load(id);
        }

        [Test]
        public void Cannon_IsTwoHitKill()
        {
            var cannon = Load("cannon");
            Assert.That(cannon.damage * 2f, Is.GreaterThanOrEqualTo(100f));
            Assert.That(cannon.damage, Is.LessThan(100f), "one-hit kill is not the design");
            Assert.That(cannon.ricochets, Is.EqualTo(1));
        }

        [Test]
        public void Mortar_IsAoeOnly_WithGravity()
        {
            var mortar = Load("mortar");
            Assert.That(mortar.useGravity, Is.True);
            Assert.That(mortar.aoeRadius, Is.GreaterThan(0f));
        }

        [Test]
        public void AmmoTable_MatchesDesign()
        {
            Assert.That(Load("cannon").ammo, Is.EqualTo(0), "cannon is infinite");
            Assert.That(Load("spread").ammo, Is.EqualTo(8));
            Assert.That(Load("mortar").ammo, Is.EqualTo(5));
            Assert.That(Load("mg").ammo, Is.EqualTo(40));
        }

        [Test]
        public void AllWeapons_HaveMaterialAssets()
        {
            foreach (var id in new[] { "cannon", "spread", "mortar", "mg" })
                Assert.That(Load(id).projectileMaterial, Is.Not.Null, id);
        }
    }
}
