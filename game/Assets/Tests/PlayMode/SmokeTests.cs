using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Potshot.Tests
{
    public class SmokeTests
    {
        [UnityTest]
        public IEnumerator PlayMode_TicksAndPhysicsStepRuns()
        {
            var go = new GameObject("smoke");
            var body = go.AddComponent<Rigidbody>();
            body.useGravity = true;

            float before = go.transform.position.y;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.That(go.transform.position.y, Is.LessThan(before),
                "physics did not step in play mode");
            Object.Destroy(go);
        }
    }
}
