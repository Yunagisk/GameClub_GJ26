using UnityEngine;

namespace GameClub.Player
{
    [DisallowMultipleComponent]
    public sealed class GravitySelectionMarker : MonoBehaviour
    {
        [SerializeField] private Sprite markerSprite;
        private SpriteRenderer icon;
        private Behaviour target;
        private Collider2D anchor;

        public void Show(Behaviour selected, Collider2D hit)
        {
            if (!icon)
            {
                var visual = new GameObject("Gravity selection marker");
                visual.transform.SetParent(transform, false);
                icon = visual.AddComponent<SpriteRenderer>();
                icon.sprite = markerSprite;
                icon.sortingOrder = 100;
            }
            target = selected;
            anchor = hit;
            LateUpdate();
        }

        public void Hide()
        {
            target = null;
            anchor = null;
            if (icon) icon.enabled = false;
        }

        private void LateUpdate()
        {
            if (!icon) return;
            if (!target || !target.isActiveAndEnabled)
            {
                Hide();
                return;
            }
            Vector3 position = target.transform.position;
            if (anchor && anchor.isActiveAndEnabled)
            {
                var bounds = anchor.bounds;
                position = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            }
            icon.transform.position = position + Vector3.up * 0.3f;
            icon.enabled = true;
        }
    }
}
