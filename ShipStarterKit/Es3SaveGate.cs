using System;
using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;

namespace ShipStarterKit;

/// <summary>
/// First-load-per-save gate.
///
/// Primary: ES3 bool key <c>ShipStarterKit_Supplied</c> on
/// <see cref="GameNetworkManager.currentSaveFileName"/> (same Easy Save 3 store
/// Lethal Company uses for LCSaveFile1/2/3). Cleared when the host starts a new
/// game on that slot via <see cref="GameNetworkManager.ResetSavedGameValues"/>.
///
/// Fallback: a small marker file under BepInEx/config/ShipStarterKit/ if ES3
/// reflection is unavailable (should not happen in a normal LC install).
///
/// Gate timing: checked once from StartOfRound.Start (server) — that method runs
/// when a save is loaded into the ship lobby, NOT when returning from moons
/// (StartOfRound persists across moon loads). Combined with the ES3 flag, reloads
/// of the same save also skip re-supply.
/// </summary>
internal static class Es3SaveGate
{
    public const string SuppliedKey = "ShipStarterKit_Supplied";

    private static Type? _es3Type;
    private static bool _es3Resolved;
    private static MethodInfo? _keyExists;
    private static MethodInfo? _deleteKey;
    private static MethodInfo? _saveBool;

    public static string? CurrentSaveName()
    {
        var gnm = GameNetworkManager.Instance;
        if (gnm == null || string.IsNullOrEmpty(gnm.currentSaveFileName))
            return null;
        return gnm.currentSaveFileName;
    }

    public static bool AlreadySupplied(string saveName)
    {
        if (TryEs3KeyExists(SuppliedKey, saveName, out var exists))
            return exists;
        return File.Exists(FallbackPath(saveName));
    }

    public static void MarkSupplied(string saveName)
    {
        if (TryEs3SaveBool(SuppliedKey, true, saveName))
        {
            Plugin.V($"ES3 marked {SuppliedKey}=true on '{saveName}'.");
            return;
        }

        try
        {
            var path = FallbackPath(saveName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "1");
            Plugin.Log.LogWarning($"ES3 unavailable; wrote fallback marker {path}");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Failed to write supply marker for '{saveName}': {ex}");
        }
    }

    public static void ClearSupplied(string saveName)
    {
        if (TryEs3DeleteKey(SuppliedKey, saveName))
            Plugin.V($"ES3 cleared {SuppliedKey} on '{saveName}' (new game reset).");

        try
        {
            var path = FallbackPath(saveName);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Failed to clear fallback marker for '{saveName}': {ex.Message}");
        }
    }

    private static string FallbackPath(string saveName)
    {
        var safe = string.Join("_", saveName.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(Paths.ConfigPath, "ShipStarterKit", $"supplied_{safe}.flag");
    }

    private static bool EnsureEs3()
    {
        if (_es3Resolved)
            return _es3Type != null;

        _es3Resolved = true;
        _es3Type = AccessTools.TypeByName("ES3");
        if (_es3Type == null)
        {
            Plugin.Log.LogWarning("ES3 type not found; using file fallback for first-load gate.");
            return false;
        }

        _keyExists = AccessTools.Method(_es3Type, "KeyExists", new[] { typeof(string), typeof(string) });
        _deleteKey = AccessTools.Method(_es3Type, "DeleteKey", new[] { typeof(string), typeof(string) });

        // ES3.Save<T>(string key, T value, string file)
        foreach (var m in _es3Type.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (m.Name != "Save" || !m.IsGenericMethodDefinition)
                continue;
            var ps = m.GetParameters();
            if (ps.Length == 3
                && ps[0].ParameterType == typeof(string)
                && ps[2].ParameterType == typeof(string))
            {
                _saveBool = m.MakeGenericMethod(typeof(bool));
                break;
            }
        }

        if (_keyExists == null || _saveBool == null)
        {
            Plugin.Log.LogWarning("ES3 methods incomplete; using file fallback for first-load gate.");
            _es3Type = null;
            return false;
        }

        Plugin.V("ES3 reflection ready for ShipStarterKit save gate.");
        return true;
    }

    private static bool TryEs3KeyExists(string key, string file, out bool exists)
    {
        exists = false;
        if (!EnsureEs3() || _keyExists == null)
            return false;
        try
        {
            exists = (bool)_keyExists.Invoke(null, new object[] { key, file })!;
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"ES3.KeyExists failed: {ex.Message}");
            return false;
        }
    }

    private static bool TryEs3SaveBool(string key, bool value, string file)
    {
        if (!EnsureEs3() || _saveBool == null)
            return false;
        try
        {
            _saveBool.Invoke(null, new object[] { key, value, file });
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"ES3.Save<bool> failed: {ex.Message}");
            return false;
        }
    }

    private static bool TryEs3DeleteKey(string key, string file)
    {
        if (!EnsureEs3() || _deleteKey == null)
            return false;
        try
        {
            _deleteKey.Invoke(null, new object[] { key, file });
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"ES3.DeleteKey failed: {ex.Message}");
            return false;
        }
    }
}
