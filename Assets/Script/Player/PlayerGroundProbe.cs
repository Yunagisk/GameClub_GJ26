using System.Collections.Generic;
using UnityEngine;

namespace GameClub.Player
{
    [DisallowMultipleComponent, RequireComponent(typeof(GravityBody2D))]
    public sealed class PlayerGroundProbe : MonoBehaviour
    {
        [SerializeField] private Collider2D playerCollider;
        [SerializeField] private LayerMask groundLayers = Physics2D.DefaultRaycastLayers;
        [Min(0.001f)] public float groundCheckDistance = 0.06f;
        public Vector2 groundCheckSize = new Vector2(0.65f, 0.06f);
        public bool isGrounded { get; private set; }
        private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>(8);
        private Rigidbody2D body;
        private GravityBody2D gravityBody;

        private void Awake()
        {
            gravityBody = GetComponent<GravityBody2D>();
            body = GetComponent<Rigidbody2D>();
            if (!playerCollider) playerCollider = GetComponent<Collider2D>();
        }

        public void Refresh()
        {
            Vector2 down = gravityBody.gravityDir;
            isGrounded = false;
            if (Vector2.Dot(body.velocity, down) < -0.05f) return;

            Bounds bounds = playerCollider.bounds;
            // Begin inside the player's feet so a resting floor has a real surface normal.
            float inset = groundCheckSize.y + 0.01f;
            Vector2 origin = (Vector2)bounds.center + down * (bounds.extents.y - inset - groundCheckSize.y * 0.5f);
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(groundLayers);
            Physics2D.BoxCast(origin, groundCheckSize, 0f, down, filter, hits, inset + groundCheckDistance);
            foreach (var hit in hits)
            {
                if (hit.rigidbody == body || hit.distance <= 0f || Vector2.Dot(hit.normal, -down) < 0.65f) continue;
                isGrounded = true;
                break;
            }
        }

        public void LeaveGround() => isGrounded = false;
    }
}
