#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

// Imports the user's original pixels, without image generation or colour changes.
public static class CodexMetalTiles
{
    public const string ArtRoot = "Assets/Art/MetalTiles/Textures";
    public const string TileRoot = "Assets/Tiles/Metal";
    public const string PalettePath = TileRoot + "/Palettes/MetalPalette.prefab";
    static readonly string[] Groups = {"Metal_Left", "Metal_Right", "Metal_Middle", "Metal_Cracked", "Metal_Vent"};
    static readonly string[] SourceNames = {
        "67c3edec4e78a4c9c7117083ce7ced86.png",
        "ef017b7ab19af5de67e38a053d74ff20.png",
        "fb174496954b8145d54218a2fd9f0ab3.png",
        "bf3e381130c086f9a797f54805b0f7ea.png",
        "8d8ae9e642721014c668c1be06f5182d.png"
    };
    const string SourceFolder = "C:/Users/ASUS/Documents/Tencent Files/1335547082/nt_qq/nt_data/Pic/2026-11/Ori";

    // Index = group * 3 + slice; groups are Left, Right, Middle, Cracked, Vent.
    public static TileBase[] Tiles
    {
        get
        {
            var result = new TileBase[15];
            for (int group = 0; group < Groups.Length; group++)
                for (int slice = 0; slice < 3; slice++)
                    result[group * 3 + slice] = AssetDatabase.LoadAssetAtPath<Tile>(TileRoot + "/Tiles/" + Groups[group] + "_" + slice + ".asset");
            return result;
        }
    }

    // One 3x1 world-unit sprite for decorative wall vents (no collider supplied).
    public static Sprite[] FullDecorations => AssetDatabase.LoadAllAssetsAtPath(ArtRoot + "/Metal_Vent.png")
        .OfType<Sprite>().Where(sprite => sprite.name == "Metal_Vent_Full").ToArray();

    [Serializable] class ImportedFile { public string source, imported, sha256; }
    [Serializable] class Report
    {
        public bool success, originalPixelsPreserved;
        public int tileCount, sliceCount, fullDecorationCount;
        public string palette, error;
        public List<ImportedFile> files = new List<ImportedFile>();
    }

