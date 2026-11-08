using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Chuangyou.Narrative
{
    /// <summary>
    /// 挂在场景里的终端物体上。玩家进入范围显示提示，按 F 打开；再按 F 或 Esc 关闭。
    /// 日志内容可以直接在本组件的 Inspector 里写，也可以指定一个 NarrativeLogAsset。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NarrativeLogTerminal : MonoBehaviour
    {
        private static NarrativeLogTerminal promptOwner;

        [Header("日志内容（指定资产后优先使用资产）")]
        [Tooltip("推荐把日志写进 NarrativeLogAsset，再拖到这里。留空则使用下面的本地列表。")]
        [SerializeField] private NarrativeLogAsset logAsset;
        [Tooltip("没有指定日志资产时，使用这个标题。")]
        [SerializeField] private string terminalTitle = "遗迹终端";
        [SerializeField] private string subtitle = "ARCHIVE // 可读取记录";
        [Tooltip("没有指定日志资产时，使用这里的本地条目。")]
        [SerializeField] private List<NarrativeLogEntry> entries = new List<NarrativeLogEntry>();

        [Header("交互")]
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private LayerMask playerLayers = ~0;
        [SerializeField] private float interactionRadius = 1.8f;
        [SerializeField] private string promptText = "按 F 打开日志";

        [Header("区域显示")]
        [Tooltip("在游戏画面里显示交互范围，靠近时高亮。")]
        [SerializeField] private bool showInteractionRange = true;
        [SerializeField] private Color rangeColor = new Color(0.89f, 0.69f, 0.37f, 1f);

        [Header("引用（留空会自动查找）")]
        [SerializeField] private NarrativeTerminalUI ui;

        [Header("打开终端时冻结玩家")]
        [SerializeField] private bool freezePlayer = true;
        [SerializeField] private bool freezeRigidbody = true;
        [Tooltip("把玩家的移动/控制脚本拖进来。终端打开时会禁用它们，关闭时恢复。")]
        [SerializeField] private MonoBehaviour[] playerControlScripts;

        [Header("音频（可选）")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip openClip;
        [SerializeField] private AudioClip closeClip;
        [SerializeField] private AudioClip pageClip;

        [Header("事件（可选）")]
        public UnityEvent onOpened = new UnityEvent();
        public UnityEvent onClosed = new UnityEvent();

        private Collider2D playerCollider;
        private readonly Collider2D[] overlapBuffer = new Collider2D[16];
        private bool playerInRange;
        private bool isOpen;
        private bool isFrozen;

        private Rigidbody2D cachedRigidbody;
        private RigidbodyConstraints2D cachedConstraints;
        private MonoBehaviour[] cachedControlScripts;
        private bool[] cachedControlStates;
        private NarrativeInteractionRangeVisual rangeVisual;

        public bool IsOpen
        {
            get { return isOpen; }
        }

        public bool PlayerInRange
        {
            get { return playerInRange; }
        }

        public float InteractionRadius
        {
            get { return interactionRadius; }
        }

        private void Start()
        {
            ResolveUI();
            if (ui != null)
            {
                ui.CloseRequested += HandleUICloseRequested;
            }
            else
            {
                Debug.LogError(
                    "[NarrativeLogTerminal] 场景里找不到 NarrativeTerminalUI。" +
                    "请先用菜单 工具 > 创优工坊 > 剧情日志终端 > 创建终端 UI。",
                    this);
            }

            EnsureRangeVisual();
        }

        private void Update()
        {
            if (ui == null)
            {
                ResolveUI();
                if (ui == null)
                {
                    return;
                }

                ui.CloseRequested += HandleUICloseRequested;
            }

            // 如果 UI 被外部关闭，终端这边同步恢复玩家控制。
            if (isOpen && !ui.IsOpen)
            {
                isOpen = false;
                SetPlayerFrozen(false);
                onClosed.Invoke();
            }

            UpdateProximity();

            if (isOpen)
            {
                HidePromptIfOwner();
                HandleOpenInput();
                return;
            }

            if (playerInRange)
            {
                ShowPromptIfPossible();
                if (NarrativeTerminalInput.InteractPressed)
                {
                    Open();
                }
            }
            else
            {
                HidePromptIfOwner();
            }
        }

        private void UpdateProximity()
        {
            bool wasInRange = playerInRange;
            playerInRange = false;
            playerCollider = null;

            int count = Physics2D.OverlapCircleNonAlloc(
                transform.position,
                interactionRadius,
                overlapBuffer,
                playerLayers);

            for (int i = 0; i < count; i++)
            {
                Collider2D hit = overlapBuffer[i];
                if (hit == null)
                {
                    continue;
                }

                if (hit.CompareTag(playerTag) ||
                    (hit.attachedRigidbody != null && hit.attachedRigidbody.CompareTag(playerTag)))
                {
                    playerInRange = true;
                    playerCollider = hit;
                    break;
                }
            }

            if (wasInRange && !playerInRange)
            {
                HidePromptIfOwner();

                if (isOpen)
                {
                    Close();
                }
            }
        }

        private void ShowPromptIfPossible()
        {
            if (ui == null || ui.IsOpen)
            {
                return;
            }

            if (promptOwner != null && promptOwner != this)
            {
                return;
            }

            promptOwner = this;
            ui.ShowPrompt(promptText);
        }

        private void HidePromptIfOwner()
        {
            if (promptOwner != this)
            {
                return;
            }

            promptOwner = null;
            if (ui != null)
            {
                ui.HidePrompt();
            }
        }

        private void HandleOpenInput()
        {
            if (NarrativeTerminalInput.InteractPressed || NarrativeTerminalInput.CancelPressed)
            {
                Close();
                return;
            }

            if (NarrativeTerminalInput.PageNextPressed)
            {
                ui.NextPage();
                PlayClip(pageClip);
            }
            else if (NarrativeTerminalInput.PagePreviousPressed)
            {
                ui.PreviousPage();
                PlayClip(pageClip);
            }

            float scroll = NarrativeTerminalInput.ScrollAxis;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                ui.Scroll(scroll * 2.2f * Time.unscaledDeltaTime);
            }
        }

        public void Open()
        {
            if (isOpen || ui == null || ui.IsOpen)
            {
                return;
            }

            if (!playerInRange)
            {
                return;
            }

            HidePromptIfOwner();
            isOpen = true;
            ui.Open(logAsset, entries, terminalTitle, subtitle);
            SetPlayerFrozen(true);
            PlayClip(openClip);
            onOpened.Invoke();
        }

        public void Close()
        {
            if (!isOpen)
            {
                return;
            }

            isOpen = false;
            if (ui != null)
            {
                ui.Close();
            }

            SetPlayerFrozen(false);
            PlayClip(closeClip);
            onClosed.Invoke();
        }

        private void HandleUICloseRequested()
        {
            Close();
        }

        private void SetPlayerFrozen(bool frozen)
        {
            if (!freezePlayer)
            {
                return;
            }

            if (frozen)
            {
                if (isFrozen)
                {
                    return;
                }

                isFrozen = true;

                if (playerCollider != null)
                {
                    cachedRigidbody = playerCollider.attachedRigidbody;
                    if (cachedRigidbody == null)
                    {
                        cachedRigidbody = playerCollider.GetComponentInParent<Rigidbody2D>();
                    }
                }

                if (cachedRigidbody != null && freezeRigidbody)
                {
                    cachedConstraints = cachedRigidbody.constraints;
                    cachedRigidbody.velocity = Vector2.zero;
                    cachedRigidbody.angularVelocity = 0f;
                    cachedRigidbody.constraints = RigidbodyConstraints2D.FreezeAll;
                }

                if (playerControlScripts != null && playerControlScripts.Length > 0)
                {
                    cachedControlScripts = playerControlScripts;
                    cachedControlStates = new bool[cachedControlScripts.Length];

                    for (int i = 0; i < cachedControlScripts.Length; i++)
                    {
                        MonoBehaviour script = cachedControlScripts[i];
                        if (script == null)
                        {
                            continue;
                        }

                        cachedControlStates[i] = script.enabled;
                        script.enabled = false;
                    }
                }
            }
            else
            {
                if (!isFrozen)
                {
                    return;
                }

                isFrozen = false;

                if (cachedRigidbody != null)
                {
                    cachedRigidbody.constraints = cachedConstraints;
                    cachedRigidbody = null;
                }

                if (cachedControlScripts != null)
                {
                    for (int i = 0; i < cachedControlScripts.Length; i++)
                    {
                        MonoBehaviour script = cachedControlScripts[i];
                        if (script != null && cachedControlStates != null && i < cachedControlStates.Length)
                        {
                            script.enabled = cachedControlStates[i];
                        }
                    }
                }

                cachedControlScripts = null;
                cachedControlStates = null;
            }
        }

        private void PlayClip(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        private void EnsureRangeVisual()
        {
            if (!showInteractionRange || rangeVisual != null)
            {
                return;
            }

            GameObject visualObject = new GameObject("InteractionRange");
            visualObject.transform.SetParent(transform, false);
            visualObject.transform.localPosition = Vector3.zero;

            rangeVisual = visualObject.AddComponent<NarrativeInteractionRangeVisual>();
            rangeVisual.Initialize(this, rangeColor);
        }

        private void ResolveUI()
        {
            if (ui != null)
            {
                return;
            }

            ui = NarrativeTerminalUI.Instance;
            if (ui == null)
            {
                ui = FindObjectOfType<NarrativeTerminalUI>();
            }
        }

        private void OnDisable()
        {
            HidePromptIfOwner();

            if (isOpen)
            {
                Close();
            }
        }

        private void OnDestroy()
        {
            if (ui != null)
            {
                ui.CloseRequested -= HandleUICloseRequested;
            }

            HidePromptIfOwner();

            if (isFrozen)
            {
                SetPlayerFrozen(false);
            }
        }

        private void OnValidate()
        {
            interactionRadius = Mathf.Max(0.1f, interactionRadius);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.89f, 0.69f, 0.37f, 0.85f);
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }
    }
}
