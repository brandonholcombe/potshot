using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Potshot.Tests.Visual
{
    /// <summary>
    /// Visual tests live in an editor-only assembly, which Unity's test
    /// framework treats as EDITMODE tests — so they enter play mode
    /// explicitly via EnterPlayMode. Run them with:
    ///   scripts/unity-gfx.sh -runTests -testPlatform EditMode -assemblyNames Potshot.Tests.Visual ...
    /// The batchmode merge gate (run-tests.sh) filters them out by assembly.
    /// </summary>
    public class VisualSmokeTests
    {
        [UnityTest]
        public IEnumerator QaProbe_RendersInPlayMode()
        {
            Assume.That(!Application.isBatchMode,
                "Visual tests need graphics — run via unity-gfx.sh");

            yield return new EnterPlayMode();

            // Editor-only load by path: QaProbe must NOT enter build
            // settings (it would leak into the server build).
            EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/QaProbe.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            yield return null;

            QaCapture.Capture("qaprobe-playmode");

            Assert.That(File.Exists(QaCapture.LastPath), "no screenshot written");
            Assert.Greater(new FileInfo(QaCapture.LastPath).Length, 10_000,
                "screenshot suspiciously small — likely blank");

            yield return new ExitPlayMode();
        }
    }
}
