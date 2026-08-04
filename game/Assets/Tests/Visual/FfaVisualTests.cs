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
    public class FfaVisualTests
    {
        [UnityTest]
        public IEnumerator DevArena_BotsFight_Captured()
        {
            Assume.That(!Application.isBatchMode,
                "Visual tests need graphics — run via unity-gfx.sh");

            yield return new EnterPlayMode();

            EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/DevArena.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;

            // Bots engage on their own; wait for live fire (event-driven —
            // frame time slices are unpredictable here, m1d lesson).
            float deadline = Time.time + 10f;
            bool sawFire = false;
            while (Time.time < deadline)
            {
                if (Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length > 0)
                {
                    sawFire = true;
                    break;
                }
                yield return null;
            }

            QaCapture.Capture("devarena-botcombat");

            Assert.That(sawFire, Is.True, "bots never opened fire in 10 s");
            Assert.That(File.Exists(QaCapture.LastPath));
            Assert.That(Object.FindFirstObjectByType<FfaGameMode>(), Is.Not.Null,
                "game mode missing from DevArena");

            yield return new ExitPlayMode();
        }
    }
}
