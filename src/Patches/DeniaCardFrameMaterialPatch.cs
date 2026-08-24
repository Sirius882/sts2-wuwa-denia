#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace Denia;

[HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals))]
public static class DeniaCardFrameMaterialPatch
{
    private const string AncientPortraitMaskMaterialPath =
        "res://scenes/cards/card_canvas_group_mask_material.tres";

    private static readonly Color DefaultDescriptionShadowColor = new("00000040");

    private static readonly AccessTools.FieldRef<NCard, TextureRect> FrameRef =
        AccessTools.FieldRefAccess<NCard, TextureRect>("_frame");

    private static readonly AccessTools.FieldRef<NCard, TextureRect> PortraitRef =
        AccessTools.FieldRefAccess<NCard, TextureRect>("_portrait");

    private static readonly AccessTools.FieldRef<NCard, TextureRect> PortraitBorderRef =
        AccessTools.FieldRefAccess<NCard, TextureRect>("_portraitBorder");

    private static readonly AccessTools.FieldRef<NCard, TextureRect> AncientPortraitRef =
        AccessTools.FieldRefAccess<NCard, TextureRect>("_ancientPortrait");

    private static readonly AccessTools.FieldRef<NCard, TextureRect> AncientBorderGlassOverlayRef =
        AccessTools.FieldRefAccess<NCard, TextureRect>("_ancientBorderGlassOverlay");

    private static readonly AccessTools.FieldRef<NCard, TextureRect> AncientBorderRef =
        AccessTools.FieldRefAccess<NCard, TextureRect>("_ancientBorder");

    private static readonly AccessTools.FieldRef<NCard, TextureRect> AncientTextBgRef =
        AccessTools.FieldRefAccess<NCard, TextureRect>("_ancientTextBg");

    private static readonly AccessTools.FieldRef<NCard, Control> AncientBannerRef =
        AccessTools.FieldRefAccess<NCard, Control>("_ancientBanner");

    private static readonly AccessTools.FieldRef<NCard, TextureRect> BannerRef =
        AccessTools.FieldRefAccess<NCard, TextureRect>("_banner");

    private static readonly AccessTools.FieldRef<NCard, CanvasGroup> PortraitCanvasGroupRef =
        AccessTools.FieldRefAccess<NCard, CanvasGroup>("_portraitCanvasGroup");

    private static readonly AccessTools.FieldRef<NCard, MegaRichTextLabel> DescriptionLabelRef =
        AccessTools.FieldRefAccess<NCard, MegaRichTextLabel>("_descriptionLabel");

    private static readonly List<WeakReference<NCard>> ActiveCards = [];
    private static readonly ConditionalWeakTable<NCard, object> BlackDescriptionNodes = new();

    private static void RestoreTrackedDescriptionAppearance(NCard node)
    {
        if (!BlackDescriptionNodes.TryGetValue(node, out _))
            return;

        try
        {
            MegaRichTextLabel descriptionLabel = DescriptionLabelRef(node);
            if (GodotObject.IsInstanceValid(descriptionLabel))
            {
                descriptionLabel.AddThemeColorOverride(
                    ThemeConstants.RichTextLabel.DefaultColor,
                    StsColors.cream);
                descriptionLabel.AddThemeColorOverride(
                    ThemeConstants.RichTextLabel.FontShadowColor,
                    DefaultDescriptionShadowColor);
            }
        }
        finally
        {
            BlackDescriptionNodes.Remove(node);
        }
    }

    private static void UpdateDefaultDescriptionAppearance(NCard node, CardModel card)
    {
        bool isBlack = card.IsMutable && card.Owner?.Creature is { } creature && DeniaFormHelper.IsBlack(creature);
        bool useBlackText = !isBlack
            && node.Visibility == ModelVisibility.Visible
            && card.Rarity != CardRarity.Ancient
            && card is not DeniaVirtualMatterMagneticBurst;

        MegaRichTextLabel descriptionLabel = DescriptionLabelRef(node);
        if (GodotObject.IsInstanceValid(descriptionLabel))
        {
            descriptionLabel.AddThemeColorOverride(
                ThemeConstants.RichTextLabel.DefaultColor,
                useBlackText ? Colors.Black : StsColors.cream);
            descriptionLabel.AddThemeColorOverride(
                ThemeConstants.RichTextLabel.FontShadowColor,
                useBlackText ? StsColors.transparentBlack : DefaultDescriptionShadowColor);

            if (useBlackText)
            {
                if (!BlackDescriptionNodes.TryGetValue(node, out _))
                    BlackDescriptionNodes.Add(node, new object());
            }
            else
            {
                RestoreTrackedDescriptionAppearance(node);
            }
        }
        else
        {
            BlackDescriptionNodes.Remove(node);
        }
    }

