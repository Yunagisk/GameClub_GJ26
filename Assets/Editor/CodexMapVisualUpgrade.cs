#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Cinemachine;
using GameClub.Puzzles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class CodexMapVisualUpgrade
{
    const string Folder = "Library/CodexMapVisualUpgrade";
    const string ArtFolder = "Assets/Art/MapBackgrounds";
    const string SkySource = "C:/Users/ASUS/.codex/generated_images/01a1ad09-76f6-7920-909d-5c4f83b4861a/exec-a1ffd929-b535-4594-8fe9-e6293a49261c.png";
    const string VisualRoot = "SurfaceAndUndergroundVisuals";
    static Material spriteMaterial;
    static Sprite solid;
    static bool busy;
    [Serializable] class Report
    {
        public bool success, collisionCellsIdentical, sceneSaved, originalPuzzleObjectsPreserved;
        public string error, beforeGeometryHash, afterGeometryHash, scene;
        public int beforeCells, afterCells, metalCells, surfaceMetalCells, undergroundMetalCells, disabledRoomCameras;
        public int gateCountBefore, gateCountAfter, playerCount;
        public float followOrthographicSize;
        public List<string> previews = new List<string>();
        public List<string> changedRegions = new List<string>();
    }
    struct SourceCell { public Tilemap map; public Vector3Int local, world; }
    static CodexMapVisualUpgrade() { EditorApplication.update += Tick; }
    static void Tick()
    {
        if (busy || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.Exists(Folder + "/build.request"))
        {
            File.Delete(Folder + "/build.request");
            try { busy = true; Build(); } catch (Exception e) { Debug.LogException(e); } finally { busy = false; }
        }
    }
    [MenuItem("Tools/Gravity/Apply metal terrain and surface underground backgrounds")]
    public static void Build()
    {
        Directory.CreateDirectory(Folder);
        var report = new Report();
        try
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/TestScene.unity") throw new InvalidOperationException("Open TestScene first; the builder preserves the live scene.");
            if (Application.isPlaying) throw new InvalidOperationException("Edit mode required.");
            string backup = Folder + "/before-visual-upgrade.unity";
            if (!File.Exists(backup)) EditorSceneManager.SaveScene(scene, backup, true);
            var mapRoot = scene.GetRootGameObjects().Single(go => go.name == "MainMap");
            var puzzleIds = mapRoot.GetComponentsInChildren<MonoBehaviour>(true).Where(c => c && c.GetType().Namespace == "GameClub.Puzzles")
                .Select(c => c.GetInstanceID()).OrderBy(id => id).ToArray();
            report.gateCountBefore = mapRoot.GetComponentsInChildren<MonoBehaviour>(true).Count(c => c && c.GetType().Name.Contains("Gate"));
            var before = SolidCells(mapRoot);
            report.beforeCells = before.Count; report.beforeGeometryHash = Hash(before);
            CodexMetalTiles.Build();
            CodexNormalizeCamera.Apply();
            EnsureArt();
            spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(ArtFolder + "/MapSpriteDefault.mat");
            if (!spriteMaterial) { spriteMaterial = new Material(Shader.Find("Sprites/Default")); AssetDatabase.CreateAsset(spriteMaterial, ArtFolder + "/MapSpriteDefault.mat"); }
            solid = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/SolidWhite.asset");
            if (!solid)
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false); tex.name = "SolidWhiteTexture";
                tex.SetPixels(new[] {Color.white, Color.white, Color.white, Color.white}); tex.Apply();
                AssetDatabase.CreateAsset(tex, ArtFolder + "/SolidWhiteTexture.asset");
                solid = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f), 2f); solid.name = "SolidWhite";
                AssetDatabase.CreateAsset(solid, ArtFolder + "/SolidWhite.asset");
            }
            var previous = mapRoot.transform.Find(VisualRoot);
            // The solid tilemap survives a visual rebuild, so a second run never loses converted terrain.
            var metalObject = previous ? previous.Find("MetalGround") : null;
            if (metalObject) metalObject.SetParent(mapRoot.transform, true);
            if (previous) Object.DestroyImmediate(previous.gameObject);
            var visuals = new GameObject(VisualRoot, typeof(Grid)); visuals.transform.SetParent(mapRoot.transform, false);
            visuals.GetComponent<Grid>().cellSize = Vector3.one;
            Tilemap metal;
            if (metalObject) { metalObject.SetParent(visuals.transform, true); metal = metalObject.GetComponent<Tilemap>(); }
            else metal = CreateMetalMap(visuals.transform);
            var occupancy = before;
            var selected = new List<SourceCell>();
            foreach (var source in CollisionMaps(mapRoot))
            {
                if (source == metal) continue;
                foreach (var cell in source.cellBounds.allPositionsWithin)
                {
                    if (!source.HasTile(cell)) continue;
                    var w = WorldCell(source, cell);
                    bool upper = w.x >= -8 && w.x < 68 && w.y >= -3;
                    bool industry = (w.x >= 22 && w.x < 55 && w.y >= -38 && w.y <= -36)
                        || (w.x >= 85 && w.x < 108 && w.y >= -28 && w.y < -25)
                        || (w.x >= 92 && w.x < 135 && w.y >= -78 && w.y < -75)
                        || (w.x >= 54 && w.x < 70 && w.y >= -36 && w.y < -25);
                    if (upper || industry) selected.Add(new SourceCell {map = source, local = cell, world = w});
                }
            }
            var tiles = CodexMetalTiles.Tiles;
            foreach (var c in selected)
            {
                c.map.SetTile(c.local, null);
                metal.SetTile(c.world, PickTile(c.world, occupancy, tiles));
            }
            metal.color = new Color(.90f, .94f, .98f, 1f);
            metal.CompressBounds();
            foreach (var p in metal.cellBounds.allPositionsWithin)
            {
                if (!metal.HasTile(p)) continue;
                report.metalCells++;
                if (p.y >= -3) report.surfaceMetalCells++; else report.undergroundMetalCells++;
            }
            Outdoor(mapRoot.transform, visuals.transform);
            Underground(visuals.transform);
            foreach (var collider in mapRoot.GetComponentsInChildren<TilemapCollider2D>(true))
            {
                collider.ProcessTilemapChanges();
                var composite = collider.GetComponent<CompositeCollider2D>();
                if (composite && composite.generationType == CompositeCollider2D.GenerationType.Manual) composite.GenerateGeometry();
            }
            Physics2D.SyncTransforms();
            var after = SolidCells(mapRoot);
            report.afterCells = after.Count; report.afterGeometryHash = Hash(after);
            report.collisionCellsIdentical = before.SetEquals(after);
            if (!report.collisionCellsIdentical) throw new InvalidOperationException("Terrain occupancy changed; saved backup is available.");
            report.gateCountAfter = mapRoot.GetComponentsInChildren<MonoBehaviour>(true).Count(c => c && c.GetType().Name.Contains("Gate"));
            var afterPuzzleIds = mapRoot.GetComponentsInChildren<MonoBehaviour>(true).Where(c => c && c.GetType().Namespace == "GameClub.Puzzles" && !(c is GravityCameraFraming))
                .Select(c => c.GetInstanceID()).OrderBy(id => id).ToArray();
            report.originalPuzzleObjectsPreserved = puzzleIds.All(afterPuzzleIds.Contains);
            report.disabledRoomCameras = mapRoot.GetComponentsInChildren<PuzzleCameraArea>(true).Count(c => !c.enabled && (!c.view || !c.view.enabled));
            report.playerCount = Object.FindObjectsOfType<GameClub.Player.PlayerController2D>().Length;
            var follow = Object.FindObjectsOfType<CinemachineVirtualCamera>().Single(c => c.name == "Virtual Camera");
            report.followOrthographicSize = follow.m_Lens.OrthographicSize;
            report.changedRegions.AddRange(new[] {"Landing: outdoor sky, distant ruined city and natural foreground; upper terrain metal", "Entrance hall: indoor metal panels and wall grilles", "Pendulum and return stair: metal structure over original geometry", "Laboratory and core: metal platforms; dark underground supports", "All underground routes retain dark opaque wall coverage"});
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene); report.sceneSaved = EditorSceneManager.SaveScene(scene);
            report.scene = scene.path;
            RenderPreview("01-landing", new Vector2(5, 4), 6.8f, report);
            RenderPreview("02-hall", new Vector2(39, 4), 6.8f, report);
            RenderPreview("03-transport", new Vector2(49, -42), 6.8f, report);
            RenderPreview("04-pendulum", new Vector2(39, -29), 7.5f, report);
            RenderPreview("05-laboratory", new Vector2(97, -22), 6.8f, report);
            RenderPreview("06-core", new Vector2(123, -72), 6.8f, report);
            var player = Object.FindObjectOfType<GameClub.Player.PlayerController2D>();
            if (SceneView.lastActiveSceneView && player)
                SceneView.lastActiveSceneView.LookAt(player.transform.position + new Vector3(3f, 2f), Quaternion.identity, 9f, true, true);
            report.success = report.sceneSaved && report.originalPuzzleObjectsPreserved && report.surfaceMetalCells > 0 && report.disabledRoomCameras == 6;
            Debug.Log("Map visual upgrade complete. Metal cells=" + report.metalCells + "; collision cells unchanged=" + report.collisionCellsIdentical);
        }
        catch (Exception e) { report.error = e.ToString(); throw; }
        finally { File.WriteAllText(Folder + "/report.json", JsonUtility.ToJson(report, true)); }
    }
    static void EnsureArt()
    {
        if (!AssetDatabase.IsValidFolder(ArtFolder)) AssetDatabase.CreateFolder("Assets/Art", "MapBackgrounds");
        string target = ArtFolder + "/SurfaceRuins.png";
        if (!File.Exists(target)) File.Copy(SkySource, target);
        AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(target);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f; importer.mipmapEnabled = false; importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 2048; importer.SaveAndReimport();
    }
    static Tilemap CreateMetalMap(Transform parent)
    {
        var go = new GameObject("MetalGround", typeof(Tilemap), typeof(TilemapRenderer)); go.transform.SetParent(parent, false);
        go.layer = LayerMask.NameToLayer("Ground");
        var renderer = go.GetComponent<TilemapRenderer>(); renderer.sharedMaterial = spriteMaterial; renderer.sortingOrder = 0;
        var map = go.GetComponent<Tilemap>(); map.tileAnchor = new Vector3(.5f, .5f, 0);
        go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        var composite = go.AddComponent<CompositeCollider2D>(); composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        composite.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/Tiles/Rock/RockContact.physicsMaterial2D");
        var collider = go.AddComponent<TilemapCollider2D>(); collider.usedByComposite = true; collider.extrusionFactor = .01f;
        return map;
    }
    static IEnumerable<Tilemap> CollisionMaps(GameObject root) => root.GetComponentsInChildren<Tilemap>(true)
        .Where(t => t.gameObject.activeInHierarchy && t.GetComponent<TilemapCollider2D>() && t.GetComponent<TilemapCollider2D>().enabled);
    static Vector3Int WorldCell(Tilemap map, Vector3Int cell)
    {
        Vector3 p = map.CellToWorld(cell);
        if (Mathf.Abs(p.x - Mathf.Round(p.x)) > .001f || Mathf.Abs(p.y - Mathf.Round(p.y)) > .001f)
            throw new InvalidOperationException("Non-integral tilemap transform: " + map.name);
        return new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), 0);
    }
    static HashSet<Vector3Int> SolidCells(GameObject root)
    {
        var set = new HashSet<Vector3Int>();
        foreach (var map in CollisionMaps(root)) foreach (var c in map.cellBounds.allPositionsWithin) if (map.HasTile(c)) set.Add(WorldCell(map, c));
        return set;
    }
    static string Hash(HashSet<Vector3Int> cells)
    {
        string text = string.Join(";", cells.OrderBy(c => c.x).ThenBy(c => c.y).Select(c => c.x + "," + c.y));
        using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
    }
    static TileBase PickTile(Vector3Int p, HashSet<Vector3Int> all, TileBase[] tiles)
    {
        if (!all.Contains(p + Vector3Int.left)) return tiles[0];
        if (!all.Contains(p + Vector3Int.right)) return tiles[5];
        int slice = ((p.x % 3) + 3) % 3;
        return tiles[(((p.x / 3 + p.y) % 5 == 0) ? 9 : 6) + slice];
    }
    static void Outdoor(Transform map, Transform parent)
    {
        foreach (string path in new[] {"01_Landing/Background", "01_Landing/BackgroundStructure_NoCollision"})
        { var t = map.Find(path); if (t) t.gameObject.SetActive(false); }
        var sky = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/SurfaceRuins.png");
        var sr = AddSprite(parent, "Surface_SkyRuins_Nature", sky, new Vector2(10, 8.375f), -45, Color.white);
        sr.transform.localScale = new Vector3(44f / sky.bounds.size.x, 24.75f / sky.bounds.size.y, 1);
        // The entrance is a building above ground: opaque panels distinguish its interior from the outdoor sky.
        var hall = new GameObject("Surface_EntranceInterior"); hall.transform.SetParent(parent, false);
        Quad(hall.transform, "Wall", new Rect(24, 0, 29, 8), new Color(.17f, .21f, .25f), -28);
        for (int x = 25; x <= 53; x += 7) Quad(hall.transform, "WallSupport_" + x, new Rect(x, 0, .32f, 8), new Color(.24f, .28f, .32f), -19);
        foreach (int x in new[] {29, 36, 46}) Vent(hall.transform, new Vector2(x, 5.7f), 1.25f);
        var main = Camera.main; if (main) main.backgroundColor = new Color(.09f, .11f, .14f);
    }
    static void Underground(Transform parent)
    {
        var root = new GameObject("Underground_DarkStructures_NoCollision"); root.transform.SetParent(parent, false);
        // Sparse structural shapes keep existing rock walls visible and never resemble additional playable platforms.
        WallBay(root.transform, "TransportLeft", new Rect(22, -46, 13, 8));
        WallBay(root.transform, "TransportRight", new Rect(55, -46, 13, 8));
        WallBay(root.transform, "Control", new Rect(2, -46, 18, 15));
        WallBay(root.transform, "Pendulum", new Rect(25, -36, 28, 14));
        WallBay(root.transform, "Laboratory", new Rect(86, -26, 20, 8));
        WallBay(root.transform, "FieldsUpper", new Rect(99, -48, 34, 10));
        WallBay(root.transform, "FieldsLower", new Rect(99, -63, 34, 9));
        WallBay(root.transform, "Core", new Rect(94, -76, 39, 8));
    }
    static void WallBay(Transform parent, string name, Rect bounds)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var tint = new Color(.13f, .16f, .19f, 1);
        float spacing = Mathf.Max(8f, bounds.width / 3f);
        for (float x = bounds.xMin + 1.2f; x < bounds.xMax; x += spacing)
        {
            Quad(go.transform, "RecessedSupport", new Rect(x, bounds.yMin, .35f, bounds.height), tint, -18);
            Vent(go.transform, new Vector2(x + 2.5f, bounds.yMax - 2), .8f);
        }
        Quad(go.transform, "UpperWallSeam", new Rect(bounds.xMin, bounds.yMax - .65f, bounds.width, .14f), new Color(.18f, .21f, .24f), -18);
    }
    static void Vent(Transform parent, Vector2 position, float scale)
    {
        var vents = CodexMetalTiles.FullDecorations;
        if (vents.Length == 0) return;
        var sr = AddSprite(parent, "WallVent_NoCollision", vents[0], position, -16, new Color(.40f, .45f, .50f));
        sr.transform.localScale = new Vector3(scale, scale, 1);
    }
    static SpriteRenderer AddSprite(Transform parent, string name, Sprite sprite, Vector2 position, int order, Color tint)
    {
        var go = new GameObject(name, typeof(SpriteRenderer)); go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(position.x, position.y, 2);
        var renderer = go.GetComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sharedMaterial = spriteMaterial;
        renderer.sortingOrder = order; renderer.color = tint; return renderer;
    }
    static void Quad(Transform parent, string name, Rect rect, Color color, int order)
    {
        var sr = AddSprite(parent, name, solid, rect.center, order, color);
        sr.transform.localScale = new Vector3(rect.width, rect.height, 1);
    }
    static void RenderPreview(string name, Vector2 center, float size, Report report)
    {
        var go = new GameObject("VisualPreviewCamera", typeof(Camera));
        var camera = go.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = size;
        camera.transform.position = new Vector3(center.x, center.y, -10); camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.09f, .11f, .14f); camera.cullingMask = ~0;
        var rt = new RenderTexture(1280, 720, 24); rt.Create(); camera.targetTexture = rt;
        var old = RenderTexture.active;
        try
        {
            camera.Render(); RenderTexture.active = rt;
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
            string path = Folder + "/" + name + ".png"; File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture); report.previews.Add(path);
        }
        finally { RenderTexture.active = old; camera.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(go); }
    }
}
#endif
