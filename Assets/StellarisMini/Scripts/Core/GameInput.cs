using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace StellarisMini
{
    /// Boutons de manette (disposition Xbox).
    public enum Pad { A, B, X, Y, LB, RB, Start, View, DUp, DDown, DLeft, DRight, LStick, RStick }

    /// Touches clavier utilisées par le jeu.
    public enum KeyId { W, A, S, D, Q, E, R, F, Z, M, H, Up, Down, Left, Right, Space, Escape, Tab, Enter, Shift, Alpha1, Alpha2, Alpha3, Plus, Minus, F1 }

    /// Couche d'abstraction des entrées : clavier + souris et manette Xbox.
    /// Utilise le nouveau "Input System" s'il est activé, sinon l'ancien Input Manager (sans manette).
    public static class GameInput
    {
        public static bool UsingGamepad { get; private set; }
        public static bool GamepadConnected { get; private set; }
        public static Vector2 LeftStick { get; private set; }
        public static Vector2 RightStick { get; private set; }
        public static float LeftTrigger { get; private set; }
        public static float RightTrigger { get; private set; }
        public static Vector2 MousePosition { get; private set; }   // pixels, origine en bas à gauche
        public static Vector2 MouseDelta { get; private set; }
        public static float Scroll { get; private set; }            // -1, 0 ou +1
        public static Vector2Int NavDir { get; private set; }       // impulsions de navigation (menus)

        static Vector2 lastMouse;
        static bool firstFrame = true;
        static Vector2Int lastNavDir;
        static float navTimer;
        static float rumbleTime;
        static float prevLT, prevRT;
        static bool ltDown, rtDown;

        public static void Update()
        {
            float dt = Time.unscaledDeltaTime;
#if ENABLE_INPUT_SYSTEM
            var pad = Gamepad.current;
            GamepadConnected = pad != null;
            if (pad != null)
            {
                LeftStick = Deadzone(pad.leftStick.ReadValue());
                RightStick = Deadzone(pad.rightStick.ReadValue());
                LeftTrigger = pad.leftTrigger.ReadValue();
                RightTrigger = pad.rightTrigger.ReadValue();
                if (LeftStick.sqrMagnitude > 0.16f || RightStick.sqrMagnitude > 0.16f || LeftTrigger > 0.4f || RightTrigger > 0.4f || AnyPadButton(pad))
                    UsingGamepad = true;
            }
            else
            {
                LeftStick = RightStick = Vector2.zero;
                LeftTrigger = RightTrigger = 0;
                UsingGamepad = false;
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 mp = mouse.position.ReadValue();
                MouseDelta = firstFrame ? Vector2.zero : mp - lastMouse;
                lastMouse = mp;
                MousePosition = mp;
                float sy = mouse.scroll.ReadValue().y;
                Scroll = sy > 0.01f ? 1f : sy < -0.01f ? -1f : 0f;
                if (MouseDelta.sqrMagnitude > 16f || mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || Scroll != 0)
                    UsingGamepad = false;
            }
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame) UsingGamepad = false;
#else
            GamepadConnected = false;
            UsingGamepad = false;
            LeftStick = RightStick = Vector2.zero;
            LeftTrigger = RightTrigger = 0;
            Vector2 mp = Input.mousePosition;
            MouseDelta = firstFrame ? Vector2.zero : mp - lastMouse;
            lastMouse = mp;
            MousePosition = mp;
            float sy = Input.mouseScrollDelta.y;
            Scroll = sy > 0.01f ? 1f : sy < -0.01f ? -1f : 0f;
#endif
            firstFrame = false;

            // Gâchettes utilisées comme boutons
            ltDown = LeftTrigger > 0.5f && prevLT <= 0.5f;
            rtDown = RightTrigger > 0.5f && prevRT <= 0.5f;
            prevLT = LeftTrigger;
            prevRT = RightTrigger;

            // Navigation dans les menus (croix directionnelle ou stick gauche, avec répétition)
            var dir = Vector2Int.zero;
            if (PadHeld(Pad.DUp)) dir.y = 1;
            else if (PadHeld(Pad.DDown)) dir.y = -1;
            else if (PadHeld(Pad.DLeft)) dir.x = -1;
            else if (PadHeld(Pad.DRight)) dir.x = 1;
            else if (LeftStick.magnitude > 0.6f)
            {
                if (Mathf.Abs(LeftStick.y) >= Mathf.Abs(LeftStick.x)) dir.y = LeftStick.y > 0 ? 1 : -1;
                else dir.x = LeftStick.x > 0 ? 1 : -1;
            }
            if (dir != Vector2Int.zero)
            {
                if (dir != lastNavDir) { NavDir = dir; navTimer = 0.38f; }
                else
                {
                    navTimer -= dt;
                    if (navTimer <= 0) { NavDir = dir; navTimer = 0.11f; }
                    else NavDir = Vector2Int.zero;
                }
            }
            else NavDir = Vector2Int.zero;
            lastNavDir = dir;

            // Vibrations
            if (rumbleTime > 0)
            {
                rumbleTime -= dt;
                if (rumbleTime <= 0) StopRumble();
            }
        }

        static Vector2 Deadzone(Vector2 v)
        {
            float m = v.magnitude;
            if (m < 0.18f) return Vector2.zero;
            return v / m * Mathf.Clamp01((m - 0.18f) / 0.77f);
        }

        // ------------------------------------------------------------------
        //  Manette
        // ------------------------------------------------------------------
#if ENABLE_INPUT_SYSTEM
        static ButtonControl Btn(Gamepad g, Pad p)
        {
            switch (p)
            {
                case Pad.A: return g.buttonSouth;
                case Pad.B: return g.buttonEast;
                case Pad.X: return g.buttonWest;
                case Pad.Y: return g.buttonNorth;
                case Pad.LB: return g.leftShoulder;
                case Pad.RB: return g.rightShoulder;
                case Pad.Start: return g.startButton;
                case Pad.View: return g.selectButton;
                case Pad.DUp: return g.dpad.up;
                case Pad.DDown: return g.dpad.down;
                case Pad.DLeft: return g.dpad.left;
                case Pad.DRight: return g.dpad.right;
                case Pad.LStick: return g.leftStickButton;
                default: return g.rightStickButton;
            }
        }

        static bool AnyPadButton(Gamepad g)
        {
            for (int i = 0; i <= (int)Pad.RStick; i++)
                if (Btn(g, (Pad)i).wasPressedThisFrame) return true;
            return false;
        }
#endif

        public static bool PadDown(Pad p)
        {
#if ENABLE_INPUT_SYSTEM
            var g = Gamepad.current;
            return g != null && Btn(g, p).wasPressedThisFrame;
#else
            return false;
#endif
        }

        public static bool PadHeld(Pad p)
        {
#if ENABLE_INPUT_SYSTEM
            var g = Gamepad.current;
            return g != null && Btn(g, p).isPressed;
#else
            return false;
#endif
        }

        public static bool PadUp(Pad p)
        {
#if ENABLE_INPUT_SYSTEM
            var g = Gamepad.current;
            return g != null && Btn(g, p).wasReleasedThisFrame;
#else
            return false;
#endif
        }

        public static bool LeftTriggerDown { get { return ltDown; } }
        public static bool RightTriggerDown { get { return rtDown; } }

        // ------------------------------------------------------------------
        //  Clavier
        // ------------------------------------------------------------------
#if ENABLE_INPUT_SYSTEM
        static KeyControl Key1(Keyboard k, KeyId id)
        {
            switch (id)
            {
                case KeyId.W: return k.wKey;
                case KeyId.A: return k.aKey;
                case KeyId.S: return k.sKey;
                case KeyId.D: return k.dKey;
                case KeyId.Q: return k.qKey;
                case KeyId.E: return k.eKey;
                case KeyId.R: return k.rKey;
                case KeyId.F: return k.fKey;
                case KeyId.Z: return k.zKey;
                case KeyId.M: return k.mKey;
                case KeyId.H: return k.hKey;
                case KeyId.Up: return k.upArrowKey;
                case KeyId.Down: return k.downArrowKey;
                case KeyId.Left: return k.leftArrowKey;
                case KeyId.Right: return k.rightArrowKey;
                case KeyId.Space: return k.spaceKey;
                case KeyId.Escape: return k.escapeKey;
                case KeyId.Tab: return k.tabKey;
                case KeyId.Enter: return k.enterKey;
                case KeyId.Shift: return k.leftShiftKey;
                case KeyId.Alpha1: return k.digit1Key;
                case KeyId.Alpha2: return k.digit2Key;
                case KeyId.Alpha3: return k.digit3Key;
                case KeyId.Plus: return k.numpadPlusKey;
                case KeyId.Minus: return k.numpadMinusKey;
                default: return k.f1Key;
            }
        }

        static KeyControl Key2(Keyboard k, KeyId id)
        {
            switch (id)
            {
                case KeyId.Enter: return k.numpadEnterKey;
                case KeyId.Shift: return k.rightShiftKey;
                case KeyId.Plus: return k.equalsKey;
                case KeyId.Minus: return k.minusKey;
                case KeyId.Alpha1: return k.numpad1Key;
                case KeyId.Alpha2: return k.numpad2Key;
                case KeyId.Alpha3: return k.numpad3Key;
                default: return null;
            }
        }
#else
        static KeyCode Code1(KeyId id)
        {
            switch (id)
            {
                case KeyId.W: return KeyCode.W;
                case KeyId.A: return KeyCode.A;
                case KeyId.S: return KeyCode.S;
                case KeyId.D: return KeyCode.D;
                case KeyId.Q: return KeyCode.Q;
                case KeyId.E: return KeyCode.E;
                case KeyId.R: return KeyCode.R;
                case KeyId.F: return KeyCode.F;
                case KeyId.Z: return KeyCode.Z;
                case KeyId.M: return KeyCode.M;
                case KeyId.H: return KeyCode.H;
                case KeyId.Up: return KeyCode.UpArrow;
                case KeyId.Down: return KeyCode.DownArrow;
                case KeyId.Left: return KeyCode.LeftArrow;
                case KeyId.Right: return KeyCode.RightArrow;
                case KeyId.Space: return KeyCode.Space;
                case KeyId.Escape: return KeyCode.Escape;
                case KeyId.Tab: return KeyCode.Tab;
                case KeyId.Enter: return KeyCode.Return;
                case KeyId.Shift: return KeyCode.LeftShift;
                case KeyId.Alpha1: return KeyCode.Alpha1;
                case KeyId.Alpha2: return KeyCode.Alpha2;
                case KeyId.Alpha3: return KeyCode.Alpha3;
                case KeyId.Plus: return KeyCode.KeypadPlus;
                case KeyId.Minus: return KeyCode.KeypadMinus;
                default: return KeyCode.F1;
            }
        }

        static KeyCode Code2(KeyId id)
        {
            switch (id)
            {
                case KeyId.Enter: return KeyCode.KeypadEnter;
                case KeyId.Shift: return KeyCode.RightShift;
                case KeyId.Plus: return KeyCode.Equals;
                case KeyId.Minus: return KeyCode.Minus;
                case KeyId.Alpha1: return KeyCode.Keypad1;
                case KeyId.Alpha2: return KeyCode.Keypad2;
                case KeyId.Alpha3: return KeyCode.Keypad3;
                default: return KeyCode.None;
            }
        }
#endif

        public static bool KeyDown(KeyId id)
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return false;
            var a = Key1(k, id);
            var b = Key2(k, id);
            return (a != null && a.wasPressedThisFrame) || (b != null && b.wasPressedThisFrame);
#else
            var b = Code2(id);
            return Input.GetKeyDown(Code1(id)) || (b != KeyCode.None && Input.GetKeyDown(b));
#endif
        }

        public static bool KeyHeld(KeyId id)
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return false;
            var a = Key1(k, id);
            var b = Key2(k, id);
            return (a != null && a.isPressed) || (b != null && b.isPressed);
