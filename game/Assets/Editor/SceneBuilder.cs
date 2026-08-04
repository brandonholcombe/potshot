using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Potshot.EditorTools
{
    /// <summary>
    /// All scenes are generated here — never hand-edited (CLAUDE.md Prime
    /// Directive). Builders must be idempotent: re-running overwrites the
    /// scene deterministically.
    /// </summary>
    public static class SceneBuilder
    {
        const string ScenesDir = "Assets/Scenes";
        const string QaMaterialsDir = "Assets/Materials/Qa";

        public static void BuildQaScene()
        {
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 4f); // 40x40 units
            ground.GetComponent<Renderer>().sharedMaterial =
                Mat("Ground", new Color(0.35f, 0.4f, 0.3f));

            Block("BlockRed", new Vector3(-5f, 0.5f, 0f), Color.red);
            Block("BlockGreen", new Vector3(0f, 0.5f, 5f), Color.green);
            Block("BlockBlue", new Vector3(6f, 0.5f, -3f), Color.blue);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(60f, 30f, 0f);

            var cam = new GameObject("TopCamera").AddComponent<Camera>();
            cam.transform.position = new Vector3(0f, 20f, 0f);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.12f);

            Directory.CreateDirectory(ScenesDir);
            EditorSceneManager.SaveScene(scene, $"{ScenesDir}/QaProbe.unity");
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneBuilder] QaProbe.unity saved");
        }

        static void Block(string name, Vector3 pos, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos;
            go.GetComponent<Renderer>().sharedMaterial = Mat(name, color);
        }

        /// <summary>Get-or-create a saved material asset (scenes must not
        /// reference in-memory materials).</summary>
        static Material Mat(string name, Color color)
        {
            Directory.CreateDirectory(QaMaterialsDir);
            string path = $"{QaMaterialsDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
