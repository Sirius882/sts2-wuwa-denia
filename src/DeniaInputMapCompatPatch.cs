// ============================================================
// InputMap / 手柄映射兼容补丁（临时、独立文件）
// ============================================================
// 现象：
//   日志台持续刷：
//   The InputMap action "controller_l_stick_press" doesn't exist.
//   The InputMap action "controller_r_stick_right/up/..." doesn't exist.
//
// 根因：
//   1) 游戏 0.107+ 正式名是 controller_joystick_*（见 Controller.cs）
//   2) SettingsSave.ControllerMapping 旧存档可能仍保存 controller_l/r_stick_* 旧名
//   3) NInputManager.Init 原样写入 _controllerInputMap
//   4) _UnhandledInput 对每个映射值调用 IsActionPressed/Released → 刷 ERROR
//
// 修复策略（双保险）：
//   A. 补全旧 action 到 Godot InputMap（静态 API，非 Singleton）
//   B. 清洗 _controllerInputMap：旧名→新名；仍无效→默认映射
//   C. 在 _UnhandledInput Prefix 入口每次先确保 A+B（保证一定在报错前执行）
//   D. 同步清洗 SettingsSave.ControllerMapping，避免下次启动再灌脏数据
//
// 删除条件：
//   游戏/BaseLib 自带“忽略无效 ControllerMapping 值”后，
//   直接删除本文件即可，无需改其他代码。
// ============================================================
#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Saves;

namespace Denia;

/// <summary>旧手柄 action 名 → 当前游戏正式名。</summary>
internal static class DeniaControllerActionAliases
{
    public static readonly Dictionary<string, string> Map = new(StringComparer.Ordinal)
    {
        ["controller_l_stick_press"] = "controller_joystick_press",
        ["controller_r_stick_press"] = "controller_joystick_press",
        ["controller_l_stick_left"] = "controller_joystick_left",
        ["controller_l_stick_right"] = "controller_joystick_right",
        ["controller_l_stick_up"] = "controller_joystick_up",
        ["controller_l_stick_down"] = "controller_joystick_down",
        ["controller_r_stick_left"] = "controller_joystick_left",
        ["controller_r_stick_right"] = "controller_joystick_right",
        ["controller_r_stick_up"] = "controller_joystick_up",
        ["controller_r_stick_down"] = "controller_joystick_down",
    };
}

/// <summary>
/// 共享实现：补 InputMap action + 清洗 NInputManager/_settings 映射。
/// </summary>
internal static class DeniaInputMapCompat
{
    private static readonly AccessTools.FieldRef<NInputManager, Dictionary<StringName, StringName>> ControllerInputMapRef =
        AccessTools.FieldRefAccess<NInputManager, Dictionary<StringName, StringName>>("_controllerInputMap");

    private static bool _actionsEnsured;
    private static bool _mapCleaned;
    private static bool _settingsCleaned;
    private static bool _loggedSanitize;

    public static void EnsureAll(NInputManager? manager)
    {
        EnsureMissingInputMapActions();
        if (manager != null)
            SanitizeControllerInputMap(manager);
        SanitizeSettingsSave();
    }

