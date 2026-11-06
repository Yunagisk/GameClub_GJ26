using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Chuangyou.Narrative
{
    /// <summary>
    /// 同时兼容旧 Input Manager 和新 Input System 的输入读取。
    /// F / Esc 是终端交互，A/D 或左右方向键翻页，W/S 或上下方向键滚动正文。
    /// </summary>
    public static class NarrativeTerminalInput
    {
        public static bool InteractPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
                {
                    return true;
                }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKeyDown(KeyCode.F))
                {
                    return true;
                }
#endif
                return false;
            }
        }

        public static bool CancelPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    return true;
                }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    return true;
                }
#endif
                return false;
            }
        }

        public static bool PageNextPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null &&
                    (Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame))
                {
                    return true;
                }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
                {
                    return true;
                }
#endif
                return false;
            }
        }

        public static bool PagePreviousPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null &&
                    (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame))
                {
                    return true;
                }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    return true;
                }
#endif
                return false;
            }
        }

        /// <summary>正数向上滚，负数向下滚。</summary>
        public static float ScrollAxis
        {
            get
            {
                float axis = 0f;
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                    {
                        axis += 1f;
                    }

                    if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                    {
                        axis -= 1f;
                    }
                }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
                {
                    axis += 1f;
                }

                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
                {
                    axis -= 1f;
                }
#endif
                return axis;
            }
        }

        /// <summary>横向输入，供演示角色使用。</summary>
        public static float HorizontalAxis
        {
            get
            {
                float axis = 0f;
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                    {
                        axis -= 1f;
                    }

                    if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                    {
                        axis += 1f;
                    }
                }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                {
                    axis -= 1f;
                }

                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                {
                    axis += 1f;
                }
#endif
                return Mathf.Clamp(axis, -1f, 1f);
            }
        }

        public static bool JumpPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    return true;
                }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    return true;
                }
#endif
                return false;
            }
        }
    }
}
