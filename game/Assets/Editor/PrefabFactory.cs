using System.IO;
using UnityEditor;
using UnityEngine;

namespace Potshot.EditorTools
{
    /// <summary>
    /// Generates gameplay prefabs + spec assets. Idempotent; assets are
    /// get-or-create so GUIDs (and scene references) survive re-runs.
    /// Run: scripts/unity.sh -quit -executeMethod Potshot.EditorTools.PrefabFactory.CreateAll
    /// </summary>
    public static class PrefabFactory
    {
        const string MatDir = "Assets/Materials/Tank";
        const string SpecDir = "Assets/Resources/Specs";
        const string PrefabDir = "Assets/Resources/Prefabs";

        public static void CreateAll()
        {
            WeaponFactory.CreateSpecs();
            CreateBotSpec();
            var spec = CreateTankSpec();
            CreateProjectilePrefab();
            CreateWeaponPickupPrefab();
            CreateTankPrefab(spec);
            CreateTankNetPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("[PrefabFactory] CreateAll done");
        }

        public static void CreateWeaponPickupPrefab()
        {
            var root = new GameObject("WeaponPickup");
            try
            {
                var trigger = root.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = Vector3.one;

                // Spin the collider-less visual child, not the trigger.
                var visual = Visual(root.transform, PrimitiveType.Cube, "Visual",
                    Vector3.zero, Vector3.one * 0.6f,
                    MaterialFactory.GetOrCreate(MatDir, "PickupDefault", Color.white));
                visual.localRotation = Quaternion.Euler(35f, 45f, 0f);

                var pickup = root.AddComponent<WeaponPickup>();
                pickup.visual = visual;

                Directory.CreateDirectory(PrefabDir);
                PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/WeaponPickup.prefab");
                Debug.Log("[PrefabFactory] WeaponPickup.prefab saved");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Networked tank = separate prefab: NetworkObject
        /// deactivates its object when no server/client runs, which would
        /// break every offline scene/test if put on Tank.prefab (M2b
        /// review). Rebuild AFTER CreateTankPrefab.</summary>
        public static void CreateTankNetPrefab()
        {
            var tankPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Tank.prefab");
            var root = (GameObject)PrefabUtility.InstantiatePrefab(tankPrefab);
            try
            {
                PrefabUtility.UnpackPrefabInstance(root,
                    PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                root.name = "TankNet";

                var nob = root.AddComponent<FishNet.Object.NetworkObject>();
                var so = new SerializedObject(nob);
                var enable = so.FindProperty("_enablePrediction");
                if (enable == null)
                {
                    Debug.LogError("[PrefabFactory] NetworkObject._enablePrediction not found — FishNet API drift");
                    EditorApplication.Exit(1);
                    return;
                }
                enable.boolValue = true;
                var predictionType = so.FindProperty("_predictionType");
                int rbIndex = System.Array.IndexOf(predictionType.enumNames, "Rigidbody");
                predictionType.enumValueIndex = rbIndex;
                so.ApplyModifiedPropertiesWithoutUndo();

                root.AddComponent<Potshot.Net.NetworkTank>();

                PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/TankNet.prefab");
                Debug.Log("[PrefabFactory] TankNet.prefab saved");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        public static void CreateProjectilePrefab()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            try
            {
                go.name = "Projectile";
                go.layer = PotshotLayers.Projectile;

                var body = go.AddComponent<Rigidbody>();
                body.mass = 1f;
                body.useGravity = false; // per-shot via WeaponSpec
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                var col = go.GetComponent<SphereCollider>();
                col.sharedMaterial = FrictionlessMat(); // exact reflections

                go.AddComponent<Projectile>();

                Directory.CreateDirectory(PrefabDir);
                PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/Projectile.prefab");
                Debug.Log("[PrefabFactory] Projectile.prefab saved");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        public static BotSpec CreateBotSpec()
        {
            Directory.CreateDirectory(SpecDir);
            string path = $"{SpecDir}/BotSpec.asset";
            var spec = AssetDatabase.LoadAssetAtPath<BotSpec>(path);
            if (spec == null)
            {
                spec = ScriptableObject.CreateInstance<BotSpec>();
                AssetDatabase.CreateAsset(spec, path);
            }
            spec.reactionInterval = 0.2f;
            spec.aimJitterDeg = 4f;
            spec.preferredRangeMin = 8f;
            spec.preferredRangeMax = 12f;
            spec.fireRange = 16f;
            spec.pickupDetourRange = 12f;
            spec.wallProbeDistance = 3f;
            EditorUtility.SetDirty(spec);
            return spec;
        }

        public static TankSpec CreateTankSpec()
        {
            Directory.CreateDirectory(SpecDir);
            string path = $"{SpecDir}/TankSpec.asset";
            var spec = AssetDatabase.LoadAssetAtPath<TankSpec>(path);
            if (spec == null)
            {
                spec = ScriptableObject.CreateInstance<TankSpec>();
                AssetDatabase.CreateAsset(spec, path);
            }
            // docs/gameplay.md feel targets — single source of tuning truth.
            spec.topSpeed = 7f;
            spec.accel = 17.5f;
            spec.hullTurnDegPerSec = 540f;
            spec.turretDegPerSec = 360f;
            spec.boostMultiplier = 1.8f;
            spec.boostDuration = 1f;
            spec.boostCooldown = 4f;
            EditorUtility.SetDirty(spec);
            return spec;
        }

        public static void CreateTankPrefab(TankSpec spec)
        {
            var root = new GameObject("Tank");
            try
            {
                var body = root.AddComponent<Rigidbody>();
                body.mass = 1500f;
                body.linearDamping = 0f;
                body.angularDamping = 0.05f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.Continuous;
                body.constraints = RigidbodyConstraints.FreezeRotationX
                                 | RigidbodyConstraints.FreezeRotationZ;

                var box = root.AddComponent<BoxCollider>();
                box.size = new Vector3(1.6f, 0.6f, 2.2f);
                box.center = new Vector3(0f, 0.3f, 0f);
                // Frictionless: TankMotor owns all planar accel/decel — ground
                // friction would fight the velocity-driven model (M1c tests
                // measured 3.9 u/s equilibrium instead of 6 without this).
                box.sharedMaterial = FrictionlessMat();

                var hullMat = MaterialFactory.GetOrCreate(MatDir, "Hull", new Color(0.55f, 0.55f, 0.2f));
                var turretMat = MaterialFactory.GetOrCreate(MatDir, "Turret", new Color(0.4f, 0.45f, 0.2f));

                Visual(root.transform, PrimitiveType.Cube, "HullVisual",
                    new Vector3(0f, 0.3f, 0f), new Vector3(1.6f, 0.6f, 2.2f), hullMat);

                // Unscaled pivot: children keep true offsets — parenting
                // them under the scaled cylinder crushed the muzzle height
                // (y -0.3 became -0.045) and flattened the barrel (M1d).
                var turret = new GameObject("Turret").transform;
                turret.SetParent(root.transform, false);
                turret.localPosition = new Vector3(0f, 0.75f, 0f);
                Visual(turret, PrimitiveType.Cylinder, "TurretVisual",
                    Vector3.zero, new Vector3(1f, 0.15f, 1f), turretMat);
                Visual(turret, PrimitiveType.Cube, "Barrel",
                    new Vector3(0f, 0f, 1f), new Vector3(0.2f, 0.2f, 1.4f), turretMat);

                // Muzzle: child of the turret (aim frame), 1.9 u out — clear
                // of the 2.2 u-long hull collider so shots spawn outside it.
                var muzzle = new GameObject("Muzzle").transform;
                muzzle.SetParent(turret, false);
                // y -0.3: shells fly at world y≈0.45, inside the 0.1–0.7
                // band of tank colliders — at turret height they graze
                // clean over enemy hulls (M1d test-caught).
                muzzle.localPosition = new Vector3(0f, -0.3f, 1.9f);

                root.AddComponent<Damageable>().maxHealth = 100f;

                var weapon = root.AddComponent<WeaponController>();
                weapon.muzzle = muzzle;
                weapon.projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"{PrefabDir}/Projectile.prefab");
                weapon.current = AssetDatabase.LoadAssetAtPath<WeaponSpec>(
                    "Assets/Resources/Specs/Weapons/cannon.asset");
                weapon.defaultWeapon = weapon.current;

                var controller = root.AddComponent<TankController>();
                controller.spec = spec;
                controller.turret = turret;
                controller.weapon = weapon;
                root.AddComponent<PlayerTankInput>();
                root.AddComponent<PlaytestHotkeys>();
                root.AddComponent<DevHud>();

                Directory.CreateDirectory(PrefabDir);
                PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/Tank.prefab");
                Debug.Log("[PrefabFactory] Tank.prefab saved");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static PhysicsMaterial FrictionlessMat()
        {
            const string path = "Assets/Materials/Tank/Frictionless.physicMaterial";
            Directory.CreateDirectory(MatDir);
            var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (mat == null)
            {
                mat = new PhysicsMaterial("Frictionless");
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.dynamicFriction = 0f;
            mat.staticFriction = 0f;
            mat.frictionCombine = PhysicsMaterialCombine.Minimum;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Primitive visual child with its collider stripped —
        /// the compound body's only collider is the root BoxCollider.</summary>
        static Transform Visual(Transform parent, PrimitiveType type, string name,
            Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go.transform;
        }
    }
}
