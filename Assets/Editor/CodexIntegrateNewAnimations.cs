#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using GameClub.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// One-shot editor integration. Raw frames remain untouched until the parent
// agent performs the reviewed Git moves; this builder only copies their archive.
[InitializeOnLoad]
public static class CodexIntegrateNewAnimations
{
    private const string Requests = "Library/CodexAnimationIntegration";
    private const string Sprites = "Assets/Sprite/Player";
    private const string Clips = "Assets/Animations/Player/Clips";
    private const string Controller = "Assets/Animations/Player/Player.controller";
    private const string Archive = "SourceArt/OriginalAnimationFrames";
    private static bool busy;

    [Serializable] private sealed class Item
    {
        public string sourceFolder, texture, clip;
        public int count;
        public float fps, length;
        public bool loop;
    }
    [Serializable] private sealed class ArchiveFile { public string source, archived, sha256; }
    [Serializable] private sealed class Report
    {
        public bool success;
        public string error;
        public int configuredPlayers;
        public List<Item> animations = new List<Item>();
        public List<ArchiveFile> originalFiles = new List<ArchiveFile>();
    }

    static CodexIntegrateNewAnimations() => EditorApplication.update += Watch;
    private static void Watch()
    {
        string request = Requests + "/build.request";
        string poseRequest = Requests + "/poses.request";
        if (busy || (!File.Exists(request) && !File.Exists(poseRequest)) || EditorApplication.isPlayingOrWillChangePlaymode
            || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.Exists(poseRequest)) { File.Delete(poseRequest); TunePoseClips(); return; }
        File.Delete(request);
        Build();
    }

