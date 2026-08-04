using System.Collections;
using NUnit.Framework;
using Potshot;
using UnityEngine;
using UnityEngine.TestTools;

namespace Potshot.Tests
{
    public class PickupTests
    {
        readonly System.Collections.Generic.List<GameObject> _env = new();
        GameObject _tank;
        ScriptedTankInput _input;
        WeaponController _weapon;

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
            _tank = Env(Object.Instantiate(
                Resources.Load<GameObject>("Prefabs/Tank"),
                new Vector3(0f, 0.1f, 0f), Quaternion.identity));
            _input = new ScriptedTankInput();
            _tank.GetComponent<TankController>().InputSource = _input;
            _weapon = _tank.GetComponent<WeaponController>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var p in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                Object.Destroy(p.gameObject);
            foreach (var p in Object.FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None))
                Object.Destroy(p.gameObject);
            foreach (var go in _env)
                if (go != null) Object.Destroy(go);
            _env.Clear();
        }

        static WeaponSpec Spec(string id) => Resources.Load<WeaponSpec>($"Specs/Weapons/{id}");
        static GameObject PickupPrefab() => Resources.Load<GameObject>("Prefabs/WeaponPickup");

        [UnityTest]
        public IEnumerator Pickup_EquipsWeaponWithFullAmmo()
        {
            WeaponPickup.Spawn(PickupPrefab(), _tank.transform.position, Spec("spread"));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return null; // Destroy() completes end of frame

            Assert.That(_weapon.current.id, Is.EqualTo("spread"));
            Assert.That(_weapon.AmmoLeft, Is.EqualTo(8));
            Assert.That(Object.FindFirstObjectByType<WeaponPickup>(), Is.Null,
                "pickup should despawn on consumption");
        }

        [UnityTest]
        public IEnumerator SpreadVolley_CostsExactlyOneAmmo()
        {
            _weapon.Equip(Spec("spread"));
            _input.Next = new TankInputSample { Fire = true, AimWorldPos = new Vector3(0f, 0f, 50f) };
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            _input.Next = default;

            Assert.That(_weapon.AmmoLeft, Is.EqualTo(7), "one volley = one ammo");
        }

        [UnityTest]
        public IEnumerator AmmoExhaustion_RevertsToCannon()
        {
            _weapon.Equip(Spec("mortar")); // 5 rounds, 1.5 s cooldown
            _input.Next = new TankInputSample { Fire = true, AimWorldPos = new Vector3(0f, 0f, 40f) };
            // 5 shots need ~6 s of cooldown time + margin
            for (int i = 0; i < 60 * 9; i++)
            {
                yield return new WaitForFixedUpdate();
                if (_weapon.current != null && _weapon.current.id == "cannon") break;
            }
            _input.Next = default;

            Assert.That(_weapon.current.id, Is.EqualTo("cannon"),
                "depleted weapon should revert to default");
            Assert.That(_weapon.current.ammo, Is.EqualTo(0), "cannon is infinite");
        }

        [UnityTest]
        public IEnumerator EmptyPad_RespawnsAfterInterval()
        {
            var padGo = Env(new GameObject("TestPad"));
            padGo.transform.position = new Vector3(15f, 0f, 15f); // away from tank
            var pad = padGo.AddComponent<PickupPad>();
            pad.pickupPrefab = PickupPrefab();
            pad.weaponPool = new[] { Spec("mg") };
            pad.respawnInterval = 0.5f;

            yield return new WaitForFixedUpdate(); // Start() spawns immediately
            var first = Object.FindFirstObjectByType<WeaponPickup>();
            Assert.That(first, Is.Not.Null, "pad should spawn on Start");

            // Consume by teleporting the tank onto it.
            _tank.transform.position = padGo.transform.position + Vector3.up * 0.1f;
            for (int i = 0; i < 10 && _weapon.current.id != "mg"; i++)
                yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(_weapon.current.id, Is.EqualTo("mg"), "teleported tank should consume");
            Assert.That(Object.FindFirstObjectByType<WeaponPickup>(), Is.Null);

            // Move away; pad refills after its interval.
            _tank.transform.position = new Vector3(0f, 0.1f, 0f);
            for (int i = 0; i < 60 && Object.FindFirstObjectByType<WeaponPickup>() == null; i++)
                yield return new WaitForFixedUpdate();
            Assert.That(Object.FindFirstObjectByType<WeaponPickup>(), Is.Not.Null,
                "pad should respawn a pickup after respawnInterval");
        }
    }
}