    [MenuItem("Tools/Gravity/Import user metal tiles")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Import metal tiles in Edit Mode.");
        Directory.CreateDirectory("Library/CodexMetalTiles");
        var report = new Report();
        Scene previous = SceneManager.GetActiveScene();
        Scene temporary = default(Scene);
        try
        {
            EnsureFolder(ArtRoot); EnsureFolder(TileRoot + "/Tiles"); EnsureFolder(TileRoot + "/Palettes");
            for (int group = 0; group < Groups.Length; group++)
            {
                string source = Path.Combine(SourceFolder, SourceNames[group]);
                string target = ArtRoot + "/" + Groups[group] + ".png";
                if (!File.Exists(source))
                {
                    if (!File.Exists(target)) throw new FileNotFoundException("User source image was not found.", source);
                }
                else if (!File.Exists(target) || Sha(source) != Sha(target)) File.Copy(source, target, true);
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(target) as TextureImporter;
                if (!importer) throw new InvalidOperationException("Texture importer is missing: " + target);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.spritePixelsPerUnit = 64f;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 512;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
                var slices = new List<SpriteMetaData>();
                for (int slice = 0; slice < 3; slice++) slices.Add(new SpriteMetaData {
                    name = Groups[group] + "_" + slice,
                    rect = new Rect(slice * 64, 0, 64, 64),
                    alignment = (int)SpriteAlignment.Center, pivot = new Vector2(.5f, .5f)
                });
                if (group == 4) slices.Add(new SpriteMetaData {
                    name = "Metal_Vent_Full", rect = new Rect(0, 0, 192, 64),
                    alignment = (int)SpriteAlignment.Center, pivot = new Vector2(.5f, .5f)
                });
                importer.spritesheet = slices.ToArray(); importer.SaveAndReimport();
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(target);
                if (!texture || texture.width != 192 || texture.height != 64) throw new InvalidOperationException("Expected original 192x64 image: " + target);
                var sprites = AssetDatabase.LoadAllAssetsAtPath(target).OfType<Sprite>().ToDictionary(sprite => sprite.name);
                for (int slice = 0; slice < 3; slice++)
                {
                    string name = Groups[group] + "_" + slice;
                    if (!sprites.TryGetValue(name, out var sprite)) throw new InvalidOperationException("Slice missing: " + name);
                    string tilePath = TileRoot + "/Tiles/" + name + ".asset";
                    var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
                    if (!tile) { tile = ScriptableObject.CreateInstance<Tile>(); AssetDatabase.CreateAsset(tile, tilePath); }
                    tile.name = name; tile.sprite = sprite; tile.color = Color.white;
                    tile.transform = Matrix4x4.identity;
                    tile.flags = TileFlags.LockColor | TileFlags.LockTransform;
                    tile.colliderType = Tile.ColliderType.Grid;
                    EditorUtility.SetDirty(tile); report.sliceCount++;
                }
                report.files.Add(new ImportedFile {source = source.Replace('\\', '/'), imported = target, sha256 = Sha(target)});
            }
            AssetDatabase.SaveAssets();
            var allTiles = Tiles;
            if (allTiles.Length != 15 || allTiles.Any(tile => !tile)) throw new InvalidOperationException("Expected fifteen reusable metal Tile assets.");
            report.tileCount = allTiles.Length; report.fullDecorationCount = FullDecorations.Length;

            // Palette authoring happens in a temporary additive scene, preserving the user's map.
            temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(temporary);
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(PalettePath))
                GridPaletteUtility.CreateNewPalette(TileRoot + "/Palettes", "MetalPalette", GridLayout.CellLayout.Rectangle,
                    GridPalette.CellSizing.Manual, new Vector3(1, 1, 0), GridLayout.CellSwizzle.XYZ);
            var contents = PrefabUtility.LoadPrefabContents(PalettePath);
            try
            {
                var map = contents.GetComponentInChildren<Tilemap>();
                if (!map) throw new InvalidOperationException("Metal palette Tilemap is missing.");
                map.ClearAllTiles();
                for (int group = 0; group < Groups.Length; group++)
                    for (int slice = 0; slice < 3; slice++) map.SetTile(new Vector3Int(slice, -group * 2, 0), allTiles[group * 3 + slice]);
                map.CompressBounds(); PrefabUtility.SaveAsPrefabAsset(contents, PalettePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            AssetDatabase.ImportAsset(PalettePath, ImportAssetOptions.ForceSynchronousImport);
            if (!AssetDatabase.LoadAssetAtPath<GridPalette>(PalettePath)) throw new InvalidOperationException("Metal palette settings sub-asset is missing.");
            report.palette = PalettePath;
            File.WriteAllText(TileRoot + "/使用说明.txt", Instructions);
            AssetDatabase.ImportAsset(TileRoot + "/使用说明.txt");
            report.originalPixelsPreserved = true; report.success = true;
            AssetDatabase.SaveAssets();
        }
        catch (Exception error) { report.error = error.ToString(); Debug.LogException(error); throw; }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (temporary.IsValid() && temporary.isLoaded) EditorSceneManager.CloseScene(temporary, true);
            File.WriteAllText("Library/CodexMetalTiles/import-report.json", JsonUtility.ToJson(report, true));
        }
    }

    static string Sha(string path)
    {
        using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
    }
    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    const string Instructions =
        "金属瓦片：5张用户原图均192×64，保持原始像素和颜色。\n" +
        "PPU=64；Grid cell=1×1；每张切3片64×64，共15片Tile。\n" +
        "在Tile Palette窗口选择MetalPalette；若未自动显示，可从Assets/Tiles/Metal/Palettes选中MetalPalette.prefab。\n" +
        "五行依次为Left、Right、Middle、Cracked、Vent，每行从左到右为原图3段。\n" +
        "连续横梁建议：Left_0作左端，Middle/Cracked变化铺中间，Right_2作右端。\n" +
        "Vent三片可作为3格宽完整格栅放到无碰撞背景Tilemap；Metal_Vent_Full另供SpriteRenderer装饰。\n" +
        "实体Tile均设Grid碰撞；要成为可碰撞地面，目标Tilemap仍需配置TilemapCollider2D及对应Ground层。\n" +
        "纯背景装饰放到没有Collider的Tilemap；这5图完全不透明。\n" +
        "若需要翻转单格，先将该格TileFlags设为None再应用TransformMatrix。\n";
}
#endif
