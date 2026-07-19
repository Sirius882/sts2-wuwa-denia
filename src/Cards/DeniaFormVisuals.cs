using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace Denia;

/// <summary>双形态战斗立绘覆盖。类名保留 DeniaFormPatch 供 FormHelper.RefreshForCreature 调用。</summary>
[HarmonyPatch(typeof(NCreature), "_Ready")]
public static class DeniaFormPatch
{
    private static readonly Dictionary<NCreature, TextureRect> _pinkOverlay = new();
    private static readonly Dictionary<NCreature, TextureRect> _blackOverlay = new();

    public static void Postfix(NCreature __instance)
    {
        // 规则注册改由 DeniaEntry.Init 完成
        var creature = __instance.Entity;
        if (creature == null || !creature.IsPlayer) return;
        if (creature.Player?.Character is not Denia) return;

        if (!_pinkOverlay.ContainsKey(__instance))
        {
            try
            {
                var pinkTex = ResourceLoader.Load<Texture2D>(
                    "res://images/packed/character_select/denia_pink.png");
                var blackTex = ResourceLoader.Load<Texture2D>(
                    "res://images/packed/character_select/denia_black.png");

                var pink = MakeOverlay(pinkTex);
                var black = MakeOverlay(blackTex);

                __instance.AddChild(pink);
                __instance.AddChild(black);

                _pinkOverlay[__instance] = pink;
                _blackOverlay[__instance] = black;

                if (__instance.Visuals != null)
                    __instance.Visuals.Visible = false;
            }
            catch (Exception ex) { GD.PrintErr($"[Denia] Form overlay load error: {ex.Message}"); }
        }

        if (__instance.Visuals != null && GodotObject.IsInstanceValid(__instance.Visuals))
        {
            var bounds = GetBoundsNode(__instance.Visuals);
            if (bounds != null && GodotObject.IsInstanceValid(bounds))
                PositionOverlays(__instance, bounds);
        }

        RefreshForCreature(creature);
    }

    private static Control? GetBoundsNode(NCreatureVisuals visuals)
    {
        var sc = visuals.GetNodeOrNull<Node>("ScaleContainer");
        if (sc != null) return sc.GetNodeOrNull<Control>("Bounds");
        return visuals.GetNodeOrNull<Control>("Bounds");
    }

    private static void PositionOverlays(NCreature nc, Control bounds)
    {
        var offset = bounds.GlobalPosition - nc.GlobalPosition;
        var size = bounds.Size * nc.Visuals.Scale;

        if (_pinkOverlay.TryGetValue(nc, out var pink))
        {
            pink.Position = offset;
            pink.Size = size;
            pink.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            pink.StretchMode = TextureRect.StretchModeEnum.Scale;
        }
        if (_blackOverlay.TryGetValue(nc, out var black))
        {
            black.Position = offset;
            black.Size = size;
            black.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            black.StretchMode = TextureRect.StretchModeEnum.Scale;
        }
    }

    private static TextureRect MakeOverlay(Texture2D tex)
    {
        return new TextureRect
        {
            Texture = tex,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
    }

    public static void RefreshForCreature(Creature creature)
    {
        var room = NCombatRoom.Instance;
        if (room == null) return;
        var node = room.GetCreatureNode(creature);
        if (node == null || !GodotObject.IsInstanceValid(node)) return;
        bool isBlack = DeniaFormHelper.GetForm(creature) == DeniaForm.Black;

        if (_pinkOverlay.TryGetValue(node, out var pink) && GodotObject.IsInstanceValid(pink))
            pink.Visible = !isBlack;
        if (_blackOverlay.TryGetValue(node, out var black) && GodotObject.IsInstanceValid(black))
            black.Visible = isBlack;

        // 释放已失效节点条目，避免字典泄漏
        PruneDeadOverlays();
    }

    private static void PruneDeadOverlays()
    {
        try
        {
            var dead = new List<NCreature>();
            foreach (var kv in _pinkOverlay)
            {
                if (!GodotObject.IsInstanceValid(kv.Key))
                    dead.Add(kv.Key);
            }
            foreach (var k in dead)
            {
                _pinkOverlay.Remove(k);
                _blackOverlay.Remove(k);
            }
        }
        catch { }
    }
}
