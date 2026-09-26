using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SprayPatterns;

internal static class InputUtil
{
    private static readonly Dictionary<KeyCode, Key> KeyMap = new()
    {
        { KeyCode.A, Key.A }, { KeyCode.B, Key.B }, { KeyCode.C, Key.C }, { KeyCode.D, Key.D },
        { KeyCode.E, Key.E }, { KeyCode.F, Key.F }, { KeyCode.G, Key.G }, { KeyCode.H, Key.H },
        { KeyCode.I, Key.I }, { KeyCode.J, Key.J }, { KeyCode.K, Key.K }, { KeyCode.L, Key.L },
        { KeyCode.M, Key.M }, { KeyCode.N, Key.N }, { KeyCode.O, Key.O }, { KeyCode.P, Key.P },
        { KeyCode.Q, Key.Q }, { KeyCode.R, Key.R }, { KeyCode.S, Key.S }, { KeyCode.T, Key.T },
        { KeyCode.U, Key.U }, { KeyCode.V, Key.V }, { KeyCode.W, Key.W }, { KeyCode.X, Key.X },
        { KeyCode.Y, Key.Y }, { KeyCode.Z, Key.Z },
        { KeyCode.Alpha0, Key.Digit0 }, { KeyCode.Alpha1, Key.Digit1 }, { KeyCode.Alpha2, Key.Digit2 },
        { KeyCode.Alpha3, Key.Digit3 }, { KeyCode.Alpha4, Key.Digit4 }, { KeyCode.Alpha5, Key.Digit5 },
        { KeyCode.Alpha6, Key.Digit6 }, { KeyCode.Alpha7, Key.Digit7 }, { KeyCode.Alpha8, Key.Digit8 },
        { KeyCode.Alpha9, Key.Digit9 },
        { KeyCode.Space, Key.Space }, { KeyCode.LeftShift, Key.LeftShift }, { KeyCode.RightShift, Key.RightShift },
        { KeyCode.LeftControl, Key.LeftCtrl }, { KeyCode.RightControl, Key.RightCtrl },
        { KeyCode.LeftAlt, Key.LeftAlt }, { KeyCode.RightAlt, Key.RightAlt },
        { KeyCode.Tab, Key.Tab }, { KeyCode.CapsLock, Key.CapsLock },
        { KeyCode.Backspace, Key.Backspace }, { KeyCode.Return, Key.Enter }, { KeyCode.Escape, Key.Escape },
        { KeyCode.UpArrow, Key.UpArrow }, { KeyCode.DownArrow, Key.DownArrow },
        { KeyCode.LeftArrow, Key.LeftArrow }, { KeyCode.RightArrow, Key.RightArrow },
        { KeyCode.LeftBracket, Key.LeftBracket }, { KeyCode.RightBracket, Key.RightBracket },
        { KeyCode.Comma, Key.Comma }, { KeyCode.Period, Key.Period },
        { KeyCode.Slash, Key.Slash }, { KeyCode.Semicolon, Key.Semicolon },
        { KeyCode.Quote, Key.Quote }, { KeyCode.BackQuote, Key.Backquote },
        { KeyCode.Minus, Key.Minus }, { KeyCode.Equals, Key.Equals },
        { KeyCode.F1, Key.F1 }, { KeyCode.F2, Key.F2 }, { KeyCode.F3, Key.F3 }, { KeyCode.F4, Key.F4 },
        { KeyCode.F5, Key.F5 }, { KeyCode.F6, Key.F6 }, { KeyCode.F7, Key.F7 }, { KeyCode.F8, Key.F8 },
        { KeyCode.F9, Key.F9 }, { KeyCode.F10, Key.F10 }, { KeyCode.F11, Key.F11 }, { KeyCode.F12, Key.F12 },
    };

    private static readonly Dictionary<KeyCode, bool> WasDown = new();
    private static readonly Dictionary<KeyCode, int> EdgeFrame = new();
    private static readonly Dictionary<KeyCode, bool> EdgeResult = new();

    public static bool WasPressedThisFrame(KeyCode keyCode)
    {
        var frame = Time.frameCount;
        if (EdgeFrame.TryGetValue(keyCode, out var cachedFrame) && cachedFrame == frame)
            return EdgeResult.TryGetValue(keyCode, out var cached) && cached;

        EdgeFrame[keyCode] = frame;

        try
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && KeyMap.TryGetValue(keyCode, out var key))
            {
                KeyControl control = keyboard[key];
                if (control != null && control.wasPressedThisFrame)
                {
                    EdgeResult[keyCode] = true;
                    WasDown[keyCode] = true;
                    return true;
                }
            }
        }
        catch
        {
            // fall through
        }

        try
        {
            if (Input.GetKeyDown(keyCode))
            {
                EdgeResult[keyCode] = true;
                WasDown[keyCode] = true;
                return true;
            }
        }
        catch
        {
            // fall through
        }

        var down = IsDown(keyCode);
        var prev = WasDown.TryGetValue(keyCode, out var was) && was;
        var edge = down && !prev;
        EdgeResult[keyCode] = edge;
        WasDown[keyCode] = down;
        return edge;
    }

    public static bool IsDown(KeyCode keyCode)
    {
        try
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && KeyMap.TryGetValue(keyCode, out var key))
            {
                KeyControl control = keyboard[key];
                if (control != null && control.isPressed)
                    return true;
            }
        }
        catch
        {
            // ignored
        }

        try
        {
            if (Input.GetKey(keyCode))
                return true;
        }
        catch
        {
            // ignored
        }

        return false;
    }

    public static float ScrollYThisFrame()
    {
        try
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                var v = mouse.scroll.ReadValue();
                if (Mathf.Abs(v.y) > 0.01f)
                    return v.y;
            }
        }
        catch
        {
            // fall through
        }

        try
        {
            return Input.mouseScrollDelta.y;
        }
        catch
        {
            return 0f;
        }
    }

    public static bool LeftClickPressedThisFrame()
    {
        try
        {
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                return true;
        }
        catch
        {
            // fall through
        }

        try
        {
            return Input.GetMouseButtonDown(0);
        }
        catch
        {
            return false;
        }
    }

    public static Vector2 MouseScreenPosition()
    {
        try
        {
            var mouse = Mouse.current;
            if (mouse != null)
                return mouse.position.ReadValue();
        }
        catch
        {
            // fall through
        }

        return Input.mousePosition;
    }

    public static string TipLabel(KeyCode keyCode) => keyCode switch
    {
        KeyCode.LeftShift or KeyCode.RightShift => "Shift",
        KeyCode.LeftControl or KeyCode.RightControl => "Ctrl",
        KeyCode.LeftAlt or KeyCode.RightAlt => "Alt",
        KeyCode.Return => "Enter",
        KeyCode.Escape => "Esc",
        KeyCode.LeftBracket => "[",
        KeyCode.RightBracket => "]",
        _ when keyCode >= KeyCode.Alpha0 && keyCode <= KeyCode.Alpha9
            => ((char)('0' + (keyCode - KeyCode.Alpha0))).ToString(),
        _ => keyCode.ToString(),
    };
}
