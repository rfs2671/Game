using UnityEngine;
#if RESONANCE_INPUTSYSTEM
using UnityEngine.InputSystem;
#endif

namespace Resonance
{
    /// <summary>Keyboard/mouse access that works with either the new Input System or legacy Input.</summary>
    public static class SimIn
    {
#if RESONANCE_INPUTSYSTEM
        static Key Map(KeyCode k) => k switch
        {
            KeyCode.W => Key.W, KeyCode.A => Key.A, KeyCode.S => Key.S, KeyCode.D => Key.D,
            KeyCode.F => Key.F, KeyCode.G => Key.G, KeyCode.R => Key.R, KeyCode.Q => Key.Q, KeyCode.E => Key.E,
            KeyCode.Space => Key.Space, KeyCode.LeftShift => Key.LeftShift, KeyCode.LeftAlt => Key.LeftAlt,
            KeyCode.Return => Key.Enter, KeyCode.N => Key.N, KeyCode.C => Key.C, KeyCode.Z => Key.Z, KeyCode.X => Key.X, KeyCode.L => Key.L, KeyCode.Escape => Key.Escape,
            _ => Key.None
        };

        public static bool Held(KeyCode k) => Keyboard.current != null && Map(k) != Key.None && Keyboard.current[Map(k)].isPressed;
        public static bool Down(KeyCode k) => Keyboard.current != null && Map(k) != Key.None && Keyboard.current[Map(k)].wasPressedThisFrame;
        public static bool LMB => Mouse.current != null && Mouse.current.leftButton.isPressed;
        public static bool RMB => Mouse.current != null && Mouse.current.rightButton.isPressed;
        public static Vector2 MousePos => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        public static Vector2 MouseDelta => Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        public static float Scroll => Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
#else
        public static bool Held(KeyCode k) => Input.GetKey(k);
        public static bool Down(KeyCode k) => Input.GetKeyDown(k);
        public static bool LMB => Input.GetMouseButton(0);
        public static bool RMB => Input.GetMouseButton(1);
        public static Vector2 MousePos => Input.mousePosition;
        public static Vector2 MouseDelta => new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 10f;
        public static float Scroll => Input.mouseScrollDelta.y * 120f;
#endif
    }
}
