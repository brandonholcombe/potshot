using System.Collections;
using System.Linq;
using NUnit.Framework;
using Potshot;
using UnityEngine;
using UnityEngine.TestTools;

namespace Potshot.Tests
{
    public class FfaTests
    {
        readonly System.Collections.Generic.List<GameObject> _env = new();
        FfaGameMode _mode;
        TankController _a, _b;

        T Env<T>(T obj) where T : Object
        {
            var go = obj as GameObject ?? (obj as Component)?.gameObject;
            if (go != null) _env.Add(go);
            return obj;
        }

        TankController SpawnTank(Vector3 pos, string name)
        {
            var tank = Env(Object.Instantiate(
                Resources.Load<GameObject>("Prefabs/Tank"), pos, Quaternion.identity));
            tank.name = name;
            var c = tank.GetComponent<TankController>();
            c.InputSource = new ScriptedTankInput();
            return c;
        }

        [SetUp]
        public void SetUp()
        {
            var ground = Env(GameObject.CreatePrimitive(PrimitiveType.Plane));
            ground.transform.localScale = new Vector3(10f, 1f, 10f);

            _mode = Env(new GameObject("Ffa")).AddComponent<FfaGameMode>();
            _mode.respawnDelay = 0.3f;
            _mode.intermissionSeconds = 0.4f;
            _mode.killTarget = 50; // out of reach unless a test lowers it
            _mode.spawnPoints = new[]
            {
                new Vector3(-8f, 0.1f, 0f), new Vector3(8f, 0.1f, 0f),
                new Vector3(0f, 0.1f, 8f),
            };

            _a = SpawnTank(new Vector3(-3f, 0.1f, 0f), "A");
            _b = SpawnTank(new Vector3(3f, 0.1f, 0f), "B");
            _mode.Register(_a);
            _mode.Register(_b);
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

        [UnityTest]
        public IEnumerator Kill_CreditsKiller()
        {
            _b.GetComponent<Damageable>().TakeDamage(100f, _a.gameObject);
            yield return null;
            Assert.That(_mode.GetScore(_a), Is.EqualTo(1));
            Assert.That(_mode.GetScore(_b), Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator SelfKill_CostsAPoint()
        {
            _a.GetComponent<Damageable>().TakeDamage(100f, _a.gameObject);
            yield return null;
            Assert.That(_mode.GetScore(_a), Is.EqualTo(-1));
        }

        [UnityTest]
        public IEnumerator DoubleRegister_DoesNotDoubleScore()
        {
            _mode.Register(_a);
            _mode.Register(_b);
            _b.GetComponent<Damageable>().TakeDamage(100f, _a.gameObject);
            yield return null;
            Assert.That(_mode.GetScore(_a), Is.EqualTo(1), "idempotent Register");
        }

        [UnityTest]
        public IEnumerator Dead_RespawnsHealthyArmedAtSpawnPoint()
        {
            _b.weapon.Equip(Resources.Load<WeaponSpec>("Specs/Weapons/mg"));
            _b.GetComponent<Damageable>().TakeDamage(100f, _a.gameObject);
            yield return null;
            Assert.That(_b.gameObject.activeSelf, Is.False, "should be dead first");

            for (int i = 0; i < 60 && !_b.gameObject.activeSelf; i++)
                yield return new WaitForFixedUpdate();

            Assert.That(_b.gameObject.activeSelf, Is.True, "should respawn");
            Assert.That(_b.GetComponent<Damageable>().Health, Is.EqualTo(100f));
            Assert.That(_b.weapon.current.id, Is.EqualTo("cannon"), "default weapon on respawn");
            Assert.That(_mode.spawnPoints.Any(p =>
                Vector3.Distance(p, _b.transform.position) < 1.5f),
                "should respawn at a registered spawn point");
        }

        [UnityTest]
        public IEnumerator RoundEnd_FiresWinner_ThenResetsScores()
        {
            _mode.killTarget = 1;
            TankController winner = null;
            _mode.RoundOver += w => winner = w;

            _b.GetComponent<Damageable>().TakeDamage(100f, _a.gameObject);
            for (int i = 0; i < 30 && winner == null; i++)
                yield return new WaitForFixedUpdate();

            Assert.That(winner, Is.EqualTo(_a), "A reached the kill target");
            Assert.That(_mode.RoundActive, Is.False, "intermission");
            Assert.That(_a.InputFrozen, Is.True, "inputs freeze in intermission");

            for (int i = 0; i < 60 && !_mode.RoundActive; i++)
                yield return new WaitForFixedUpdate();

            Assert.That(_mode.RoundActive, Is.True, "next round starts");
            Assert.That(_mode.GetScore(_a), Is.EqualTo(0), "scores reset");
            Assert.That(_a.InputFrozen, Is.False);
            Assert.That(_b.gameObject.activeSelf, Is.True, "everyone respawned");
        }
    }

    public class BotTests
    {
        readonly System.Collections.Generic.List<GameObject> _env = new();

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

        TankController SpawnBot(Vector3 pos)
        {
            var tank = Env(Object.Instantiate(
                Resources.Load<GameObject>("Prefabs/Tank"), pos, Quaternion.identity));
            Object.Destroy(tank.GetComponent<PlayerTankInput>());
            var c = tank.GetComponent<TankController>();
            var brain = tank.AddComponent<BotBrain>();
            brain.spec = Resources.Load<BotSpec>("Specs/BotSpec");
            c.InputSource = brain;
            return c;
        }

        [UnityTest]
        public IEnumerator Bot_ApproachesAndKillsATarget()
        {
            var bot = SpawnBot(new Vector3(0f, 0.1f, -20f));
            var target = Env(Object.Instantiate(
                Resources.Load<GameObject>("Prefabs/Tank"),
                new Vector3(0f, 0.1f, 5f), Quaternion.identity));
            target.GetComponent<TankController>().InputSource = new ScriptedTankInput();
            var targetHp = target.GetComponent<Damageable>();

            float startDist = Vector3.Distance(bot.transform.position, target.transform.position);
            bool approached = false, killed = false;
            for (int i = 0; i < 60 * 45; i++) // up to 45 s sim
            {
                yield return new WaitForFixedUpdate();
                if (!approached && Vector3.Distance(
                        bot.transform.position, target.transform.position) < startDist - 5f)
                    approached = true;
                if (targetHp.IsDead) { killed = true; break; }
            }

            Assert.That(approached, Is.True, "bot never closed distance");
            Assert.That(killed, Is.True, "bot never killed a stationary target");
        }

        [UnityTest]
        public IEnumerator Bot_DoesNotGrindWallsForever()
        {
            // Wall between bot and target: avoidance must keep it moving.
            var wall = Env(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.transform.position = new Vector3(0f, 1f, -10f);
            wall.transform.localScale = new Vector3(14f, 2f, 1f);

            var bot = SpawnBot(new Vector3(0f, 0.1f, -16f));
            var target = Env(Object.Instantiate(
                Resources.Load<GameObject>("Prefabs/Tank"),
                new Vector3(0f, 0.1f, 0f), Quaternion.identity));
            target.GetComponent<TankController>().InputSource = new ScriptedTankInput();

            var last = bot.transform.position;
            float travelled = 0f;
            for (int i = 0; i < 60 * 8; i++)
            {
                yield return new WaitForFixedUpdate();
                travelled += Vector3.Distance(bot.transform.position, last);
                last = bot.transform.position;
            }

            Assert.That(travelled, Is.GreaterThan(10f),
                "bot appears stuck grinding the wall");
        }
    }
}
