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

            AssetDatabase.SaveAssets();
            Debug.Log($"[ProjectConfigurator] applied: company={PlayerSettings.companyName} " +
                      $"product={PlayerSettings.productName} version={PlayerSettings.bundleVersion} " +
                      $"fixedTimestep={Time.fixedDeltaTime}");
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
