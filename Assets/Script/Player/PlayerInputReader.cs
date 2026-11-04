using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameClub.Player
{
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerInput))]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        private InputAction move, jump, dash, pointer, interact, select, exchange;
        private bool jumpPressed, dashPressed;
        private int dashAxis;
        private bool gameplayEnabled = true;

        public void SetGameplayEnabled(bool value)
        {
            gameplayEnabled = value;
            jumpPressed = dashPressed = false;
        }

        public event Action InteractPressed;
        public event Action SelectPressed;
        public event Action ExchangePressed;
        public Vector2 PointerPosition => pointer == null ? Vector2.zero : pointer.ReadValue<Vector2>();

        public int Horizontal => !gameplayEnabled || move == null ? 0 : System.Math.Sign(move.ReadValue<float>());
        public bool JumpHeld => gameplayEnabled && jump != null && jump.IsPressed();

        private void OnEnable()
        {
            var actions = GetComponent<PlayerInput>().actions;
            if (!actions)
            {
                Debug.LogError("Assign PlayerControls to PlayerInput and select the Gameplay map.", this);
                enabled = false;
                return;
            }
            move = actions.FindAction("Gameplay/Move", true);
            jump = actions.FindAction("Gameplay/Jump", true);
            dash = actions.FindAction("Gameplay/Dash", true);
            pointer = actions.FindAction("Gameplay/Point", true);
            interact = actions.FindAction("Gameplay/Interact", true);
            select = actions.FindAction("Gameplay/SelectGravityTarget", true);
            exchange = actions.FindAction("Gameplay/ExchangeGravity", true);
            jump.performed += OnJump;
            dash.performed += OnDash;
            interact.performed += OnInteract;
            select.performed += OnSelect;
            exchange.performed += OnExchange;
        }

        private void OnJump(InputAction.CallbackContext context) { if (gameplayEnabled) jumpPressed = true; }
        private void OnInteract(InputAction.CallbackContext context) { if (gameplayEnabled) InteractPressed?.Invoke(); }
        private void OnSelect(InputAction.CallbackContext context) { if (gameplayEnabled) SelectPressed?.Invoke(); }
        private void OnExchange(InputAction.CallbackContext context) { if (gameplayEnabled) ExchangePressed?.Invoke(); }
        private void OnDash(InputAction.CallbackContext context)
        {
            if (!gameplayEnabled) return;
            dashAxis = Horizontal;
            dashPressed = true;
        }

        public void Consume(out bool wantsJump, out bool wantsDash, out int axis)
        {
            wantsJump = jumpPressed;
            wantsDash = dashPressed;
            axis = dashAxis;
            jumpPressed = dashPressed = false;
        }

        private void OnDisable()
        {
            if (jump != null) jump.performed -= OnJump;
            if (dash != null) dash.performed -= OnDash;
            if (interact != null) interact.performed -= OnInteract;
            if (select != null) select.performed -= OnSelect;
            if (exchange != null) exchange.performed -= OnExchange;
            jumpPressed = dashPressed = false;
        }
    }
}
