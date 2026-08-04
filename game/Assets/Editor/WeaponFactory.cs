using System.IO;
using UnityEditor;
using UnityEngine;

namespace Potshot.EditorTools
{
    /// <summary>
    /// Weapon tuning lives in this C# table — the generated assets in
    /// Resources/Specs/Weapons/ are derived artifacts. Values trace to
    /// docs/gameplay.md (cannon = 2-hit kill vs 100 HP, etc.).
    /// </summary>
    public static class WeaponFactory
    {
        const string SpecDir = "Assets/Resources/Specs/Weapons";
        const string MatDir = "Assets/Materials/Weapons";

        struct Row
        {
            public string id, name;
            public float cooldown, speed, damage;
            public int ricochets, pellets;
            public float spread;
            public bool gravity;
            public float aoe, scale;
            public int ammo;
            public Color color;
        }

        static readonly Row[] Table =
        {
            new Row { id = "cannon", name = "Cannon", cooldown = 0.8f, speed = 14f,
                      damage = 50f, ricochets = 1, pellets = 1, spread = 0f,
                      gravity = false, aoe = 0f, scale = 0.3f, ammo = 0,
                      color = new Color(0.15f, 0.15f, 0.15f) },
            new Row { id = "spread", name = "Spread Shot", cooldown = 1.0f, speed = 12f,
                      damage = 15f, ricochets = 0, pellets = 5, spread = 30f,
                      gravity = false, aoe = 0f, scale = 0.2f, ammo = 8,
                      color = new Color(0.9f, 0.5f, 0.1f) },
            new Row { id = "mortar", name = "Mortar", cooldown = 1.5f, speed = 0f,
                      damage = 60f, ricochets = 0, pellets = 1, spread = 0f,
                      gravity = true, aoe = 2.5f, scale = 0.4f, ammo = 5,
                      color = new Color(0.2f, 0.6f, 0.2f) },
            new Row { id = "mg", name = "Machine Gun", cooldown = 0.1f, speed = 30f,
                      damage = 8f, ricochets = 0, pellets = 1, spread = 3f,
                      gravity = false, aoe = 0f, scale = 0.15f, ammo = 40,
                      color = new Color(0.95f, 0.85f, 0.2f) },
        };

        public static void CreateSpecs()
        {
            Directory.CreateDirectory(SpecDir);
            foreach (var row in Table)
            {
                string path = $"{SpecDir}/{row.id}.asset";
                var spec = AssetDatabase.LoadAssetAtPath<WeaponSpec>(path);
                if (spec == null)
                {
                    spec = ScriptableObject.CreateInstance<WeaponSpec>();
                    AssetDatabase.CreateAsset(spec, path);
                }
                spec.id = row.id;
                spec.displayName = row.name;
                spec.fireCooldown = row.cooldown;
                spec.projectileSpeed = row.speed;
                spec.damage = row.damage;
                spec.ricochets = row.ricochets;
                spec.pelletCount = row.pellets;
                spec.spreadAngleDeg = row.spread;
                spec.useGravity = row.gravity;
                spec.aoeRadius = row.aoe;
                spec.projectileScale = row.scale;
                spec.ammo = row.ammo;
                spec.projectileMaterial =
                    MaterialFactory.GetOrCreate(MatDir, $"Projectile_{row.id}", row.color);
                EditorUtility.SetDirty(spec);
            }
            Debug.Log($"[WeaponFactory] {Table.Length} weapon specs written");
        }
    }
}
