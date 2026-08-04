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
    public class WeaponVisualTests
    {
        [UnityTest]
        public IEnumerator DevArena_TwoTanks_ExchangeFire()
        {
            Assume.That(!Application.isBatchMode,
                "Visual tests need graphics — run via unity-gfx.sh");

            yield return new EnterPlayMode();

            EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/DevArena.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;

            var tankA = Object.FindFirstObjectByType<TankController>();
            tankA.transform.position = new Vector3(-7f, 0.1f, 0f);
            var tankB = Object.Instantiate(
                Resources.Load<GameObject>("Prefabs/Tank"),
                new Vector3(7f, 0.1f, 0f), Quaternion.identity)
                .GetComponent<TankController>();

            tankA.InputSource = new ScriptedTankInput
            {
                Next = new TankInputSample { Fire = true, AimWorldPos = tankB.transform.position }
            };
            tankB.InputSource = new ScriptedTankInput
            {
                Next = new TankInputSample { Fire = true, AimWorldPos = tankA.transform.position }
            };

            // Editor-driven frames can simulate big time slices (physics
            // catch-up), so a fixed wait can overshoot the shells' whole
            // flight — poll until shells are actually in the air.
            float deadline = Time.time + 3f;
            int inFlight = 0;
            while (Time.time < deadline)
            {
                inFlight = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length;
                if (inFlight >= 1) break;
                yield return null;
            }

            QaCapture.Capture("devarena-firefight");

            Assert.That(File.Exists(QaCapture.LastPath));
            Assert.That(inFlight, Is.GreaterThanOrEqualTo(1),
                "no shells observed in flight within 3 s");

            yield return new ExitPlayMode();
        }
    }
}
