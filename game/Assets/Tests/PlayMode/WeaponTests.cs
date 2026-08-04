using System.Collections;
using NUnit.Framework;
using Potshot;
using UnityEngine;
using UnityEngine.TestTools;

namespace Potshot.Tests
{
    /// <summary>Weapon behavior against the real prefabs, primitives-only
    /// environment (m1c convention).</summary>
    public class WeaponTests
    {
        readonly System.Collections.Generic.List<GameObject> _env = new();
        ScriptedTankInput _input;
        WeaponController _weapon;
        GameObject _shooter;

        /// <summary>Register any spawned object for guaranteed teardown —
        /// a failed assert must not leak walls into later tests (that
        /// cascade caused 2 phantom failures in the first M1d run).</summary>
        T Env<T>(T obj) where T : Object
        {
            var go = obj as GameObject ?? (obj as Component)?.gameObject;
            if (go != null) _env.Add(go);
            return obj;
        }

        [SetUp]
        public void SetUp()
        {
            var ground = Env(GameObject.CreatePrimitive(PrimitiveType.Plane));
            ground.transform.localScale = new Vector3(10f, 1f, 10f);

            _shooter = SpawnTank(new Vector3(0f, 0.1f, 0f), out _input);
            _weapon = _shooter.GetComponent<WeaponController>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var p in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                Object.Destroy(p.gameObject);
            foreach (var go in _env)
                if (go != null) Object.Destroy(go);
            _env.Clear();
        }

        GameObject SpawnTank(Vector3 pos, out ScriptedTankInput input)
        {
            var prefab = Resources.Load<GameObject>("Prefabs/Tank");
            var tank = Env(Object.Instantiate(prefab, pos, Quaternion.identity));
            input = new ScriptedTankInput();
            tank.GetComponent<TankController>().InputSource = input;
            return tank;
        }

        static WeaponSpec Spec(string id) => Resources.Load<WeaponSpec>($"Specs/Weapons/{id}");

        IEnumerator FireOnce(Vector3 aim)
        {
            _input.Next = new TankInputSample { Fire = true, AimWorldPos = aim };
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            _input.Next = new TankInputSample { AimWorldPos = aim };
        }

        [UnityTest]
        public IEnumerator Cannon_ShellFliesAtSpecSpeed()
        {
            _weapon.Equip(Spec("cannon"));
            yield return FireOnce(new Vector3(0f, 0f, 50f));
            var shell = Object.FindFirstObjectByType<Projectile>();
            Assert.That(shell, Is.Not.Null, "no shell spawned");
            float speed = shell.GetComponent<Rigidbody>().linearVelocity.magnitude;
            Assert.That(speed, Is.EqualTo(14f).Within(0.5f));
        }

        [UnityTest]
        public IEnumerator Cannon_TwoHitsKill()
        {
            _weapon.Equip(Spec("cannon"));
            var target = SpawnTank(new Vector3(0f, 0.1f, 10f), out _);
            var damageable = target.GetComponent<Damageable>();
            int deaths = 0;
            damageable.Died += (_, _) => deaths++;

            for (int shot = 0; shot < 2; shot++)
            {
                yield return FireOnce(target.transform.position);
                // flight ~0.7 s + cooldown headroom
                for (int i = 0; i < 60; i++) yield return new WaitForFixedUpdate();
            }

            Assert.That(deaths, Is.EqualTo(1), "Died should fire exactly once");
            Assert.That(target.activeSelf, Is.False, "dead tank should deactivate");
        }

        [UnityTest]
        public IEnumerator Cannon_RicochetsOnceThenDespawns()
        {
            // Wall angled 45° reflects a +z shot to +x; second wall catches it.
            var wall1 = Env(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall1.transform.position = new Vector3(0f, 0.5f, 8f);
            wall1.transform.rotation = Quaternion.Euler(0f, -45f, 0f); // reflect +z → +x
            wall1.transform.localScale = new Vector3(6f, 1f, 0.5f);
            var wall2 = Env(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall2.transform.position = new Vector3(8f, 0.5f, 8f);
            wall2.transform.localScale = new Vector3(0.5f, 1f, 6f);

            _weapon.Equip(Spec("cannon"));
            yield return FireOnce(new Vector3(0f, 0f, 50f));

            // After first bounce (~0.55 s) the shell must still exist...
            for (int i = 0; i < 45; i++) yield return new WaitForFixedUpdate();
            Assert.That(Object.FindFirstObjectByType<Projectile>(), Is.Not.Null,
                "shell died on first wall — ricochet failed");
            // ...and be gone after hitting the second wall (~0.6 s later).
            for (int i = 0; i < 60; i++) yield return new WaitForFixedUpdate();
            Assert.That(Object.FindFirstObjectByType<Projectile>(), Is.Null,
                "shell survived second wall");
        }

        [UnityTest]
        public IEnumerator Spread_SpawnsFivePellets()
        {
            _weapon.Equip(Spec("spread"));
            yield return FireOnce(new Vector3(0f, 0f, 50f));
            int count = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length;
            Assert.That(count, Is.EqualTo(5));
        }

        [UnityTest]
        public IEnumerator Mortar_AoeDamagesNearTarget_NotFirer()
        {
            _weapon.Equip(Spec("mortar"));
            var target = SpawnTank(new Vector3(0f, 0.1f, 10.5f), out _);
            var targetHp = target.GetComponent<Damageable>();
            var myHp = _shooter.GetComponent<Damageable>();

            yield return FireOnce(new Vector3(0f, 0f, 10f));
            for (int i = 0; i < 180; i++) // up to 3 s flight
            {
                yield return new WaitForFixedUpdate();
                if (Object.FindFirstObjectByType<Projectile>() == null) break;
            }

            Assert.That(targetHp.Health, Is.LessThan(targetHp.maxHealth),
                "mortar AoE missed a target 0.5 u from the aim point");
            Assert.That(myHp.Health, Is.EqualTo(myHp.maxHealth), "firer damaged itself");
        }

        [UnityTest]
        public IEnumerator MachineGun_FiresNineToElevenShotsPerSecond()
        {
            _weapon.Equip(Spec("mg"));
            _input.Next = new TankInputSample { Fire = true, AimWorldPos = new Vector3(0f, 0f, 50f) };
            for (int i = 0; i < 60; i++) yield return new WaitForFixedUpdate();
            _input.Next = default;
            int count = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length;
            Assert.That(count, Is.InRange(9, 11), "MG rate off (cooldown 0.1 s @ 60 Hz)");
        }
    }
}
