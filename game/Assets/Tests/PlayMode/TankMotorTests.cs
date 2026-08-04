using System.Collections;
using NUnit.Framework;
using Potshot;
using UnityEngine;
using UnityEngine.TestTools;

namespace Potshot.Tests
{
    /// <summary>
    /// Feel + collision tests against the real prefab. Environment is built
    /// from primitives in code (scenes are not loadable in this assembly —
    /// M1c review). Counts WaitForFixedUpdate steps, not wall time.
    /// </summary>
    public class TankMotorTests
    {
        const float Dt = 1f / 60f;

        GameObject _ground;
        GameObject _tank;
        ScriptedTankInput _input;
        TankController _controller;

        [SetUp]
        public void SetUp()
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _ground.transform.localScale = new Vector3(10f, 1f, 10f);

            var prefab = Resources.Load<GameObject>("Prefabs/Tank");
            Assert.That(prefab, Is.Not.Null, "Tank.prefab missing — run PrefabFactory.CreateAll");
            _tank = Object.Instantiate(prefab, new Vector3(0f, 0.1f, 0f), Quaternion.identity);
            _controller = _tank.GetComponent<TankController>();
            _input = new ScriptedTankInput();
            _controller.InputSource = _input; // overrides PlayerTankInput
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_tank);
            Object.Destroy(_ground);
        }

        float PlanarSpeed()
        {
            var v = _tank.GetComponent<Rigidbody>().linearVelocity;
            return new Vector3(v.x, 0f, v.z).magnitude;
        }

        [UnityTest]
        public IEnumerator TopSpeed_IsWithin5PercentOfTarget()
        {
            _input.Next = new TankInputSample { Move = Vector2.up };
            for (int i = 0; i < 72; i++) yield return new WaitForFixedUpdate(); // 1.2 s
            Assert.That(PlanarSpeed(), Is.EqualTo(6f).Within(6f * 0.05f));
        }

        [UnityTest]
        public IEnumerator ReachesNinetyFivePercent_InPoint3ToPoint5Seconds()
        {
            _input.Next = new TankInputSample { Move = Vector2.up };
            int steps = 0;
            while (PlanarSpeed() < 6f * 0.95f && steps < 60)
            {
                yield return new WaitForFixedUpdate();
                steps++;
            }
            float t = steps * Dt; // linear model predicts 5.7/15 = 0.38 s
            Assert.That(t, Is.InRange(0.3f, 0.5f), $"reached 95% in {t:F3}s");
        }

        [UnityTest]
        public IEnumerator Stops_WithinPoint6SecondsOfRelease()
        {
            _input.Next = new TankInputSample { Move = Vector2.up };
            for (int i = 0; i < 60; i++) yield return new WaitForFixedUpdate();
            _input.Next = default;
            int steps = 0;
            while (PlanarSpeed() > 0.2f && steps < 60)
            {
                yield return new WaitForFixedUpdate();
                steps++;
            }
            float t = steps * Dt; // linear: (6-0.2)/15 ≈ 0.39 s
            Assert.That(t, Is.LessThanOrEqualTo(0.6f), $"stopped in {t:F3}s");
        }

        [UnityTest]
        public IEnumerator Turret_Reaches90DegreeAim_InUnderPoint35Seconds()
        {
            // Aim due east from origin; turret starts facing north (+z).
            _input.Next = new TankInputSample { AimWorldPos = new Vector3(100f, 0f, 0f) };
            float start = Time.time;
            var turret = _controller.turret;
            while (Time.time - start < 0.6f)
            {
                var flat = turret.forward; flat.y = 0f;
                if (Vector3.Angle(flat, Vector3.right) < 3f) break;
                yield return null; // turret turns in Update
            }
            Assert.That(Time.time - start, Is.LessThanOrEqualTo(0.35f),
                "turret too slow to 90°");
        }

        [UnityTest]
        public IEnumerator Wall_StopsTheTank()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 1f, 5f);
            wall.transform.localScale = new Vector3(10f, 2f, 1f);

            _input.Next = new TankInputSample { Move = Vector2.up };
            for (int i = 0; i < 180; i++) yield return new WaitForFixedUpdate(); // 3 s into the wall

            Assert.That(_tank.transform.position.z, Is.LessThan(4.6f),
                "tank penetrated or passed the wall");
            Assert.That(Mathf.Abs(_tank.transform.rotation.eulerAngles.x), Is.LessThan(5f).Or.GreaterThan(355f),
                "tank pitched while pushing the wall — rotation constraints failed");
            Object.Destroy(wall);
        }
    }
}
