using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Potshot.Tests.Visual
{
    /// <summary>
    /// Synchronous camera capture for Visual tests: renders a camera to a
    /// RenderTexture and writes a PNG under game/Logs/qa/. No
    /// WaitForEndOfFrame dependency, so it works from EditMode-driven tests
    /// that have entered play mode (see VisualSmokeTests). Editor-only
    /// assembly — never ships. Assumes Built-in RP (see QaScreenshots).
    /// </summary>
    public static class QaCapture
    {
        const int Width = 1280, Height = 720;

        public static string LastPath { get; private set; }

        public static void Capture(string name, Camera cam = null)
        {
            if (GraphicsSettings.currentRenderPipeline != null)
                throw new System.InvalidOperationException(
                    "QaCapture assumes Built-in RP — rewrite for SRP");

            cam = cam != null ? cam : Camera.main;
            if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
            if (cam == null)
                throw new System.InvalidOperationException("no camera to capture");

            var rt = new RenderTexture(Width, Height, 24);
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;

            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();

            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;

            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/qa"));
            Directory.CreateDirectory(dir);
            LastPath = Path.Combine(dir, name + ".png");
            File.WriteAllBytes(LastPath, tex.EncodeToPNG());

            Object.Destroy(tex);
            rt.Release();
            Object.Destroy(rt);
            Debug.Log($"[QaCapture] wrote {LastPath}");
        }
    }
}
