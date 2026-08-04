using System.IO;
using UnityEditor;
using UnityEngine;

namespace Potshot.EditorTools
{
    /// <summary>Get-or-create saved material assets (scenes/prefabs must
    /// never reference in-memory materials). GUID-stable across re-runs.</summary>
    public static class MaterialFactory
    {
        public static Material GetOrCreate(string dir, string name, Color color)
        {
            Directory.CreateDirectory(dir);
            string path = $"{dir}/{name}.mat";
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
