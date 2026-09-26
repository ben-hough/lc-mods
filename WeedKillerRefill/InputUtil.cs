using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace WeedKillerRefill;

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
        { KeyCode.Insert, Key.Insert }, { KeyCode.Delete, Key.Delete },
        { KeyCode.Home, Key.Home }, { KeyCode.End, Key.End },
        { KeyCode.PageUp, Key.PageUp }, { KeyCode.PageDown, Key.PageDown },
        { KeyCode.F1, Key.F1 }, { KeyCode.F2, Key.F2 }, { KeyCode.F3, Key.F3 }, { KeyCode.F4, Key.F4 },
        { KeyCode.F5, Key.F5 }, { KeyCode.F6, Key.F6 }, { KeyCode.F7, Key.F7 }, { KeyCode.F8, Key.F8 },
        { KeyCode.F9, Key.F9 }, { KeyCode.F10, Key.F10 }, { KeyCode.F11, Key.F11 }, { KeyCode.F12, Key.F12 },
    };

    private static bool _wasDown;
    private static int _edgeFrame = -1;
    private static bool _edgeResult;

    public static bool WasPressedThisFrame(KeyCode keyCode)
    {
        // Cache per frame so tip + LateUpdate callers see the same edge.
        var frame = Time.frameCount;
        if (frame == _edgeFrame)
            return _edgeResult;

        _edgeFrame = frame;

        // Prefer Input System's own edge detect when available.
        try
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyCode == KeyCode.R && keyboard.rKey.wasPressedThisFrame)
            {
                _edgeResult = true;
                _wasDown = true;
                return true;
            }

            if (keyboard != null && KeyMap.TryGetValue(keyCode, out var key))
            {
                KeyControl control = keyboard[key];
                if (control != null && control.wasPressedThisFrame)
                {
                    _edgeResult = true;
                    _wasDown = true;
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
                _edgeResult = true;
                _wasDown = true;
                return true;
            }
        }
        catch
        {
            // fall through
        }

        var down = IsDown(keyCode);
        _edgeResult = down && !_wasDown;
        _wasDown = down;
        return _edgeResult;
    }

    public static bool IsDown(KeyCode keyCode)
    {
        try
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyCode == KeyCode.R && keyboard.rKey.isPressed)
                    return true;

                if (KeyMap.TryGetValue(keyCode, out var key))
                {
                    KeyControl control = keyboard[key];
                    if (control != null && control.isPressed)
                        return true;
                }
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

    public static string TipLabel(KeyCode keyCode) => keyCode switch
    {
        KeyCode.LeftShift or KeyCode.RightShift => "Shift",
        KeyCode.LeftControl or KeyCode.RightControl => "Ctrl",
        KeyCode.LeftAlt or KeyCode.RightAlt => "Alt",
        KeyCode.Return => "Enter",
        KeyCode.Escape => "Esc",
        _ when keyCode >= KeyCode.Alpha0 && keyCode <= KeyCode.Alpha9 => ((char)('0' + (keyCode - KeyCode.Alpha0))).ToString(),
        _ => keyCode.ToString(),
    };
}
