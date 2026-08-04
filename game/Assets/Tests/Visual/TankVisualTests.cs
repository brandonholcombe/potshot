using System.Collections;
using System.IO;
using NUnit.Framework;
using Potshot;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Potshot.Tests.Visual
{
    public class TankVisualTests
    {
        [UnityTest]
        public IEnumerator DevArena_TankDrives_AndIsVisible()
        {
            Assume.That(!Application.isBatchMode,
                "Visual tests need graphics — run via unity-gfx.sh");

            yield return new EnterPlayMode();

            EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/DevArena.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;

            var controller = Object.FindFirstObjectByType<TankController>();
            Assert.That(controller, Is.Not.Null, "no tank in DevArena");
            var input = new ScriptedTankInput
            {
                Next = new TankInputSample
                {
                    Move = new Vector2(0.6f, 0.8f),
                    AimWorldPos = new Vector3(20f, 0f, 0f),
                }
            };
            controller.InputSource = input;

            // ~1 s of driving; WaitForFixedUpdate is unsupported in
            // EnterPlayMode-based tests (M1c review) — use frame yields.
            float until = Time.time + 1f;
            while (Time.time < until) yield return null;

            QaCapture.Capture("devarena-driving");

            Assert.That(File.Exists(QaCapture.LastPath));
            Assert.Greater(new FileInfo(QaCapture.LastPath).Length, 10_000);
            Assert.That(controller.transform.position.magnitude, Is.GreaterThan(1f),
                "tank did not move");

            yield return new ExitPlayMode();
        }
    }
}
