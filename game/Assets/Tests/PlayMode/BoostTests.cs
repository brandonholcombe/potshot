using System.Collections;
using NUnit.Framework;
using Potshot;
using UnityEngine;
using UnityEngine.TestTools;

namespace Potshot.Tests
{
    public class BoostTests
    {
        GameObject _ground, _tank;
        ScriptedTankInput _input;

        [SetUp]
        public void SetUp()
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _ground.transform.localScale = new Vector3(10f, 1f, 10f);
            _tank = Object.Instantiate(Resources.Load<GameObject>("Prefabs/Tank"),
                new Vector3(0f, 0.1f, 0f), Quaternion.identity);
            _input = new ScriptedTankInput();
            _tank.GetComponent<TankController>().InputSource = _input;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_tank);
            Object.Destroy(_ground);
        }

        float Speed()
        {
            var v = _tank.GetComponent<Rigidbody>().linearVelocity;
            return new Vector3(v.x, 0f, v.z).magnitude;
        }

        [UnityTest]
        public IEnumerator Boost_RaisesSpeed_ThenDecaysToTopSpeed()
        {
            _input.Next = new TankInputSample { Move = Vector2.up, Boost = true };
            for (int i = 0; i < 48; i++) yield return new WaitForFixedUpdate(); // 0.8 s
            Assert.That(Speed(), Is.GreaterThan(9f), "boost should exceed normal cap");

            _input.Next = new TankInputSample { Move = Vector2.up };
            for (int i = 0; i < 72; i++) yield return new WaitForFixedUpdate(); // 1.2 s
            Assert.That(Speed(), Is.LessThanOrEqualTo(7.35f), "should decay to topSpeed");
        }

        [UnityTest]
        public IEnumerator Boost_CooldownBlocks_ThenHeldRefires()
        {
            // Hold boost the whole time: fires at t0, must NOT refire during
            // cooldown, refires automatically when cooldown expires.
            _input.Next = new TankInputSample { Move = Vector2.up, Boost = true };
            for (int i = 0; i < 150; i++) yield return new WaitForFixedUpdate(); // t=2.5 s
            Assert.That(Speed(), Is.LessThan(8f), "no boost inside the 4 s cooldown");

            for (int i = 0; i < 138; i++) yield return new WaitForFixedUpdate(); // t=4.8 s
            Assert.That(Speed(), Is.GreaterThan(9f), "held boost should refire after cooldown");
        }
    }
}
