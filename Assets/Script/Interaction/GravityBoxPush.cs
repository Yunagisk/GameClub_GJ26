using System.Collections.Generic;
using UnityEngine;

namespace GameClub.Player
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class GravityBoxPush : MonoBehaviour
    {
        private const float MinSideNormalX = 0.85f;
        private readonly List<ContactPoint2D> contacts = new List<ContactPoint2D>(8);
        private Rigidbody2D body;
        private BoxCollider2D box;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            box = GetComponent<BoxCollider2D>();
            body.constraints |= RigidbodyConstraints2D.FreezeRotation;
            SetPushAllowed(false);
        }

        private void FixedUpdate()
        {
            box.GetContacts(contacts);
            SetPushAllowed(HasSidePush());
        }

        private bool HasSidePush()
        {
            bool pushing = false;
            foreach (var contact in contacts)
            {
                var other = contact.collider == box ? contact.otherCollider : contact.collider;
                var actor = other.attachedRigidbody;
                if (!actor || !actor.TryGetComponent<PlayerInputReader>(out var input) || !input.isActiveAndEnabled)
                    continue;

                // A supporting face or an ambiguous corner takes priority over a side contact.
                if (Mathf.Abs(contact.normal.x) < MinSideNormalX) return false;
                float towardBox = box.bounds.center.x - other.bounds.center.x;
                if (input.Horizontal * towardBox > 0f) pushing = true;
            }
            return pushing;
        }

        private void SetPushAllowed(bool allowed)
        {
            var constraints = body.constraints;
            var next = allowed ? constraints & ~RigidbodyConstraints2D.FreezePositionX
                               : constraints | RigidbodyConstraints2D.FreezePositionX;
            if (next == constraints) return;
            if (!allowed) body.velocity = new Vector2(0f, body.velocity.y);
            body.constraints = next;
            body.WakeUp();
        }

        private void OnDisable()
        {
            if (body) SetPushAllowed(false);
        }
    }
}
