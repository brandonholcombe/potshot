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
            var spec = CreateTankSpec();
            CreateTankPrefab(spec);
            AssetDatabase.SaveAssets();
            Debug.Log("[PrefabFactory] CreateAll done");
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
            spec.topSpeed = 6f;
            spec.accel = 15f;
            spec.hullTurnDegPerSec = 540f;
            spec.turretDegPerSec = 360f;
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

                var turret = Visual(root.transform, PrimitiveType.Cylinder, "Turret",
                    new Vector3(0f, 0.75f, 0f), new Vector3(1f, 0.15f, 1f), turretMat);
                Visual(turret, PrimitiveType.Cube, "Barrel",
                    new Vector3(0f, 0f, 1f), new Vector3(0.2f, 0.2f, 1.4f), turretMat);

                var controller = root.AddComponent<TankController>();
                controller.spec = spec;
                controller.turret = turret;
                root.AddComponent<PlayerTankInput>();

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