#else
            var b = Code2(id);
            return Input.GetKey(Code1(id)) || (b != KeyCode.None && Input.GetKey(b));
#endif
        }

        // ------------------------------------------------------------------
        //  Souris (0 = gauche, 1 = droite, 2 = milieu)
        // ------------------------------------------------------------------
#if ENABLE_INPUT_SYSTEM
        static ButtonControl MouseBtn(Mouse m, int b)
        {
            return b == 0 ? m.leftButton : b == 1 ? m.rightButton : m.middleButton;
        }
#endif

        public static bool MouseDown(int b)
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            return m != null && MouseBtn(m, b).wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(b);
#endif
        }

        public static bool MouseHeld(int b)
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            return m != null && MouseBtn(m, b).isPressed;
#else
            return Input.GetMouseButton(b);
#endif
        }

        public static bool MouseUp(int b)
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            return m != null && MouseBtn(m, b).wasReleasedThisFrame;
#else
            return Input.GetMouseButtonUp(b);
#endif
        }

        // ------------------------------------------------------------------
        //  Raccourcis de haut niveau
        // ------------------------------------------------------------------
        /// Déplacement (stick gauche ou ZQSD / WASD / flèches).
        /// Avec l'Input System, les touches sont repérées par leur position physique :
        /// W/A/S/D correspondent donc automatiquement à Z/Q/S/D sur un clavier AZERTY.
        public static Vector2 Move
        {
            get
            {
                var v = LeftStick;
#if ENABLE_INPUT_SYSTEM
                bool up = KeyHeld(KeyId.W), left = KeyHeld(KeyId.A);
#else
                bool up = KeyHeld(KeyId.W) || KeyHeld(KeyId.Z), left = KeyHeld(KeyId.A) || KeyHeld(KeyId.Q);
#endif
                if (up || KeyHeld(KeyId.Up)) v.y += 1;
                if (KeyHeld(KeyId.S) || KeyHeld(KeyId.Down)) v.y -= 1;
                if (KeyHeld(KeyId.D) || KeyHeld(KeyId.Right)) v.x += 1;
                if (left || KeyHeld(KeyId.Left)) v.x -= 1;
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        public static bool Confirm { get { return PadDown(Pad.A) || KeyDown(KeyId.Enter); } }
        public static bool Back { get { return PadDown(Pad.B) || KeyDown(KeyId.Escape); } }

        /// Replace le curseur au centre de l'écran (pilotage 3D à la souris).
        public static void CenterMouse()
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            if (m != null && !UsingGamepad) m.WarpCursorPosition(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
#endif
        }

        // ------------------------------------------------------------------
        //  Vibrations
        // ------------------------------------------------------------------
        public static void Rumble(float low, float high, float duration)
        {
#if ENABLE_INPUT_SYSTEM
            var g = Gamepad.current;
            if (g == null || !UsingGamepad) return;
            g.SetMotorSpeeds(Mathf.Clamp01(low), Mathf.Clamp01(high));
            rumbleTime = Mathf.Max(rumbleTime, duration);
#endif
        }

        public static void StopRumble()
        {
            rumbleTime = 0;
#if ENABLE_INPUT_SYSTEM
            var g = Gamepad.current;
            if (g != null) g.ResetHaptics();
#endif
        }

        /// Libellé d'un bouton selon le périphérique utilisé.
        public static string Hint(string pad, string keyboard)
        {
            return UsingGamepad ? "<b>[" + pad + "]</b>" : "<b>[" + keyboard + "]</b>";
        }
    }
}
