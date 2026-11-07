using UnityEngine;
using TMPro;

public class BlinkText : MonoBehaviour
{
    [Header("闪烁设置")]
    public float blinkSpeed = 2.0f; // 闪烁速度
    public float minAlpha = 0.2f;   // 最小透明度
    public float maxAlpha = 1.0f;   // 最大透明度

    [SerializeField]private TextMeshProUGUI textComponent;

    private void Start()
    {
        textComponent = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        // 使用 Mathf.Sin 产生 -1 到 1 的平滑波动，再映射到 minAlpha 和 maxAlpha 之间
        // (Mathf.Sin(Time.time * blinkSpeed) + 1f) / 2f 会将值映射到 0 到 1
        float alpha = Mathf.Lerp(minAlpha, maxAlpha, (Mathf.Sin(Time.time * blinkSpeed) + 1f) / 2f);

        Color color = textComponent.color;
        color.a = alpha;
        textComponent.color = color;
    }
}