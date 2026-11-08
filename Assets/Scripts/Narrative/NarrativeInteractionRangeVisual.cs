using UnityEngine;

namespace Chuangyou.Narrative
{
    /// <summary>
    /// 在游戏画面里显示终端的交互范围。
    /// 由 NarrativeLogTerminal 自动创建，不需要手动挂到场景物体上。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NarrativeInteractionRangeVisual : MonoBehaviour
    {
        private static Sprite sharedRingSprite;

        [Header("显示")]
        [SerializeField] private Color rangeColor = new Color(0.89f, 0.69f, 0.37f, 1f);
        [SerializeField] private float idleAlpha = 0f;
        [SerializeField] private float inRangeAlpha = 0.58f;
        [SerializeField] private float openAlpha = 0f;
        [SerializeField] private float fadeSpeed = 7f;

        [Header("靠近时呼吸")]
        [SerializeField] private float pulseAmplitude = 0.05f;
        [SerializeField] private float pulseSpeed = 3.2f;

        private NarrativeLogTerminal terminal;
        private SpriteRenderer spriteRenderer;
        private float currentAlpha;
        private float alphaScale = 1f;

        /// <summary>由终端在运行时调用。</summary>
        public void Initialize(NarrativeLogTerminal owner, Color color)
        {
            terminal = owner;
            rangeColor = color;
            alphaScale = Mathf.Clamp01(color.a);
            EnsureRenderer();

            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            currentAlpha = 0f;
            ApplyAlpha(0f);

            if (terminal != null)
            {
                CacheSorting();
                UpdateScale();
            }
        }

        private void Awake()
        {
            EnsureRenderer();
        }

        private void LateUpdate()
        {
            if (terminal == null)
            {
                Destroy(gameObject);
                return;
            }

            UpdateScale();
            CacheSorting();

            float targetAlpha = !terminal.isActiveAndEnabled
                ? 0f
                : (terminal.IsOpen
                    ? openAlpha
                    : (terminal.PlayerInRange ? inRangeAlpha : idleAlpha));

            if (terminal.isActiveAndEnabled && terminal.PlayerInRange && !terminal.IsOpen)
            {
                targetAlpha += Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmplitude;
            }

            targetAlpha = Mathf.Clamp01(targetAlpha);
            currentAlpha = Mathf.MoveTowards(
                currentAlpha,
                targetAlpha,
                fadeSpeed * Time.unscaledDeltaTime);

            ApplyAlpha(currentAlpha);
        }

        private void EnsureRenderer()
        {
            if (spriteRenderer != null)
            {
                return;
            }

            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            spriteRenderer.sprite = GetSharedRingSprite();
            spriteRenderer.color = new Color(rangeColor.r, rangeColor.g, rangeColor.b, 0f);
            spriteRenderer.drawMode = SpriteDrawMode.Simple;
        }

        private void UpdateScale()
        {
            float diameter = Mathf.Max(0.1f, terminal.InteractionRadius * 2f);
            Vector3 parentScale = transform.parent != null
                ? transform.parent.lossyScale
                : Vector3.one;

            transform.localScale = new Vector3(
                diameter / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                diameter / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
                1f);
        }

        private void CacheSorting()
        {
            if (spriteRenderer == null || terminal == null)
            {
                return;
            }

            SpriteRenderer terminalRenderer = terminal.GetComponent<SpriteRenderer>();
            if (terminalRenderer == null)
            {
                return;
            }

            spriteRenderer.sortingLayerID = terminalRenderer.sortingLayerID;
            spriteRenderer.sortingOrder = terminalRenderer.sortingOrder - 1;
        }

        private void ApplyAlpha(float alpha)
        {
            if (spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.color = new Color(
                rangeColor.r,
                rangeColor.g,
                rangeColor.b,
                Mathf.Clamp01(alpha * alphaScale));
        }

        private static Sprite GetSharedRingSprite()
        {
            if (sharedRingSprite != null)
            {
                return sharedRingSprite;
            }

            const int size = 256;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "NarrativeInteractionRangeTexture";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Color32[] pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy) / center;

                    // 外圈柔边 + 内部很淡的填充，既能看清范围，又不会挡住场景。
                    float ring = SmoothStep(0.90f, 0.955f, distance)
                                 * (1f - SmoothStep(0.982f, 1f, distance));
                    float fill = (1f - SmoothStep(0.0f, 0.96f, distance)) * 0.09f;
                    float alpha = Mathf.Max(ring, fill);

                    pixels[y * size + x] = new Color32(
                        255,
                        255,
                        255,
                        (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            sharedRingSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                size);

            sharedRingSprite.name = "NarrativeInteractionRangeRing";
            return sharedRingSprite;
        }

        private static float SmoothStep(float edge0, float edge1, float value)
        {
            float t = Mathf.Clamp01((value - edge0) / Mathf.Max(0.0001f, edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }
}