    /// <summary>
    /// Godot 4.5 C# 使用静态 InputMap.HasAction/AddAction，不是 InputMap.Singleton。
    /// </summary>
    public static void EnsureMissingInputMapActions()
    {
        if (_actionsEnsured) return;

        try
        {
            // 先探测 InputMap 是否可用；失败则下次再试，不要永久锁死
            _ = InputMap.HasAction("ui_accept");

            EnsureAction("controller_l_stick_press", 0.2f,
                new InputEventJoypadButton { ButtonIndex = JoyButton.LeftStick, Pressed = true });
            EnsureAction("controller_r_stick_press", 0.2f,
                new InputEventJoypadButton { ButtonIndex = JoyButton.RightStick, Pressed = true });

            EnsureAction("controller_r_stick_left", 0.5f,
                new InputEventJoypadMotion { Axis = JoyAxis.RightX, AxisValue = -1.0f });
            EnsureAction("controller_r_stick_right", 0.5f,
                new InputEventJoypadMotion { Axis = JoyAxis.RightX, AxisValue = 1.0f });
            EnsureAction("controller_r_stick_up", 0.5f,
                new InputEventJoypadMotion { Axis = JoyAxis.RightY, AxisValue = -1.0f });
            EnsureAction("controller_r_stick_down", 0.5f,
                new InputEventJoypadMotion { Axis = JoyAxis.RightY, AxisValue = 1.0f });

            EnsureAction("controller_l_stick_left", 0.5f,
                new InputEventJoypadMotion { Axis = JoyAxis.LeftX, AxisValue = -1.0f });
            EnsureAction("controller_l_stick_right", 0.5f,
                new InputEventJoypadMotion { Axis = JoyAxis.LeftX, AxisValue = 1.0f });
            EnsureAction("controller_l_stick_up", 0.5f,
                new InputEventJoypadMotion { Axis = JoyAxis.LeftY, AxisValue = -1.0f });
            EnsureAction("controller_l_stick_down", 0.5f,
                new InputEventJoypadMotion { Axis = JoyAxis.LeftY, AxisValue = 1.0f });

            _actionsEnsured = true;
            if (!_loggedSanitize)
                GD.Print("[Denia] InputMap compat: ensured legacy controller_* stick actions.");
        }
        catch (Exception ex)
        {
            // 不置 _actionsEnsured，允许后续重试
            GD.PrintErr($"[Denia] InputMap ensure failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void EnsureAction(string name, float deadzone, InputEvent ev)
    {
        if (InputMap.HasAction(name)) return;
        InputMap.AddAction(name, deadzone);
        InputMap.ActionAddEvent(name, ev);
    }

    public static void SanitizeControllerInputMap(NInputManager manager)
    {
        if (_mapCleaned) return;

        try
        {
            Dictionary<StringName, StringName>? map = ControllerInputMapRef(manager);
            if (map == null || map.Count == 0) return;

            Dictionary<StringName, StringName>? defaults = null;
            try { defaults = manager.ControllerManager?.GetDefaultControllerInputMap; }
            catch { /* ControllerManager 可能尚未就绪 */ }

            var keys = new List<StringName>(map.Keys);
            bool changed = false;
            foreach (StringName uxAction in keys)
            {
                string current = map[uxAction].ToString();
                if (string.IsNullOrEmpty(current)) continue;

                string resolved = ResolveActionName(current, defaults, uxAction);
                if (!string.Equals(resolved, current, StringComparison.Ordinal))
                {
                    map[uxAction] = resolved;
                    changed = true;
                }
            }

            _mapCleaned = true;
            if (changed && !_loggedSanitize)
            {
                _loggedSanitize = true;
                GD.Print("[Denia] InputMap compat: sanitized NInputManager._controllerInputMap.");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Denia] Controller map sanitize failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// 清洗设置存档里的 ControllerMapping，避免下次启动再灌脏数据。
    /// </summary>
    public static void SanitizeSettingsSave()
    {
        if (_settingsCleaned) return;

        try
        {
            SettingsSave? settings = SaveManager.Instance?.SettingsSave;
            if (settings?.ControllerMapping == null || settings.ControllerMapping.Count == 0)
            {
                _settingsCleaned = true;
                return;
            }

            bool changed = false;
            var keys = new List<string>(settings.ControllerMapping.Keys);
            foreach (string ux in keys)
            {
                string current = settings.ControllerMapping[ux];
                if (string.IsNullOrEmpty(current)) continue;

                if (DeniaControllerActionAliases.Map.TryGetValue(current, out string? alias)
                    && !string.IsNullOrEmpty(alias)
                    && !string.Equals(alias, current, StringComparison.Ordinal))
                {
                    settings.ControllerMapping[ux] = alias;
                    changed = true;
                }
            }

            _settingsCleaned = true;
            if (changed)
            {
                try { SaveManager.Instance.SaveSettings(); }
                catch { /* 存档失败不致命 */ }
                if (!_loggedSanitize)
                {
                    _loggedSanitize = true;
                    GD.Print("[Denia] InputMap compat: cleaned SettingsSave.ControllerMapping.");
                }
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Denia] SettingsSave sanitize failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string ResolveActionName(
        string current,
        Dictionary<StringName, StringName>? defaults,
        StringName uxAction)
    {
        // 已知旧名 → 新名
        if (DeniaControllerActionAliases.Map.TryGetValue(current, out string? alias)
            && !string.IsNullOrEmpty(alias)
            && InputMap.HasAction(alias))
        {
            return alias;
        }

        // 当前名本身有效
        if (InputMap.HasAction(current))
            return current;

        // 回退默认
        if (defaults != null
            && defaults.TryGetValue(uxAction, out StringName def)
            && InputMap.HasAction(def))
        {
            return def.ToString();
        }

        // 仍无效：尽量用别名（即便 InputMap 刚补上）
        if (DeniaControllerActionAliases.Map.TryGetValue(current, out string? fallback)
            && !string.IsNullOrEmpty(fallback))
        {
            return fallback;
        }

        return current;
    }
}

// 关键：每次输入前先清洗，确保一定在 IsActionPressed 报错前执行
[HarmonyPatch(typeof(NInputManager), nameof(NInputManager._UnhandledInput))]
public static class DeniaInputMapCompatUnhandledInputPatch
{
    public static void Prefix(NInputManager __instance)
    {
        DeniaInputMapCompat.EnsureAll(__instance);
    }
}

// 启动兜底：NInputManager._Ready
[HarmonyPatch(typeof(NInputManager), nameof(NInputManager._Ready))]
public static class DeniaInputMapCompatReadyPatch
{
    public static void Prefix()
    {
        DeniaInputMapCompat.EnsureMissingInputMapActions();
    }

    public static void Postfix(NInputManager __instance)
    {
        DeniaInputMapCompat.EnsureAll(__instance);
    }
}

// Init 完成后清洗（存档映射灌入之后）
[HarmonyPatch(typeof(NInputManager), "Init")]
public static class DeniaInputMapCompatInitPatch
{
    public static void Postfix(NInputManager __instance, ref Task __result)
    {
        __result = Wrap(__instance, __result);
    }

    private static async Task Wrap(NInputManager instance, Task original)
    {
        DeniaInputMapCompat.EnsureMissingInputMapActions();
        await (original ?? Task.CompletedTask);
        DeniaInputMapCompat.EnsureAll(instance);
    }
}
