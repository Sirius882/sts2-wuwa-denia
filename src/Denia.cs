using System;
using System.Collections.Generic;
using BaseLib.Abstracts;
using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Relics;

namespace Denia;

public sealed class Denia : PlaceholderCharacterModel
{
    public override string PlaceholderID => "necrobinder";
    public override string CustomIconTexturePath =>
        "res://images/ui/top_panel/character_icon_denia.png";
    public override string? CustomIconPath =>
        "res://scenes/ui/character_icons/denia_icon.tscn";
    public override string? CustomCharacterSelectIconPath =>
        "res://images/char_select/denia_icon.jpg";
    public override string? CustomCharacterSelectBg =>
        "res://scenes/screens/char_select/denia_bg.tscn";
    // 火堆/商店：需在 Godot 编辑器里创建带 NRestSiteCharacter/NMerchantCharacter 脚本的 tscn
    // public override string? CustomRestSiteAnimPath =>
    //     "res://scenes/rest_site/characters/denia_rest_site.tscn";
    // public override string? CustomMerchantAnimPath =>
    //     "res://scenes/merchant/characters/denia_merchant.tscn";

    // 双形态视觉通过 DeniaFormPatch (Harmony) 实现

    // 本地化
    public override List<(string, string)>? Localization =>
        new CharacterLoc(
            Title:                   "达妮娅",
            TitleObject:             "达妮娅",
            Description:             "如果执着得够久，故事就一定能迎来好结局，不是吗？\n“我在终点站等你”\n黑色形态下，能以虚质和黯核强化卡牌的效果。",
            PronounObject:           "她",
            PronounSubject:          "她",
            PronounPossessive:       "她的",
            PossessiveAdjective:     "她的",
            AromaPrinciple:          "鸣式阿列夫一的气息",
            EndTurnPingAlive:        "我赶时间",
            EndTurnPingDead:         "有人还在……等我……",
            EventDeathPrevention:    "达妮娅的意志拒绝倒下。",
            GoldMonologue:           "能拿去换点甜品嘛？",
            CardsModifierTitle:      "达妮娅",
            CardsModifierDescription: "达妮娅卡牌"
        );

    public override Color NameColor => new Color("FF69B4");
    public override CharacterGender Gender => CharacterGender.Feminine;
    public override int StartingHp => 80;
    public override int StartingGold => 99;
    public override bool ShouldAlwaysShowStarCounter => true;

    public override CardPoolModel CardPool => ModelDb.CardPool<DeniaCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<DeniaRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<DeniaPotionPool>();

    public override IEnumerable<CardModel> StartingDeck => new CardModel[10]
    {
        ModelDb.Card<DeniaStrike>(),
        ModelDb.Card<DeniaStrike>(),
        ModelDb.Card<DeniaStrike>(),
        ModelDb.Card<DeniaStrike>(),
        ModelDb.Card<DeniaDefend>(),
        ModelDb.Card<DeniaDefend>(),
        ModelDb.Card<DeniaDefend>(),
        ModelDb.Card<DeniaDefend>(),
        ModelDb.Card<DeniaPleaseDoNot>(),
        ModelDb.Card<DeniaBackToPink>(),
    };

    public override IReadOnlyList<RelicModel> StartingRelics =>
        new RelicModel[] { ModelDb.Relic<DeniaTrickster>() };

    public override float AttackAnimDelay => 0.15f;
    public override float CastAnimDelay => 0.25f;

    // 覆写过渡音效：默认路径 wipe_{id} 不存在，使用 wipe_ironclad 代替
    public override string CharacterTransitionSfx => "event:/sfx/ui/wipe_ironclad";

    // 预加载自定义资源，避免运行时同步加载卡顿
    protected override IEnumerable<string> ExtraAssetPaths => new string[]
    {
        // 角色图标
        "res://images/ui/top_panel/character_icon_denia.png",
        "res://scenes/ui/character_icons/denia_icon.tscn",
        // 选人界面
        "res://images/char_select/denia_icon.jpg",
        "res://scenes/screens/char_select/denia_bg.tscn",
        // 双形态立绘
        "res://images/packed/character_select/denia_pink.png",
        "res://images/packed/character_select/denia_black.png",
        // 战斗UI图标
        "res://images/ui/combat/denia_virtual_matter.png",
        "res://images/ui/combat/denia_virtual_matter_cost_icon.png",
        "res://images/ui/combat/denia_dark_core_cost_icon.png",
        "res://images/ui/combat/denia_dark_core_icon.png",
        // 火堆/商店角色场景
        "res://scenes/rest_site/characters/denia_rest_site.tscn",
        "res://scenes/merchant/characters/denia_merchant.tscn",
    };

    public override Color EnergyLabelOutlineColor => new Color("801212FF");
    public override Color DialogueColor => new Color("4a1530");
    public override Color RemoteTargetingLineColor => new Color("FF69B4FF");

    public override List<string> GetArchitectAttackVfx()
    {
        return new List<string>(5)
        {
            "vfx/vfx_attack_blunt",
            "vfx/vfx_heavy_blunt",
            "vfx/vfx_attack_slash",
            "vfx/vfx_bloody_impact",
            "vfx/vfx_rock_shatter",
        };
    }
}