    [MenuItem("Tools/Gravity/Integrate November 6 player animations")]
    public static void Build()
    {
        if (busy) return;
        busy = true;
        var report = new Report();
        Directory.CreateDirectory(Requests);
        try
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before integrating assets.");
            if (EditorSceneManager.GetActiveScene().path != "Assets/Scenes/TestScene.unity")
                EditorSceneManager.OpenScene("Assets/Scenes/TestScene.unity", OpenSceneMode.Single);
            Directory.CreateDirectory(Sprites);
            Directory.CreateDirectory(Clips);
            var idle = AtlasAndClip("待机", "待机", "Idle", 10, 12f, false, null, report);
            var dash = AtlasAndClip("冲刺-带粒子", "冲刺-带粒子", "Dash", 13, 12f, true, null, report);
            var death = AtlasAndClip("死亡", "死亡", "Death", 13, 18f, false, null, report);
            var revive = AtlasAndClip("重生", "重生", "Revive", 16, 18f, false, null, report);
            var flip = AtlasAndClip("翻转", "翻转", "GravityFlip", 7, 20f, false, null, report);
            var fall = AtlasAndClip("跳跃", "跳跃-下降", "Fall", 1, 12f, true, "跳跃-下降.png", report);
            var ready = UpdateClip("IdleReady", PoseSprites("待机").Take(1).ToArray(), 12f, false);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
            if (!controller) throw new InvalidOperationException("The existing Player.controller is missing.");
            ConfigureController(controller, new Dictionary<string, AnimationClip> {
                {"Idle", idle}, {"Dash", dash}, {"Fall", fall},
                {"GravityFlip", flip}, {"Death", death}, {"Revive", revive}, {"IdleReady", ready}
            });
            ConfigurePlayers(controller, dash, death, revive, flip, report);
            ArchiveOriginals(report);
            AssetDatabase.SaveAssets();
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path == "Assets/Scenes/TestScene.unity")
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save TestScene.");
            }
            report.success = true;
            File.WriteAllText(Archive + "/animation-merge-20261106.json", JsonUtility.ToJson(report, true));
            Debug.Log("New player animation integration completed. " + report.configuredPlayers + " player(s) configured.");
        }
        catch (Exception exception) { report.error = exception.ToString(); Debug.LogException(exception); }
        finally
        {
            File.WriteAllText(Requests + "/build-report.json", JsonUtility.ToJson(report, true));
            busy = false;
        }
    }

    private static Sprite[] PoseSprites(string textureName)
    {
        return AssetDatabase.LoadAllAssetsAtPath(Sprites + "/" + textureName + ".png")
            .OfType<Sprite>().OrderBy(sprite => sprite.name, StringComparer.Ordinal).ToArray();
    }

    [MenuItem("Tools/Gravity/Apply flight dash and held idle poses")]
    public static void TunePoseClips()
    {
        if (busy) return;
        busy = true;
        Directory.CreateDirectory(Requests);
        try
        {
            Sprite[] idleFrames = PoseSprites("待机");
            Sprite[] dashFrames = PoseSprites("冲刺-带粒子");
            if (idleFrames.Length != 10 || dashFrames.Length != 13)
                throw new InvalidOperationException("Import the existing idle and dash atlases before tuning poses.");
            var idle = UpdateClip("Idle", idleFrames.Take(5).ToArray(), 12f, false);
            var dash = UpdateClip("Dash", dashFrames.Skip(6).Take(3).ToArray(), 12f, true);
            var ready = UpdateClip("IdleReady", idleFrames.Take(1).ToArray(), 12f, false);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
            if (!controller) throw new InvalidOperationException("Player.controller is missing.");
            ConfigureController(controller, new Dictionary<string, AnimationClip> {
                {"Idle", idle}, {"Dash", dash}, {"IdleReady", ready}
            });
            AssetDatabase.SaveAssets();
            File.WriteAllText(Requests + "/pose-settings.json", "{\"success\":true,\"idleDelay\":1.5,\"idleFinalFrame\":5,\"dashFrames\":[7,8,9],\"dashLoop\":true}");
            Debug.Log("Player poses updated: flying dash; delayed one-shot idle holding the lowered head.");
        }
        catch (Exception error) { File.WriteAllText(Requests + "/pose-error.txt", error.ToString()); Debug.LogException(error); }
        finally { busy = false; }
    }

    private static AnimationClip AtlasAndClip(string folder, string textureName, string clipName,
        int expected, float fps, bool loop, string singleName, Report report)
    {
        string source = "animation/" + folder;
        if (!Directory.Exists(source)) source = Archive + "/" + folder;
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException(source);
        string[] files = singleName != null ? new[] {Path.Combine(source, singleName)}
            : Directory.GetFiles(source, "*.png").OrderBy(NaturalFrame).ToArray();
        if (files.Length != expected || files.Any(file => !File.Exists(file)))
            throw new InvalidOperationException(folder + " expected " + expected + " frames, got " + files.Length);
        const int cell = 2048;
        int columns = Mathf.CeilToInt(Mathf.Sqrt(files.Length));
        int rows = Mathf.CeilToInt((float)files.Length / columns);
        string texturePath = Sprites + "/" + textureName + ".png";
        var atlas = new Texture2D(cell * columns, cell * rows, TextureFormat.RGBA32, false);
        atlas.name = textureName;
        try
        {
            for (int index = 0; index < files.Length; index++)
            {
                var frame = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    byte[] bytes = File.ReadAllBytes(files[index]);
                    if (bytes.Length < 8 || bytes[0] != 137 || bytes[1] != 80
                        || !ImageConversion.LoadImage(frame, bytes, false))
                        throw new IOException("A frame is not a downloaded PNG. Fetch its Git LFS content: " + files[index]);
                    if (frame.width != cell || frame.height != cell)
                        throw new InvalidOperationException("Expected original 2048x2048 canvas: " + files[index]);
                    int x = index % columns * cell;
                    int y = (rows - 1 - index / columns) * cell;
                    atlas.SetPixels32(x, y, cell, cell, frame.GetPixels32());
                }
                finally { Object.DestroyImmediate(frame); }
            }
            atlas.Apply(false, false);
            File.WriteAllBytes(texturePath, atlas.EncodeToPNG());
        }
        finally { Object.DestroyImmediate(atlas); }
        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 8192;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        var slices = new SpriteMetaData[files.Length];
        for (int index = 0; index < files.Length; index++)
            slices[index] = new SpriteMetaData {
                name = textureName + "-" + (index + 1).ToString("00"),
                rect = new Rect(index % columns * cell, (rows - 1 - index / columns) * cell, cell, cell),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            };
        // No existing spritesheet texture is overwritten: these six textures are new.
        // Existing .anim and .controller assets are edited in place to retain GUIDs.
        importer.spritesheet = slices;
        importer.SaveAndReimport();
        Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(texturePath).OfType<Sprite>()
            .OrderBy(sprite => sprite.name, StringComparer.Ordinal).ToArray();
        if (frames.Length != expected) throw new InvalidOperationException("Sprite slicing failed: " + texturePath);
        if (clipName == "Idle") frames = frames.Take(5).ToArray();
        if (clipName == "Dash") frames = frames.Skip(6).Take(3).ToArray();
        var clip = UpdateClip(clipName, frames, fps, loop);
        report.animations.Add(new Item {sourceFolder = folder, texture = texturePath,
            clip = Clips + "/" + clipName + ".anim", count = frames.Length, fps = fps, length = clip.length, loop = loop});
        return clip;
    }

    private static int NaturalFrame(string file)
    {
        var match = Regex.Match(Path.GetFileNameWithoutExtension(file), @"-(\d+)$");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    private static AnimationClip UpdateClip(string name, Sprite[] frames, float fps, bool loop)
    {
        string path = Clips + "/" + name + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (!clip)
        {
            clip = new AnimationClip {name = name};
            AssetDatabase.CreateAsset(clip, path);
        }
        Undo.RecordObject(clip, "Update player animation frames");
        clip.ClearCurves();
        clip.name = name;
        clip.frameRate = fps;
        clip.wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever;
        var keys = new ObjectReferenceKeyframe[frames.Length + 1];
        for (int index = 0; index < frames.Length; index++)
            keys[index] = new ObjectReferenceKeyframe {time = index / fps, value = frames[index]};
        keys[frames.Length] = new ObjectReferenceKeyframe {
            time = frames.Length / fps, value = loop ? frames[0] : frames[frames.Length - 1]
        };
        AnimationUtility.SetObjectReferenceCurve(clip,
            new EditorCurveBinding {path = "", type = typeof(SpriteRenderer), propertyName = "m_Sprite"}, keys);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.startTime = 0;
        settings.stopTime = frames.Length / fps;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void ConfigureController(AnimatorController controller, Dictionary<string, AnimationClip> clips)
    {
        Undo.RecordObject(controller, "Add lifecycle and gravity flip animation states");
        if (!controller.parameters.Any(parameter => parameter.name == "State"))
            controller.AddParameter("State", AnimatorControllerParameterType.Int);
        var machine = controller.layers[0].stateMachine;
        string[] names = {"Idle", "Run", "Rise", "Fall", "Land", "Dash", "Interact", "GravityFlip", "Death", "Revive", "IdleReady"};
        for (int value = 0; value < names.Length; value++)
        {
            string name = names[value];
            var state = machine.states.Select(child => child.state).FirstOrDefault(item => item.name == name);
            if (!state) state = machine.AddState(name, new Vector3(300 + value % 3 * 240, value / 3 * 100));
            if (clips.TryGetValue(name, out var clip)) state.motion = clip;
            state.writeDefaultValues = false;
            var transition = machine.anyStateTransitions.FirstOrDefault(item => item.destinationState == state
                && item.conditions.Any(condition => condition.parameter == "State"
                    && condition.mode == AnimatorConditionMode.Equals && Mathf.Approximately(condition.threshold, value)));
            if (!transition)
            {
                transition = machine.AddAnyStateTransition(state);
                transition.AddCondition(AnimatorConditionMode.Equals, value, "State");
            }
            transition.hasExitTime = false;
            transition.duration = 0;
            transition.canTransitionToSelf = false;
            transition.hasFixedDuration = true;
            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(transition);
        }
        EditorUtility.SetDirty(machine);
        EditorUtility.SetDirty(controller);
    }

    private static void ConfigurePlayers(AnimatorController controller, AnimationClip dash,
        AnimationClip death, AnimationClip revive, AnimationClip flip, Report report)
    {
        foreach (var visual in Object.FindObjectsOfType<PlayerVisuals>(true))
        {
            if (visual.gameObject.scene.path != "Assets/Scenes/TestScene.unity") continue;
            var animator = visual.GetComponentInChildren<Animator>(true);
            var sprite = animator ? animator.GetComponent<SpriteRenderer>() : null;
            if (!sprite || !animator) throw new InvalidOperationException("Player visual renderer or Animator missing.");
            Undo.RecordObject(animator, "Bind integrated player animation controller");
            animator.runtimeAnimatorController = controller;
            var serialized = new SerializedObject(visual);
            serialized.FindProperty("sprite").objectReferenceValue = sprite;
            serialized.FindProperty("animator").objectReferenceValue = animator;
            serialized.FindProperty("dashClip").objectReferenceValue = dash;
            serialized.FindProperty("deathClip").objectReferenceValue = death;
            serialized.FindProperty("reviveClip").objectReferenceValue = revive;
            serialized.FindProperty("gravityFlipClip").objectReferenceValue = flip;
            serialized.FindProperty("landingClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AnimationClip>(Clips + "/Land.anim");
            serialized.FindProperty("interactionClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AnimationClip>(Clips + "/Interact.anim");
            serialized.ApplyModifiedProperties();
            var life = visual.GetComponent<PlayerLifecycle>();
            if (!life) life = Undo.AddComponent<PlayerLifecycle>(visual.gameObject);
            Undo.RecordObject(life, "Match lifecycle to real animation durations");
            life.deathDuration = death.length;
            life.reviveDuration = revive.length;
            var reader = visual.GetComponent<PlayerAnimationState>();
            if (!reader) reader = Undo.AddComponent<PlayerAnimationState>(visual.gameObject);
            var readSettings = new SerializedObject(reader);
            var rendererProperty = readSettings.FindProperty("visual");
            if (rendererProperty != null) rendererProperty.objectReferenceValue = sprite;
            var placeholderProperty = readSettings.FindProperty("usePlaceholderColors");
            if (placeholderProperty != null) placeholderProperty.boolValue = false;
            readSettings.ApplyModifiedProperties();
            EditorUtility.SetDirty(life);
            EditorUtility.SetDirty(visual);
            EditorUtility.SetDirty(animator);
            report.configuredPlayers++;
        }
        if (report.configuredPlayers == 0) throw new InvalidOperationException("Open the main TestScene before running the integration.");
    }

    private static void ArchiveOriginals(Report report)
    {
        Directory.CreateDirectory(Archive);
        if (!Directory.Exists("animation")) return;
        using (var hasher = SHA256.Create())
            foreach (string original in Directory.GetFiles("animation", "*", SearchOption.AllDirectories))
            {
                string relative = original.Substring("animation".Length).TrimStart('/', '\\');
                string archived = Path.Combine(Archive, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(archived));
                string checksum = BitConverter.ToString(hasher.ComputeHash(File.ReadAllBytes(original))).Replace("-", "").ToLowerInvariant();
                if (File.Exists(archived))
                {
                    string prior = BitConverter.ToString(hasher.ComputeHash(File.ReadAllBytes(archived))).Replace("-", "").ToLowerInvariant();
                    if (prior != checksum) throw new IOException("Different original frame already exists in archive: " + archived);
                }
                else File.Copy(original, archived);
                report.originalFiles.Add(new ArchiveFile {source = original.Replace('\\', '/'),
                    archived = archived.Replace('\\', '/'), sha256 = checksum});
            }
    }
}
#endif
