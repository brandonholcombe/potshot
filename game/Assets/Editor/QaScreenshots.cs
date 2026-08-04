using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Potshot.EditorTools
{
    /// <summary>
    /// Edit-mode render probe: renders every enabled camera in a scene to
    /// PNGs under game/Logs/qa/ so agents can Read and judge composition.
    /// Needs graphics — run via unity-gfx.sh (NOT unity.sh):
    ///   scripts/unity-gfx.sh -executeMethod Potshot.EditorTools.QaScreenshots.RenderProbe [-potshotScene Assets/Scenes/Foo.unity]
    /// Exits the editor itself (EditorApplication.Exit) — do not pass -quit.
    /// </summary>
    public static class QaScreenshots
    {
        const int Width = 1280, Height = 720;

        public static void RenderProbe()
        {
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                // Camera.Render() below assumes Built-in RP (M1b review).
                Debug.LogError("[QaScreenshots] project moved to SRP — rewrite RenderProbe");
                EditorApplication.Exit(1);
                return;
            }

            string scenePath = Arg("-potshotScene") ?? "Assets/Scenes/QaProbe.unity";
            EditorSceneManager.OpenScene(scenePath);

            // -potshotOverview: add a temp orthographic top camera framing
            // all renderers — full-map shots for drawing comparison.
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-potshotOverview") >= 0)
            {
                var bounds = new Bounds();
                bool first = true;
                foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (first) { bounds = r.bounds; first = false; }
                    else bounds.Encapsulate(r.bounds);
                }
                var top = new GameObject("Overview").AddComponent<Camera>();
                top.orthographic = true;
                top.orthographicSize = Mathf.Max(bounds.extents.z,
                    bounds.extents.x / ((float)Width / Height)) * 1.05f;
                top.transform.position = bounds.center + Vector3.up * 60f;
                top.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                top.clearFlags = CameraClearFlags.SolidColor;
                top.backgroundColor = new Color(0.08f, 0.08f, 0.1f);
                foreach (var cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                    if (cam != top) cam.enabled = false;
            }

            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/qa"));
            Directory.CreateDirectory(outDir);
            string sceneName = Path.GetFileNameWithoutExtension(scenePath);

            int captured = 0;
            foreach (var cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (!cam.enabled || !cam.gameObject.activeInHierarchy) continue;

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

                string file = Path.Combine(outDir, $"{sceneName}-{cam.name}.png");
                File.WriteAllBytes(file, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);

                Debug.Log($"[QaScreenshots] wrote {file}");
                captured++;
            }

            EditorApplication.Exit(captured > 0 ? 0 : 1);
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
