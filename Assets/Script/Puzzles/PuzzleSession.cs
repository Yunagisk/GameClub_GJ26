using System.Collections.Generic;
using Cinemachine;
using GameClub.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameClub.Puzzles
{
    [DefaultExecutionOrder(-500), DisallowMultipleComponent]
    public sealed class PuzzleSession : MonoBehaviour
    {
        public static PuzzleSession Instance { get; private set; }
        public Rigidbody2D PlayerBody;
        public GravityBody2D Gravity;
        public PuzzleRoom initialRoom;
        public float respawnDelay = 0.35f;
        public float respawnProtection = 0.9f;
        public float killBelowY = -110f;
        [Tooltip("Only brief control tutorials; no objectives, counters or permanent HUD.")]
        public bool showHUD = true;
        public string[] requiredFlags = {
            "hall_done", "bridge_done", "strength_done", "pendulum_routed", "laser_done", "fields_done"
        };
        public PuzzleRoom CurrentRoom { get; private set; }
        public bool IsRespawning { get; private set; }
        public bool Completed { get; private set; }
        public bool InputBlocked => IsRespawning || completionPanel;
        public int DeathCount { get; private set; }
        public string[] ActiveFlags => new List<string>(flags).ToArray();

        private readonly HashSet<string> flags = new HashSet<string>();
        private PlayerController2D controller;
        private Collider2D playerCollider;
        private PuzzlePlayerEnvironment environment;
        private PlayerInteractionPicker picker;
        private Vector2 fallbackSpawn;
        private bool fallbackUpward;
        private bool controllerWasEnabled = true;
        private bool bodyWasSimulated = true;
        private bool completionPanel;
        private float respawnAt;
        private float protectedUntil;
        private string controlHint;
        private float controlHintUntil;
        private float controlHintStarted;
        private float nextControlCheck;
        private bool exchangeControlsShown, terminalControlsShown;
        private Collider2D[] exchangeHintTargets;
        private Font font;
        private GUIStyle headingStyle, bodyStyle, hintStyle, centerStyle, buttonStyle;

        private void Awake()
        {
            if (Instance && Instance != this)
            {
                enabled = false;
                return;
            }
            Instance = this;
            if (!PlayerBody)
            {
                var player = FindObjectOfType<PlayerController2D>();
                if (player) PlayerBody = player.GetComponent<Rigidbody2D>();
            }
            CachePlayer();
        }

        private void CachePlayer()
        {
            if (!PlayerBody) return;
            if (!Gravity) Gravity = PlayerBody.GetComponent<GravityBody2D>();
            controller = PlayerBody.GetComponent<PlayerController2D>();
            playerCollider = PlayerBody.GetComponent<Collider2D>();
            environment = PlayerBody.GetComponent<PuzzlePlayerEnvironment>();
            picker = PlayerBody.GetComponent<PlayerInteractionPicker>();
        }

        private void Start()
        {
            CachePlayer();
            if (!PlayerBody || !Gravity)
            {
                Debug.LogError("PuzzleSession needs the scene's Player Rigidbody2D and GravityBody2D.", this);
                enabled = false;
                return;
            }
            fallbackSpawn = PlayerBody.position;
            fallbackUpward = Gravity.startsUpward;
            foreach (var room in FindObjectsOfType<PuzzleRoom>(true)) room.CaptureInitialState();
            if (initialRoom) CurrentRoom = initialRoom;
            protectedUntil = Time.unscaledTime + respawnProtection;
            var targets = new List<Collider2D>();
            foreach (var body in FindObjectsOfType<GravityBody2D>())
            {
                if (body == Gravity || body.gameObject.layer != 8) continue;
                var target = body.GetComponentInChildren<Collider2D>();
                if (target) targets.Add(target);
            }
            exchangeHintTargets = targets.ToArray();
            ShowControlHint("A / D 移动   ·   W 跳跃   ·   Shift 冲刺", 6f);
        }

        private void Update()
        {
            if (!PlayerBody) return;
            if (IsRespawning)
            {
                if (Time.unscaledTime >= respawnAt) FinishRespawn();
                return;
            }
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                ResetRoom();
                return;
            }
            if (PlayerBody.position.y < killBelowY) KillPlayer("离开了安全区域，已返回检查点");
            UpdateControlHints();
        }

        public bool IsPlayer(Collider2D collider) => collider && PlayerBody
            && (collider.attachedRigidbody == PlayerBody || collider.transform.IsChildOf(PlayerBody.transform));

        public bool Solved(string key) => !string.IsNullOrEmpty(key) && flags.Contains(key);

        public void SetFlag(string key, bool value)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            if (value) flags.Add(key);
            else
            {
                flags.Remove(key);
                if (Completed && IsRequired(key))
                {
                    Completed = false;
                    DismissCompletion();
                }
            }
        }

        public void Solve(string key) => SetFlag(key, true);

        private bool IsRequired(string key)
        {
            if (requiredFlags == null) return false;
            foreach (string required in requiredFlags) if (required == key) return true;
            return false;
        }

        public void SetCheckpoint(PuzzleRoom room, bool normalizeGravity = true)
        {
            if (!room || room == CurrentRoom || IsRespawning) return;
            room.CaptureInitialState();
            CurrentRoom = room;
            if (picker) picker.ClearSelection();
            if (normalizeGravity && Gravity)
            {
                if (environment) environment.ResetEnvironment();
                Gravity.ResetGravity(room.spawnUpward);
                PlayerBody.velocity = new Vector2(PlayerBody.velocity.x, 0f);
                PlayerBody.GetComponent<PlayerGroundProbe>()?.LeaveGround();
            }
            protectedUntil = Time.unscaledTime + 0.35f;
        }

        public void KillPlayer(string reason)
        {
            if (!PlayerBody || IsRespawning || completionPanel || Time.unscaledTime < protectedUntil) return;
            DeathCount++;
            BeginRespawn(string.IsNullOrEmpty(reason) ? "返回检查点" : reason);
        }

        public void ResetRoom()
        {
            if (!PlayerBody || IsRespawning) return;
            DismissCompletion();
            Completed = false;
            BeginRespawn("已复位当前房间");
        }

        private void BeginRespawn(string reason)
        {
            IsRespawning = true;
            respawnAt = Time.unscaledTime + Mathf.Max(0f, respawnDelay);
            controllerWasEnabled = !controller || controller.enabled;
            bodyWasSimulated = PlayerBody.simulated;
            if (controller) controller.enabled = false;
            if (picker) picker.ClearSelection();
            PlayerBody.velocity = Vector2.zero;
            PlayerBody.angularVelocity = 0f;
            PlayerBody.simulated = false;
            if (!exchangeControlsShown) ShowControlHint("R 复位当前房间", 5f);
        }

        private void FinishRespawn()
        {
            Vector3 before = PlayerBody.transform.position;
            if (CurrentRoom) CurrentRoom.ResetRoomState();
            Vector2 spawn = CurrentRoom ? CurrentRoom.spawnPoint : fallbackSpawn;
            bool up = CurrentRoom ? CurrentRoom.spawnUpward : fallbackUpward;
            if (environment) environment.ResetEnvironment();
            Gravity.ResetGravity(up);
            PlayerBody.position = spawn;
            PlayerBody.transform.position = new Vector3(spawn.x, spawn.y, before.z);
            PlayerBody.velocity = Vector2.zero;
            PlayerBody.angularVelocity = 0f;
            PlayerBody.GetComponent<PlayerGroundProbe>()?.LeaveGround();
            var input = PlayerBody.GetComponent<PlayerInputReader>();
            if (input) input.Consume(out _, out _, out _);
            var dash = PlayerBody.GetComponent<PlayerDash>();
            var motor = PlayerBody.GetComponent<PlayerHorizontalMotor>();
            if (dash) dash.Prepare(true, motor ? motor.groundMaxSpeed : 7f);
            Physics2D.SyncTransforms();
            PlayerBody.simulated = bodyWasSimulated;
            if (controller) controller.enabled = controllerWasEnabled;
            IsRespawning = false;
            protectedUntil = Time.unscaledTime + Mathf.Max(0.1f, respawnProtection);
            SnapFollowCameras(PlayerBody.transform.position - before);
        }

        private void SnapFollowCameras(Vector3 delta)
        {
            foreach (var camera in FindObjectsOfType<CinemachineVirtualCameraBase>())
            {
                if (!camera.Follow || (camera.Follow != PlayerBody.transform
                    && !camera.Follow.IsChildOf(PlayerBody.transform))) continue;
                camera.OnTargetObjectWarped(camera.Follow, delta);
                camera.PreviousStateIsValid = false;
            }
        }

        public void Complete()
        {
            if (requiredFlags != null)
                foreach (string requirement in requiredFlags)
                    if (!string.IsNullOrEmpty(requirement) && !Solved(requirement))
                    {
                        return;
                    }
            Completed = true;
            if (!completionPanel)
            {
                controllerWasEnabled = !controller || controller.enabled;
                if (controller) controller.enabled = false;
                if (PlayerBody) PlayerBody.velocity = Vector2.zero;
            }
            completionPanel = true;
            if (picker) picker.ClearSelection();
        }

        private void DismissCompletion()
        {
            if (!completionPanel) return;
            completionPanel = false;
            if (controller) controller.enabled = controllerWasEnabled;
        }

        private void ShowControlHint(string message, float duration)
        {
            controlHint = message;
            controlHintStarted = Time.unscaledTime;
            controlHintUntil = controlHintStarted + duration;
        }

        private void UpdateControlHints()
        {
            if (!showHUD || InputBlocked || Time.unscaledTime < controlHintUntil
                || Time.unscaledTime < nextControlCheck) return;
            nextControlCheck = Time.unscaledTime + 0.25f;
            if (!exchangeControlsShown && exchangeHintTargets != null)
            {
                var camera = Camera.main;
                if (camera)
                    foreach (var target in exchangeHintTargets)
                    {
                        if (!target || !target.enabled || !target.gameObject.activeInHierarchy) continue;
                        Vector3 point = camera.WorldToViewportPoint(target.bounds.center);
                        if (point.z <= 0 || point.x < 0.04f || point.x > 0.96f
                            || point.y < 0.08f || point.y > 0.92f) continue;
                        exchangeControlsShown = true;
                        ShowControlHint("左键选中   ·   右键交换重力   ·   R 复位当前房间", 6f);
                        return;
                    }
            }
            if (!terminalControlsShown && InteractableBase.FindNearest(playerCollider) is PuzzleTerminal)
            {
                terminalControlsShown = true;
                ShowControlHint("F 操作终端", 4f);
            }
        }

        private void EnsureStyles()
        {
            if (headingStyle != null) return;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Noto Sans CJK SC", "Arial" }, 20);
            headingStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 21, fontStyle = FontStyle.Bold, wordWrap = true };
            bodyStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 17, wordWrap = true };
            hintStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 16, wordWrap = true };
            centerStyle = new GUIStyle(bodyStyle) { alignment = TextAnchor.MiddleCenter };
            buttonStyle = new GUIStyle(GUI.skin.button) { font = font, fontSize = 18 };
            headingStyle.normal.textColor = Color.white;
            bodyStyle.normal.textColor = Color.white;
            hintStyle.normal.textColor = new Color(0.84f, 0.91f, 0.94f);
            centerStyle.normal.textColor = Color.white;
        }

        private static void Backdrop(Rect rect, float alpha)
        {
            Color old = GUI.color;
            GUI.color = new Color(0.025f, 0.035f, 0.055f, alpha);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void OnGUI()
        {
            if (!PlayerBody || !Gravity) return;
            bool hasControlHint = showHUD && !InputBlocked && Time.unscaledTime < controlHintUntil
                && !string.IsNullOrEmpty(controlHint);
            if (!hasControlHint && !completionPanel) return;
            EnsureStyles();
            Matrix4x4 old = GUI.matrix;
            float scale = Mathf.Clamp(Screen.width / 1280f, 0.75f, 1.5f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale, height = Screen.height / scale;
            if (hasControlHint)
            {
                float fade = Mathf.Min(Mathf.Clamp01((Time.unscaledTime - controlHintStarted) / 0.25f),
                    Mathf.Clamp01((controlHintUntil - Time.unscaledTime) / 0.45f));
                float hintWidth = Mathf.Min(680f, width - 32f);
                var hintRect = new Rect((width - hintWidth) * 0.5f, height - 82f, hintWidth, 54f);
                Backdrop(hintRect, 0.68f * fade);
                Color previousColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, fade);
                GUI.Label(new Rect(hintRect.x + 12f, hintRect.y + 5f, hintWidth - 24f, 44f), controlHint, centerStyle);
                GUI.color = previousColor;
            }
            if (completionPanel)
            {
                Backdrop(new Rect(0, 0, width, height), 0.74f);
                Rect panel = new Rect(width * 0.5f - 330, height * 0.5f - 160, 660, 320);
                Backdrop(panel, 0.96f);
                GUI.Label(new Rect(panel.x + 35, panel.y + 28, 590, 42), "记录已回收 · 重力核心已关闭", headingStyle);
                GUI.Label(new Rect(panel.x + 35, panel.y + 85, 590, 120), "人类把星球的重力变成能源，却没能停止最后一次抽取。\n\n你关闭了仍在运行的设备，带走了这座基地最后的记录。", bodyStyle);
                GUI.Label(new Rect(panel.x + 35, panel.y + 212, 590, 32), "全部六类机关已完成  ·  Heptagram Game Jam", hintStyle);
                if (GUI.Button(new Rect(panel.x + 195, panel.y + 264, 270, 42), "继续探索", buttonStyle)) DismissCompletion();
            }
            GUI.matrix = old;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (font) Destroy(font);
        }
    }
}
