using System.IO;
using Potshot;
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

        static Material Mat(string name, Color color) =>
            MaterialFactory.GetOrCreate(QaMaterialsDir, name, color);

        /// <summary>Dev arena: 40x40 ground, perimeter walls, tank from the
        /// generated prefab, follow camera, sun. Overwrites DevArena.unity.</summary>
        public static void BuildDevArena()
        {
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            ground.GetComponent<Renderer>().sharedMaterial =
                Mat("ArenaGround", new Color(0.42f, 0.38f, 0.3f));

            var wallMat = Mat("ArenaWall", new Color(0.3f, 0.3f, 0.32f));
            Wall("WallN", new Vector3(0f, 1f, 20f), new Vector3(41f, 2f, 1f), wallMat);
            Wall("WallS", new Vector3(0f, 1f, -20f), new Vector3(41f, 2f, 1f), wallMat);
            Wall("WallE", new Vector3(20f, 1f, 0f), new Vector3(1f, 2f, 41f), wallMat);
            Wall("WallW", new Vector3(-20f, 1f, 0f), new Vector3(1f, 2f, 41f), wallMat);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(55f, 40f, 0f);

            var tankPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Prefabs/Tank.prefab");
            if (tankPrefab == null)
            {
                Debug.LogError("[SceneBuilder] Tank.prefab missing — run PrefabFactory.CreateAll first");
                return;
            }
            var tank = (GameObject)PrefabUtility.InstantiatePrefab(tankPrefab);
            tank.transform.position = new Vector3(0f, 0.1f, 0f);

            // Stationary target dummies for playtests: input stripped so
            // they ignore the keyboard, Damageable stays (they die).
            foreach (var pos in new[]
            {
                new Vector3(-10f, 0.1f, 10f),
                new Vector3(12f, 0.1f, 6f),
                new Vector3(4f, 0.1f, -12f),
            })
            {
                var dummy = (GameObject)PrefabUtility.InstantiatePrefab(tankPrefab);
                dummy.transform.position = pos;
                dummy.name = "TargetDummy";
                Object.DestroyImmediate(dummy.GetComponent<PlayerTankInput>());
                Object.DestroyImmediate(dummy.GetComponent<PlaytestHotkeys>());
                Object.DestroyImmediate(dummy.GetComponent<DevHud>()); // one HUD only
            }

            // Weapon pickup pads
            var pickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Prefabs/WeaponPickup.prefab");
            var pool = new[] { "spread", "mortar", "mg" };
            var poolSpecs = new WeaponSpec[pool.Length];
            for (int i = 0; i < pool.Length; i++)
                poolSpecs[i] = AssetDatabase.LoadAssetAtPath<WeaponSpec>(
                    $"Assets/Resources/Specs/Weapons/{pool[i]}.asset");
            var padMat = Mat("PickupPad", new Color(0.25f, 0.25f, 0.28f));
            foreach (var pos in new[]
            {
                new Vector3(-12f, 0f, -12f), new Vector3(12f, 0f, -12f),
                new Vector3(-12f, 0f, 12f), new Vector3(12f, 0f, 12f),
            })
            {
                var padVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(padVisual.GetComponent<Collider>());
                padVisual.name = "PickupPad";
                padVisual.transform.position = pos + new Vector3(0f, 0.05f, 0f);
                padVisual.transform.localScale = new Vector3(2f, 0.05f, 2f);
                padVisual.GetComponent<Renderer>().sharedMaterial = padMat;
                var pad = padVisual.AddComponent<PickupPad>();
                pad.pickupPrefab = pickupPrefab;
                pad.weaponPool = poolSpecs;
            }

            var cam = new GameObject("FollowCamera").AddComponent<Camera>();
            cam.gameObject.tag = "MainCamera";
            cam.transform.position = new Vector3(0f, 22f, -8f);
            cam.transform.rotation = Quaternion.Euler(70f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.12f);
            var follow = cam.gameObject.AddComponent<CameraFollow>();
            follow.target = tank.transform;
            follow.offset = new Vector3(0f, 22f, -8f);

            Directory.CreateDirectory(ScenesDir);
            EditorSceneManager.SaveScene(scene, $"{ScenesDir}/DevArena.unity");
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneBuilder] DevArena.unity saved");
        }

        static void Wall(string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.position = pos;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
