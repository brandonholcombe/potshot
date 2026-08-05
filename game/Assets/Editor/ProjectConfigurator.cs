using System.Linq;
using Potshot;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Potshot.EditorTools
{
    /// <summary>
    /// Idempotent project configuration. All editor-side project settings go
    /// through here so they are reproducible and reviewable — never set them
    /// in the GUI. Invoke:
    ///   scripts/unity.sh -quit -executeMethod Potshot.EditorTools.ProjectConfigurator.Configure
    /// </summary>
    public static class ProjectConfigurator
    {
        public static void Configure()
        {
            PlayerSettings.companyName = "kodloki";
            PlayerSettings.productName = "Potshot";
            PlayerSettings.bundleVersion = GameVersion.Version;
            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.Standalone, "io.kodloki.potshot");

            SetFixedTimestep(1f / 60f);
            SetLayerName(PotshotLayers.Projectile, "Projectile");
            // Pre-join status panel fetches plain http://host:8080/status;
            // Unity 6 default (NotAllowed) kills every insecure request.
            // TLS via ingress is the M4 upgrade path.
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            SyncEditorBuildScenes();

            AssetDatabase.SaveAssets();
            Debug.Log($"[ProjectConfigurator] applied: company={PlayerSettings.companyName} " +
                      $"product={PlayerSettings.productName} version={PlayerSettings.bundleVersion} " +
                      $"fixedTimestep={Time.fixedDeltaTime}");
        }

        /// <summary>FishNet's scene processor NREs with an empty editor
        /// build-scenes list (lobby review) — keep it synced to the client
        /// scene set so lifecycle tests can load scenes by name.</summary>
        public static void SyncEditorBuildScenes()
        {
            var paths = new System.Collections.Generic.List<string>
            {
                "Assets/Scenes/MainMenu.unity",
                "Assets/Scenes/Lobby.unity",
                "Assets/Scenes/DevArena.unity",
            };
            if (System.IO.Directory.Exists("Assets/Scenes/Maps"))
                paths.AddRange(System.IO.Directory
                    .GetFiles("Assets/Scenes/Maps", "*.unity")
                    .OrderBy(p => p));
            EditorBuildSettings.scenes = paths
                .Where(System.IO.File.Exists)
                .Select(p => new EditorBuildSettingsScene(p, true))
                .ToArray();
            Debug.Log($"[ProjectConfigurator] {EditorBuildSettings.scenes.Length} editor build scenes synced");
        }

        static void SetLayerName(int index, string name)
        {
            var tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManager.Length == 0)
            {
                Debug.LogError("[ProjectConfigurator] TagManager.asset not found");
                return;
            }
            var so = new SerializedObject(tagManager[0]);
            var layers = so.FindProperty("layers");
            layers.GetArrayElementAtIndex(index).stringValue = name;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetFixedTimestep(float seconds)
        {
            var timeManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TimeManager.asset");
            if (timeManager.Length == 0)
            {
                Debug.LogError("[ProjectConfigurator] TimeManager.asset not found");
                return;
            }
            var so = new SerializedObject(timeManager[0]);
            var prop = so.FindProperty("Fixed Timestep");
            var count = prop.FindPropertyRelative("m_Count");
            if (count != null)
            {
                // Unity 6.1+ rational time: seconds = m_Count * denom / numer.
                var rate = prop.FindPropertyRelative("m_Rate");
                long numer = rate.FindPropertyRelative("m_Numerator").longValue;
                long denom = rate.FindPropertyRelative("m_Denominator").longValue;
                count.longValue = (long)System.Math.Round(seconds * numer / denom);
            }
            else
            {
                prop.floatValue = seconds;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
