using UnityEngine;

namespace GameClub.Player
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
    public sealed class DoorController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private Color closedColor = new Color(0.65f, 0.3f, 0.16f);
        [SerializeField] private Color openColor = new Color(0.3f, 0.85f, 0.6f, 0.2f);
        public bool IsOpen { get; private set; }

        public void SetOpen(bool open)
        {
            IsOpen = open;
            GetComponent<BoxCollider2D>().enabled = !open;

            if (!visual) visual = GetComponent<SpriteRenderer>();

            if (visual) visual.color = open ? openColor : closedColor;
        }
    }
}