    internal static void RefreshForForm(Creature creature)
    {
        try
        {
            for (int i = ActiveCards.Count - 1; i >= 0; i--)
            {
                if (!ActiveCards[i].TryGetTarget(out NCard? card) || !GodotObject.IsInstanceValid(card))
                {
                    ActiveCards.RemoveAt(i);
                    continue;
                }

                try
                {
                    CardModel? model = card.Model;
                    if (model?.Pool is DeniaCardPool && model.IsMutable && model.Owner?.Creature == creature)
                        card.UpdateVisuals(card.DisplayingPile, CardPreviewMode.Normal);
                }
                catch (Exception)
                {
                }
            }
        }
        catch (Exception)
        {
        }
    }

    private static void TrackCard(NCard card)
    {
        try
        {
            for (int i = ActiveCards.Count - 1; i >= 0; i--)
            {
                if (!ActiveCards[i].TryGetTarget(out NCard? tracked) || !GodotObject.IsInstanceValid(tracked))
                {
                    ActiveCards.RemoveAt(i);
                    continue;
                }

                if (tracked == card)
                    return;
            }

            ActiveCards.Add(new WeakReference<NCard>(card));
        }
        catch (Exception)
        {
        }
    }

    [HarmonyPatch(typeof(NCard), nameof(NCard._EnterTree))]
    private static class EnterTreePatch
    {
        [HarmonyPostfix]
        private static void Postfix(NCard __instance)
        {
            try
            {
                if (GodotObject.IsInstanceValid(__instance))
                    TrackCard(__instance);
            }
            catch (Exception)
            {
            }
        }
    }

    private static void Postfix(NCard __instance)
    {
        try
        {
            if (!GodotObject.IsInstanceValid(__instance))
                return;
            TrackCard(__instance);
            if (__instance.Model is not CardModel card || card.Pool is not DeniaCardPool)
            {
                RestoreTrackedDescriptionAppearance(__instance);
                return;
            }

            UpdateDefaultDescriptionAppearance(__instance, card);

            if (card is DeniaVirtualMatterMagneticBurst model)
            {
                DeniaImBack ancientVisualSource = ModelDb.Card<DeniaImBack>();
                TextureRect portrait = PortraitRef(__instance);
                TextureRect portraitBorder = PortraitBorderRef(__instance);
                TextureRect eventFrame = FrameRef(__instance);
                TextureRect ancientPortrait = AncientPortraitRef(__instance);
                TextureRect ancientBorderGlassOverlay = AncientBorderGlassOverlayRef(__instance);
                TextureRect ancientBorder = AncientBorderRef(__instance);
                TextureRect ancientTextBg = AncientTextBgRef(__instance);
                Control ancientBanner = AncientBannerRef(__instance);
                TextureRect banner = BannerRef(__instance);
                CanvasGroup portraitCanvasGroup = PortraitCanvasGroupRef(__instance);

                if (!GodotObject.IsInstanceValid(portrait)
                    || !GodotObject.IsInstanceValid(portraitBorder)
                    || !GodotObject.IsInstanceValid(eventFrame)
                    || !GodotObject.IsInstanceValid(ancientPortrait)
                    || !GodotObject.IsInstanceValid(ancientBorderGlassOverlay)
                    || !GodotObject.IsInstanceValid(ancientBorder)
                    || !GodotObject.IsInstanceValid(ancientTextBg)
                    || !GodotObject.IsInstanceValid(ancientBanner)
                    || !GodotObject.IsInstanceValid(banner)
                    || !GodotObject.IsInstanceValid(portraitCanvasGroup))
                    return;

                portrait.Visible = false;
                portraitBorder.Visible = false;
                eventFrame.Visible = false;
                banner.Visible = false;
                ancientPortrait.Visible = true;
                ancientBorderGlassOverlay.Visible = true;
                ancientBorder.Visible = true;
                ancientTextBg.Visible = true;
                ancientBanner.Visible = true;
                ancientPortrait.Texture = model.Portrait;
                ancientBorder.Texture = ancientVisualSource.AncientBorder;
                ancientTextBg.Texture = ancientVisualSource.AncientTextBg;
                if (__instance.Visibility == ModelVisibility.Visible)
                    portraitCanvasGroup.Material = GD.Load<Material>(AncientPortraitMaskMaterialPath);
                return;
            }

            TextureRect frame = FrameRef(__instance);
            if (!GodotObject.IsInstanceValid(frame)) return;
            frame.Texture = card.Frame;
            frame.Material = null;
        }
        catch (Exception)
        {
        }
    }
}
