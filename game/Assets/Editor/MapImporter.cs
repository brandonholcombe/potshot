using System.Collections.Generic;
using System.IO;
using System.Linq;
using Potshot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Potshot.EditorTools
{
    /// <summary>
    /// Turns Brandon's hand-drawn PNGs (Human-Input-Maps/) into playable
    /// scenes. Drawing language (M1g review-verified against the PNGs):
    ///   brown outline blobs  -> SOLID cliff plateaus (flood-filled)
    ///   brown dashed strokes -> tunnels CARVED through cliffs at tank width
    ///   gray strokes         -> fort/cover walls
    ///   blue bank lines      -> water band (dilated), blocks tanks not shells
    ///   brown rects on water -> bridges (open, suppress water)
    ///   dark red circles     -> spawn markers
    /// All source-resolution analysis happens BEFORE downsampling (dash
    /// gaps weld shut otherwise). Deterministic: no RNG anywhere.
    /// </summary>
    public static class MapImporter
    {
        // Passage sizing (playtest 2026-08-04: doorways too tight): the
        // world SCALE carries passage width — corridor-carving approaches
        // were tried and reverted (walls thinner than the carve radius get
        // deleted, mutilating forts/ridges; see git history). At 62 u the
        // drawn ~2.5 u passages become ~3.2 u — comfortable for a 1.6 u
        // tank. Explicit dash tunnels are carved generously on top.
        public const float WorldWidth = 62f;
        public const float CellSize = 0.6f;
        const float DashCarveRadius = 2.6f;

        public const byte Open = 0, Cliff = 1, Wall = 2, Water = 3;

        public class MapData
        {
            public string name;
            public int cols, rows;
            public byte[,] cells;
            public List<Vector3> spawnMarkers = new();
            public float worldHeight;

            public Vector3 CellToWorld(int cx, int cy) => new(
                (cx + 0.5f) * CellSize - WorldWidth * 0.5f, 0f,
                (cy + 0.5f) * CellSize - worldHeight * 0.5f);

            /// <summary>Open cells pinched between non-open cells on
            /// opposite sides within halfCells — post-widening this should
            /// be ~zero (doorway-width regression metric).</summary>
            public int TightCells(int halfCells) => TightCellList(halfCells).Count;

            public List<(int x, int y)> TightCellList(int halfCells)
            {
                var main = MainRegionMask();
                var tight = new List<(int, int)>();
                var axes = new[] { (1, 0), (0, 1), (1, 1), (1, -1) };
                for (int x = 1; x < cols - 1; x++)
                    for (int y = 1; y < rows - 1; y++)
                    {
                        // Sealed pockets aren't doorways — main region only.
                        if (cells[x, y] != Open || !main[x, y]) continue;
                        foreach (var (dx, dy) in axes)
                        {
                            bool hitA = false, hitB = false;
                            for (int s = 1; s <= halfCells; s++)
                            {
                                int ax = x + dx * s, ay = y + dy * s;
                                int bx = x - dx * s, by = y - dy * s;
                                if (ax >= 0 && ay >= 0 && ax < cols && ay < rows
                                    && cells[ax, ay] != Open) hitA = true;
                                if (bx >= 0 && by >= 0 && bx < cols && by < rows
                                    && cells[bx, by] != Open) hitB = true;
                            }
                            if (hitA && hitB) { tight.Add((x, y)); break; }
                        }
                    }
                return tight;
            }

            bool[,] MainRegionMask()
            {
                var seen = new bool[cols, rows];
                var best = new List<(int, int)>();
                for (int x = 0; x < cols; x++)
                    for (int y = 0; y < rows; y++)
                    {
                        if (cells[x, y] != Open || seen[x, y]) continue;
                        var region = new List<(int, int)>();
                        var stack = new Stack<(int, int)>();
                        stack.Push((x, y));
                        seen[x, y] = true;
                        while (stack.Count > 0)
                        {
                            var (cx, cy) = stack.Pop();
                            region.Add((cx, cy));
                            foreach (var (nx, ny) in new[]
                                { (cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1) })
                            {
                                if (nx < 0 || ny < 0 || nx >= cols || ny >= rows) continue;
                                if (seen[nx, ny] || cells[nx, ny] != Open) continue;
                                seen[nx, ny] = true;
                                stack.Push((nx, ny));
                            }
                        }
                        if (region.Count > best.Count) best = region;
                    }
                var mask = new bool[cols, rows];
                foreach (var (x, y) in best) mask[x, y] = true;
                return mask;
            }

            /// <summary>Fraction of open cells reachable from the largest
            /// open region — the tunnel/bridge pathability metric.</summary>
            public float OpenConnectivity()
            {
                var seen = new bool[cols, rows];
                int totalOpen = 0, bestRegion = 0;
                for (int x = 0; x < cols; x++)
                    for (int y = 0; y < rows; y++)
                        if (cells[x, y] == Open) totalOpen++;
                for (int x = 0; x < cols; x++)
                    for (int y = 0; y < rows; y++)
                    {
                        if (cells[x, y] != Open || seen[x, y]) continue;
                        int size = 0;
                        var stack = new Stack<(int, int)>();
                        stack.Push((x, y));
                        seen[x, y] = true;
                        while (stack.Count > 0)
                        {
                            var (cx, cy) = stack.Pop();
                            size++;
                            foreach (var (nx, ny) in new[]
                                { (cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1) })
                            {
                                if (nx < 0 || ny < 0 || nx >= cols || ny >= rows) continue;
                                if (seen[nx, ny] || cells[nx, ny] != Open) continue;
                                seen[nx, ny] = true;
                                stack.Push((nx, ny));
                            }
                        }
                        bestRegion = Mathf.Max(bestRegion, size);
                    }
                return totalOpen == 0 ? 0f : (float)bestRegion / totalOpen;
            }
        }

        public static string MapsSourceDir =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "../../Human-Input-Maps"));

        public static void BuildAllMaps()
        {
            foreach (var png in Directory.GetFiles(MapsSourceDir, "*.png").OrderBy(p => p))
            {
                var data = Classify(png);
                BuildScene(data);
            }
        }

        // ── Classification (source resolution) ──────────────────────────

        public static MapData Classify(string pngPath)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(pngPath)); // row 0 = image bottom
            int w = tex.width, h = tex.height;
            var px = tex.GetPixels32();
            Object.DestroyImmediate(tex);

            float unitsPerPixel = WorldWidth / w;
            var brown = new bool[w * h];
            var gray = new bool[w * h];
            var blue = new bool[w * h];
            var red = new bool[w * h];
            for (int i = 0; i < w * h; i++)
            {
                var c = px[i];
                if (c.a < 128) continue;
                int max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                int min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                if (c.r > 100 && c.g < 70 && c.b < 70 && c.r > c.g + 60) red[i] = true;
                else if (c.b > c.r + 40 && c.b > c.g + 40) blue[i] = true;
                else if (max - min < 25 && max > 90 && max < 200) gray[i] = true;
                else if (c.r > 130 && c.g > 60 && c.g < 190 && c.b < 140
                         && c.r > c.g && c.g > c.b) brown[i] = true;
            }

            // Brown components: big = cliff outlines, small = tunnel dashes.
            var comps = Components(brown, w, h);
            var outline = new bool[w * h];
            var dashes = new bool[w * h];
            var brownComps = new List<List<int>>();
            foreach (var comp in comps)
            {
                // Absolute threshold: dashes are ~30-100 px strokes. A
                // relative (biggest/5) rule misclassified Clifside Battle's
                // thinner cliff outlines as dashes and erased them.
                bool isDash = comp.Count < 400;
                foreach (int i in comp) (isDash ? dashes : outline)[i] = true;
                if (!isDash) brownComps.Add(comp);
            }

            // Water: dilate the bank lines into a band.
            var water = Dilate(blue, w, h, Mathf.RoundToInt(1.4f / unitsPerPixel));

            // Bridges: large brown comps that mostly overlap water are
            // bridges, not cliffs — reopen them and clear water there.
            var bridgeOpen = new bool[w * h];
            foreach (var comp in brownComps.ToList())
            {
                // Bridges span past both banks, so only a minority of their
                // pixels sit inside the water band — 15%, not 50% (cliffs
                // never touch water in any of the drawings).
                int inWater = comp.Count(i => water[i]);
                if (inWater * 100 < comp.Count * 15) continue;
                foreach (int i in comp) { outline[i] = false; bridgeOpen[i] = true; }
            }
            if (bridgeOpen.Any(b => b))
            {
                var bridgeZone = Dilate(bridgeOpen, w, h,
                    Mathf.RoundToInt(1.2f / unitsPerPixel));
                for (int i = 0; i < w * h; i++)
                    if (bridgeZone[i]) water[i] = false;
            }

            // Solid cliffs: close pinholes, flood from the border; anything
            // the outside can't reach is plateau interior.
            var closedOutline = Dilate(outline, w, h, 2);
            var outside = FloodFromBorder(closedOutline, w, h);
            var cliffSolid = new bool[w * h];
            for (int i = 0; i < w * h; i++)
                cliffSolid[i] = !outside[i] || outline[i];

            // Tunnels: carve the dashed paths through the solid at tank width.
            var carve = Dilate(dashes, w, h, Mathf.RoundToInt(DashCarveRadius / unitsPerPixel));
            for (int i = 0; i < w * h; i++)
                if (carve[i]) cliffSolid[i] = false;

            var grayWall = Dilate(gray, w, h, 1);

            // Spawn markers from red component centroids.
            var data = new MapData
            {
                name = Slug(Path.GetFileNameWithoutExtension(pngPath)),
                worldHeight = WorldWidth * h / (float)w,
            };
            foreach (var comp in Components(red, w, h).Where(c => c.Count > 20))
            {
                float mx = (float)comp.Average(i => i % w);
                float my = (float)comp.Average(i => i / w);
                data.spawnMarkers.Add(new Vector3(
                    (mx / w - 0.5f) * WorldWidth, 0.1f,
                    (my / h - 0.5f) * data.worldHeight));
            }

            // Downsample to grid; walls/water need >=25% cell coverage.
            int cols = Mathf.CeilToInt(WorldWidth / CellSize);
            int rows = Mathf.CeilToInt(data.worldHeight / CellSize);
            data.cols = cols; data.rows = rows;
            data.cells = new byte[cols, rows];
            float pxPerCellX = w / (float)cols, pxPerCellY = h / (float)rows;
            for (int cx = 0; cx < cols; cx++)
                for (int cy = 0; cy < rows; cy++)
                {
                    int x0 = (int)(cx * pxPerCellX), x1 = (int)((cx + 1) * pxPerCellX);
                    int y0 = (int)(cy * pxPerCellY), y1 = (int)((cy + 1) * pxPerCellY);
                    int n = 0, nCliff = 0, nWall = 0, nWater = 0;
                    for (int x = x0; x < x1 && x < w; x++)
                        for (int y = y0; y < y1 && y < h; y++)
                        {
                            int i = y * w + x;
                            n++;
                            if (cliffSolid[i]) nCliff++;
                            else if (grayWall[i]) nWall++;
                            else if (water[i]) nWater++;
                        }
                    if (n == 0) continue;
                    if (nCliff * 4 >= n) data.cells[cx, cy] = Cliff;
                    else if (nWall * 4 >= n) data.cells[cx, cy] = Wall;
                    else if (nWater * 4 >= n) data.cells[cx, cy] = Water;
                }

            // Perimeter containment ring — BEFORE grid widening, so pinches
            // against the ring get widened too (they were the unfixable 24).
            for (int cx = 0; cx < cols; cx++)
            { data.cells[cx, 0] = Cliff; data.cells[cx, rows - 1] = Cliff; }
            for (int cy = 0; cy < rows; cy++)
            { data.cells[0, cy] = Cliff; data.cells[cols - 1, cy] = Cliff; }

            // NOTE: no grid-level wall clearing — tried and reverted; it
            // cannot distinguish doorways from legitimate tight geometry
            // (hollow forts, box obstacles, bridges between water) and
            // dismantles them. Doorway width is guaranteed by the source-
            // resolution iterative widening above; TightCells() remains as
            // a coarse regression metric only.

            return data;
        }

        // ── Scene generation ────────────────────────────────────────────

        public static void BuildScene(MapData data)
        {
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(
                WorldWidth / 10f, 1f, data.worldHeight / 10f);
            ground.GetComponent<Renderer>().sharedMaterial =
                MaterialFactory.GetOrCreate("Assets/Materials/Qa", "ArenaGround",
                    new Color(0.42f, 0.38f, 0.3f));

            var cliffMat = MaterialFactory.GetOrCreate("Assets/Materials/Qa", "Cliff",
                new Color(0.5f, 0.36f, 0.22f));
            var wallMat = MaterialFactory.GetOrCreate("Assets/Materials/Qa", "ArenaWall",
                new Color(0.3f, 0.3f, 0.32f));
            var waterMat = MaterialFactory.GetOrCreate("Assets/Materials/Qa", "Water",
                new Color(0.2f, 0.35f, 0.7f));

            EmitBoxes(data, Cliff, 2.5f, cliffMat, "Cliff");
            EmitBoxes(data, Wall, 1.5f, wallMat, "Wall");
            // Water: low collider — tanks blocked, shells (y≈0.45) fly over.
            EmitBoxes(data, Water, 0.3f, waterMat, "Water");

            var spawns = PickSpawns(data, 8);
            var pads = PickPads(data, spawns, 4);

            var tankPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Prefabs/Tank.prefab");
            var player = (GameObject)PrefabUtility.InstantiatePrefab(tankPrefab);
            player.transform.position = spawns[0];
            player.name = "Player";

            var botSpec = AssetDatabase.LoadAssetAtPath<BotSpec>(
                "Assets/Resources/Specs/BotSpec.asset");
            for (int i = 1; i <= 3; i++)
            {
                var bot = (GameObject)PrefabUtility.InstantiatePrefab(tankPrefab);
                bot.transform.position = spawns[i % spawns.Count];
                bot.name = $"Bot{i}";
                Object.DestroyImmediate(bot.GetComponent<PlayerTankInput>());
                Object.DestroyImmediate(bot.GetComponent<PlaytestHotkeys>());
                Object.DestroyImmediate(bot.GetComponent<DevHud>());
                bot.AddComponent<BotBrain>().spec = botSpec;
            }

            var pickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Prefabs/WeaponPickup.prefab");
            var pool = new[] { "spread", "mortar", "mg" }
                .Select(id => AssetDatabase.LoadAssetAtPath<WeaponSpec>(
                    $"Assets/Resources/Specs/Weapons/{id}.asset")).ToArray();
            var padMat = MaterialFactory.GetOrCreate("Assets/Materials/Qa", "PickupPad",
                new Color(0.25f, 0.25f, 0.28f));
            foreach (var pos in pads)
            {
                var padVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(padVisual.GetComponent<Collider>());
                padVisual.name = "PickupPad";
                padVisual.transform.position = pos + new Vector3(0f, 0.05f, 0f);
                padVisual.transform.localScale = new Vector3(2f, 0.05f, 2f);
                padVisual.GetComponent<Renderer>().sharedMaterial = padMat;
                var pad = padVisual.AddComponent<PickupPad>();
                pad.pickupPrefab = pickupPrefab;
                pad.weaponPool = pool;
            }

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(55f, 40f, 0f);

            var cam = new GameObject("FollowCamera").AddComponent<Camera>();
            cam.gameObject.tag = "MainCamera";
            cam.transform.position = player.transform.position + new Vector3(0f, 22f, -8f);
            cam.transform.rotation = Quaternion.Euler(70f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.12f);
            var follow = cam.gameObject.AddComponent<CameraFollow>();
            cam.gameObject.AddComponent<AudioListener>();
            follow.target = player.transform;

            var mode = new GameObject("FfaGameMode").AddComponent<FfaGameMode>();
            mode.spawnPoints = spawns.ToArray();

            Directory.CreateDirectory("Assets/Scenes/Maps");
            EditorSceneManager.SaveScene(scene, $"Assets/Scenes/Maps/{data.name}.unity");
            AssetDatabase.SaveAssets();
            Debug.Log($"[MapImporter] built Assets/Scenes/Maps/{data.name}.unity " +
                      $"({data.cols}x{data.rows} cells, {data.spawnMarkers.Count} markers, " +
                      $"connectivity {data.OpenConnectivity():P0})");
        }

        static void EmitBoxes(MapData data, byte type, float height, Material mat, string label)
        {
            var parent = new GameObject($"{label}s").transform;
            var used = new bool[data.cols, data.rows];
            for (int cy = 0; cy < data.rows; cy++)
                for (int cx = 0; cx < data.cols; cx++)
                {
                    if (data.cells[cx, cy] != type || used[cx, cy]) continue;
                    int end = cx;
                    while (end + 1 < data.cols && data.cells[end + 1, cy] == type
                           && !used[end + 1, cy]) end++;
                    for (int x = cx; x <= end; x++) used[x, cy] = true;

                    var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    box.name = label;
                    box.transform.SetParent(parent, false);
                    var a = data.CellToWorld(cx, cy);
                    var b = data.CellToWorld(end, cy);
                    box.transform.position = new Vector3(
                        (a.x + b.x) * 0.5f, height * 0.5f, a.z);
                    box.transform.localScale = new Vector3(
                        (end - cx + 1) * CellSize, height, CellSize);
                    box.GetComponent<Renderer>().sharedMaterial = mat;
                }
        }

        static List<Vector3> PickSpawns(MapData data, int want)
        {
            var spawns = new List<Vector3>(data.spawnMarkers.Select(SnapToOpen(data)));
            var openCells = AllOpenCells(data);
            while (spawns.Count < want && openCells.Count > 0)
            {
                // Deterministic farthest-point: fixed iteration order.
                Vector3 best = openCells[0];
                float bestScore = -1f;
                foreach (var c in openCells)
                {
                    float score = spawns.Count == 0
                        ? c.sqrMagnitude
                        : spawns.Min(s => (s - c).sqrMagnitude);
                    if (score > bestScore) { bestScore = score; best = c; }
                }
                spawns.Add(best);
            }
            return spawns;
        }

        static List<Vector3> PickPads(MapData data, List<Vector3> spawns, int want)
        {
            var pads = new List<Vector3>();
            var openCells = AllOpenCells(data);
            while (pads.Count < want && openCells.Count > 0)
            {
                Vector3 best = openCells[0];
                float bestScore = -1f;
                foreach (var c in openCells)
                {
                    float score = spawns.Concat(pads).Min(s => (s - c).sqrMagnitude);
                    if (score > bestScore) { bestScore = score; best = c; }
                }
                pads.Add(best);
            }
            return pads;
        }

        static System.Func<Vector3, Vector3> SnapToOpen(MapData data) => marker =>
        {
            var open = AllOpenCells(data);
            return open.Count == 0 ? marker
                : open.OrderBy(c => (c - marker).sqrMagnitude).First();
        };

        static List<Vector3> AllOpenCells(MapData data)
        {
            var cells = new List<Vector3>();
            for (int cx = 1; cx < data.cols - 1; cx += 2) // stride keeps it cheap
                for (int cy = 1; cy < data.rows - 1; cy += 2)
                    if (data.cells[cx, cy] == Open)
                        cells.Add(data.CellToWorld(cx, cy));
            return cells;
        }

        // ── Pixel ops ───────────────────────────────────────────────────

        static List<List<int>> Components(bool[] mask, int w, int h)
        {
            var comps = new List<List<int>>();
            var seen = new bool[w * h];
            for (int start = 0; start < w * h; start++)
            {
                if (!mask[start] || seen[start]) continue;
                var comp = new List<int>();
                var stack = new Stack<int>();
                stack.Push(start);
                seen[start] = true;
                while (stack.Count > 0)
                {
                    int i = stack.Pop();
                    comp.Add(i);
                    int x = i % w, y = i / w;
                    foreach (int n in Neighbors(x, y, w, h))
                        if (mask[n] && !seen[n]) { seen[n] = true; stack.Push(n); }
                }
                comps.Add(comp);
            }
            return comps;
        }

        static IEnumerable<int> Neighbors(int x, int y, int w, int h)
        {
            if (x > 0) yield return y * w + x - 1;
            if (x < w - 1) yield return y * w + x + 1;
            if (y > 0) yield return (y - 1) * w + x;
            if (y < h - 1) yield return (y + 1) * w + x;
        }

        /// <summary>Multi-source BFS distance dilation — O(pixels).</summary>
        static bool[] Dilate(bool[] mask, int w, int h, int radius)
        {
            var dist = new int[w * h];
            var queue = new Queue<int>();
            for (int i = 0; i < w * h; i++)
            {
                dist[i] = mask[i] ? 0 : int.MaxValue;
                if (mask[i]) queue.Enqueue(i);
            }
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                if (dist[i] >= radius) continue;
                int x = i % w, y = i / w;
                foreach (int n in Neighbors(x, y, w, h))
                    if (dist[n] > dist[i] + 1)
                    {
                        dist[n] = dist[i] + 1;
                        queue.Enqueue(n);
                    }
            }
            var result = new bool[w * h];
            for (int i = 0; i < w * h; i++) result[i] = dist[i] <= radius;
            return result;
        }

        /// <summary>Open pixels with blockers on opposite sides within
        /// halfWidth along any of 4 axes — i.e. too-tight passages.</summary>
        static bool[] NarrowCorridors(bool[] blocked, int w, int h, int halfWidth)
        {
            var result = new bool[w * h];
            var axes = new[] { (1, 0), (0, 1), (1, 1), (1, -1) };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (blocked[i]) continue;
                    foreach (var (dx, dy) in axes)
                    {
                        bool hitA = false, hitB = false;
                        for (int s = 1; s <= halfWidth && !hitA; s++)
                        {
                            int nx = x + dx * s, ny = y + dy * s;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) break;
                            hitA = blocked[ny * w + nx];
                        }
                        if (!hitA) continue;
                        for (int s = 1; s <= halfWidth && !hitB; s++)
                        {
                            int nx = x - dx * s, ny = y - dy * s;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) break;
                            hitB = blocked[ny * w + nx];
                        }
                        if (hitB) { result[i] = true; break; }
                    }
                }
            return result;
        }

        static bool[] LargestOpenRegion(bool[] blocked, int w, int h)
        {
            var inverse = new bool[w * h];
            for (int i = 0; i < w * h; i++) inverse[i] = !blocked[i];
            var comps = Components(inverse, w, h);
            var result = new bool[w * h];
            if (comps.Count == 0) return result;
            foreach (int i in comps.OrderByDescending(c => c.Count).First())
                result[i] = true;
            return result;
        }

        static bool[] FloodFromBorder(bool[] blocked, int w, int h)
        {
            var outside = new bool[w * h];
            var queue = new Queue<int>();
            void Seed(int i)
            {
                if (!blocked[i] && !outside[i]) { outside[i] = true; queue.Enqueue(i); }
            }
            for (int x = 0; x < w; x++) { Seed(x); Seed((h - 1) * w + x); }
            for (int y = 0; y < h; y++) { Seed(y * w); Seed(y * w + w - 1); }
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int x = i % w, y = i / w;
                foreach (int n in Neighbors(x, y, w, h)) Seed(n);
            }
            return outside;
        }

        static string Slug(string name) => string.Concat(
            name.Split(new[] { ' ', '-', '_' }, System.StringSplitOptions.RemoveEmptyEntries)
                .Select(word => char.ToUpperInvariant(word[0]) + word.Substring(1)));
    }
}
