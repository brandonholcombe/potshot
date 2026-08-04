using System.IO;
using FishNet.Managing;
using FishNet.Transporting.Tugboat;
using Potshot.Net;
using UnityEditor;
using UnityEngine;

namespace Potshot.EditorTools
{
    /// <summary>
    /// Generates the NetworkHub prefab: NetworkManager + Tugboat (UDP 7777)
    /// + VersionAuthenticator. ServerManager auto-discovers a co-located
    /// Authenticator component (M2a review) — no serialized wiring needed.
    /// Run: scripts/unity.sh -quit -executeMethod Potshot.EditorTools.NetworkFactory.CreateNetworkHubPrefab
    /// </summary>
    public static class NetworkFactory
    {
        const string PrefabDir = "Assets/Resources/Prefabs";

        public static void CreateNetworkHubPrefab()
        {
            var root = new GameObject("NetworkHub");
            try
            {
                var tugboat = root.AddComponent<Tugboat>();
                tugboat.SetPort(7777);
                var nm = root.AddComponent<NetworkManager>();
                root.AddComponent<VersionAuthenticator>();

                // FishNet requires a spawnable-prefabs collection; wire the
                // auto-generated DefaultPrefabObjects (SerializedObject —
                // the field is private).
                var prefabObjects = AssetDatabase.LoadMainAssetAtPath(
                    "Assets/DefaultPrefabObjects.asset");
                if (prefabObjects == null)
                {
                    Debug.LogError("[NetworkFactory] DefaultPrefabObjects.asset missing");
                    EditorApplication.Exit(1);
                    return;
                }
                var so = new SerializedObject(nm);
                so.FindProperty("_spawnablePrefabs").objectReferenceValue = prefabObjects;
                so.ApplyModifiedPropertiesWithoutUndo();

                Directory.CreateDirectory(PrefabDir);
                PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/NetworkHub.prefab");
                Debug.Log("[NetworkFactory] NetworkHub.prefab saved");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
