#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameClub.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class CodexPoseVerify
{
    const string Folder = "Library/CodexAnimationIntegration";
    const string Pending = "CodexPoseVerify.Pending", Finishing = "CodexPoseVerify.Finishing";
    [Serializable] public class Check { public string name, details; public bool passed; }
    [Serializable] public class Report
    {
        public bool finished, passed, returnedToEditMode;
        public string error, scope = "Actual Animator state and SpriteRenderer observations; physics unchanged.";
        public List<Check> checks = new List<Check>();
    }
    static Report report;
    static PlayerLifecycle life;
    static PlayerInput input;
    static PlayerDash dash;
    static Animator animator;
    static SpriteRenderer sprite;
    static Sprite firstIdle, lastIdle;
    static HashSet<Sprite> flying;
    static HashSet<Sprite> dashFrames = new HashSet<Sprite>();
    static bool running, inputEnabled, wasBackground, sawReady, stable, sawDash, allDashFlying;
    static int phase;
    static float started, mark, idleEntered, idleDelay = 1.5f;
    static double deadline;
    static CodexPoseVerify()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += change => { if (change == PlayModeStateChange.EnteredEditMode) FinishEdit(); };
        EditorApplication.delayCall += () => { if (!EditorApplication.isPlaying) FinishEdit(); };
    }
    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!running && !SessionState.GetBool(Pending, false))
        {
            if (!File.Exists(Folder + "/pose-verify.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Folder + "/pose-verify.request");
            EditorSceneManager.OpenScene("Assets/Scenes/TestScene.unity", OpenSceneMode.Single);
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
            return;
        }
        if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
        if (!running)
        {
            running = true; phase = 0; report = new Report(); deadline = EditorApplication.timeSinceStartup + 18;
            sawReady = sawDash = false; stable = allDashFlying = true; dashFrames.Clear();
        }
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Pose verification timed out at phase " + phase);
            Sample();
        }
        catch (Exception ex) { End(ex.ToString()); }
    }
    static bool InState(string name) => animator.GetCurrentAnimatorStateInfo(0).shortNameHash == Animator.StringToHash(name);
    static void Add(string name, bool passed, string details)
        => report.checks.Add(new Check {name = name, passed = passed, details = details});
    static void Sample()
    {
        if (phase == 0)
        {
            life = Object.FindObjectOfType<PlayerLifecycle>();
            if (!life || life.State != PlayerLifeState.Playing) return;
            animator = life.GetComponentInChildren<Animator>(); sprite = animator ? animator.GetComponent<SpriteRenderer>() : null;
            dash = life.GetComponent<PlayerDash>(); input = life.GetComponent<PlayerInput>();
            if (!animator || !sprite || !dash || !input) throw new InvalidOperationException("Player animation components missing.");
            var idle = AssetDatabase.LoadAllAssetsAtPath("Assets/Sprite/Player/待机.png").OfType<Sprite>().OrderBy(s => s.name).ToArray();
            firstIdle = idle[0]; lastIdle = idle[4];
            flying = new HashSet<Sprite>(AssetDatabase.LoadAllAssetsAtPath("Assets/Sprite/Player/冲刺-带粒子.png")
                .OfType<Sprite>().Where(s => s.name.EndsWith("-07") || s.name.EndsWith("-08") || s.name.EndsWith("-09")));
            if (flying.Count != 3) throw new InvalidOperationException("The three flight sprites were not found.");
            var settings = new SerializedObject(life.GetComponent<PlayerVisuals>()).FindProperty("idleDelay");
            if (settings != null) idleDelay = settings.floatValue;
            inputEnabled = input.enabled; input.enabled = false;
            wasBackground = Application.runInBackground; Application.runInBackground = true;
            started = Time.time; phase = 1; return;
        }
        if (phase == 1)
        {
            if (InState("IdleReady") && sprite.sprite == firstIdle) sawReady = true;
            if (!InState("Idle")) return;
            idleEntered = Time.time;
            Add("idle.normal_standing_before_delay", sawReady, "Observed IdleReady and the first original sprite.");
            Add("idle.delayed_entry", idleEntered - started >= Mathf.Max(0f, idleDelay - 0.25f),
                "Observed delay=" + (idleEntered - started).ToString("F3") + "; configured=" + idleDelay);
            phase = 2; return;
        }
        if (phase == 2)
        {
            if (!InState("Idle") || sprite.sprite != lastIdle) return;
            Add("idle.reached_original_frame_05", true, sprite.sprite.name);
            mark = Time.time; stable = true; phase = 3; return;
        }
        if (phase == 3)
        {
            stable &= InState("Idle") && sprite.sprite == lastIdle;
            if (Time.time - mark < 1.1f) return;
            Add("idle.final_pose_held_over_one_second", stable, "Only the actual renderer sprite and Animator state were sampled.");
            if (!life.GetComponent<PlayerGroundProbe>().isGrounded) throw new InvalidOperationException("Player is not grounded for the dash check.");
            dash.TryBurst(1, true); mark = Time.time; phase = 4; return;
        }
        if (phase == 4)
        {
            if (InState("Dash"))
            {
                sawDash = true; dashFrames.Add(sprite.sprite); allDashFlying &= flying.Contains(sprite.sprite);
            }
            if (Time.time - mark < 0.25f) return;
            if (InState("Dash")) return;
            Add("dash.all_rendered_frames_are_07_to_09", sawDash && allDashFlying && dashFrames.Count >= 2,
                "Actual distinct sprites=" + dashFrames.Count + "; sawDash=" + sawDash);
            phase = 5; mark = Time.time;
        }
        if (phase == 5)
        {
            if (!InState("IdleReady") || sprite.sprite != firstIdle)
            {
                if (Time.time - mark > 2f) throw new InvalidOperationException("Dash did not return to the normal standing pose.");
                return;
            }
            Add("dash.returns_to_normal_standing", true, "IdleReady with original frame 01 after the actual ground burst.");
            End(null);
        }
    }
    static void End(string error)
    {
        report.error = error;
        report.passed = error == null && report.checks.Count >= 6 && report.checks.All(check => check.passed);
        if (phase > 0)
        {
            if (input) input.enabled = inputEnabled;
            Application.runInBackground = wasBackground;
        }
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Folder + "/pose-checks.json", JsonUtility.ToJson(report, true));
        SessionState.SetBool(Pending, false); SessionState.SetBool(Finishing, true);
        running = false; EditorApplication.isPlaying = false;
    }
    static void FinishEdit()
    {
        if (!SessionState.GetBool(Finishing, false)) return;
        SessionState.SetBool(Finishing, false);
        var result = JsonUtility.FromJson<Report>(File.ReadAllText(Folder + "/pose-checks.json"));
        result.finished = true; result.returnedToEditMode = true;
        File.WriteAllText(Folder + "/pose-checks.json", JsonUtility.ToJson(result, true));
        Debug.Log("Pose checks completed: " + result.passed + ". Unity remains open in Edit Mode.");
    }
}
#endif
