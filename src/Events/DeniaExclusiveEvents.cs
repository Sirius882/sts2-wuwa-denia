#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace Denia;

public abstract class DeniaExclusiveEvent : CustomEventModel
{
    public override string? CustomInitialPortraitPath => "res://images/events/denia_event_placeholder.png";

    protected static bool HasOnlyDeniaPlayers(IRunState runState) =>
        runState.Players.Count > 0 && runState.Players.All(player => player.Character is Denia);
}

/// <summary>第一层专属事件：学院的食堂。</summary>
public sealed class DeniaAcademyCafeteriaEvent : DeniaExclusiveEvent
{
    public override string? CustomInitialPortraitPath => "res://images/events/denia_event_academy_cafeteria.png";

    public override ActModel[] Acts => new ActModel[] { ModelDb.Act<Overgrowth>(), ModelDb.Act<Underdocks>() };

    public override bool IsAllowed(IRunState runState) =>
        HasOnlyDeniaPlayers(runState) && runState.Players.All(player => player.Gold >= 100);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var options = new List<EventOption>
        {
            FoodOption<DeniaFrozenStarMossCake>(100, BuyFrozenStarMossCake),
            FoodOption<DeniaFernAlgaeCake>(100, BuyFernAlgaeCake),
            FoodOption<DeniaKarakara>(100, BuyKarakara),
            FoodOption<DeniaTorchPineNut>(100, BuyTorchPineNut),
            FoodOption<DeniaRainbowCandyJump>(100, BuyRainbowCandyJump),
            FoodOption<DeniaDetermination>(50, BuyDetermination),
            FoodOption<DeniaPineNeedleRockFloss>(50, BuyPineNeedleRockFloss),
            FoodOption<DeniaCannedFishCake>(50, BuyCannedFishCake),
        };

        options.UnstableShuffle(Rng);
        return options.Take(4).ToList();
    }

    private EventOption FoodOption<T>(int cost, Func<Task> onChosen) where T : CardModel
    {
        if (Owner!.Gold < cost)
            return LockedOption($"LOCKED_{cost}");
        return Option(onChosen, HoverTipFactory.FromCardWithCardHoverTips<T>());
    }

    private Task BuyFrozenStarMossCake() => BuyCard<DeniaFrozenStarMossCake>(100, "BUY_FROZEN_STAR_MOSS_CAKE");
    private Task BuyFernAlgaeCake() => BuyCard<DeniaFernAlgaeCake>(100, "BUY_FERN_ALGAE_CAKE");
    private Task BuyKarakara() => BuyCard<DeniaKarakara>(100, "BUY_KARAKARA");
    private Task BuyTorchPineNut() => BuyCard<DeniaTorchPineNut>(100, "BUY_TORCH_PINE_NUT");
    private Task BuyRainbowCandyJump() => BuyCard<DeniaRainbowCandyJump>(100, "BUY_RAINBOW_CANDY_JUMP");
    private Task BuyDetermination() => BuyCard<DeniaDetermination>(50, "BUY_DETERMINATION");
    private Task BuyPineNeedleRockFloss() => BuyCard<DeniaPineNeedleRockFloss>(50, "BUY_PINE_NEEDLE_ROCK_FLOSS");
    private Task BuyCannedFishCake() => BuyCard<DeniaCannedFishCake>(50, "BUY_CANNED_FISH_CAKE");

    private async Task BuyCard<T>(int cost, string pageKey) where T : CardModel
    {
        await PlayerCmd.LoseGold(cost, Owner!, GoldLossType.Spent);
        CardModel card = Owner!.RunState.CreateCard<T>(Owner);
        CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card, PileType.Deck), 1.2f, CardPreviewStyle.EventLayout);
        SetEventFinished(PageDescription(pageKey));
    }
}

/// <summary>第二层专属事件：残星会的实验。</summary>
public sealed class DeniaFadedStarExperimentEvent : DeniaExclusiveEvent
{
    public override string? CustomInitialPortraitPath => "res://images/events/denia_event_faded_star_experiment.png";

    public override ActModel[] Acts => new[] { ModelDb.Act<Hive>() };

    public override bool IsAllowed(IRunState runState) =>
        HasOnlyDeniaPlayers(runState) && runState.Players.All(player => player.Creature.CurrentHp > 10);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => new EventOption[]
    {
        Option(GazeAtIt, HoverTipFactory.FromCardWithCardHoverTips<DeniaVirtualMatterMagneticBurst>()),
        Option(FallIntoBlackHole, HoverTipFactory.FromRelic<DeniaFrequencyOfNothingness>()),
        Option(WakeUp),
    };

    private async Task GazeAtIt()
    {
        await LoseHp(10);
        CardModel card = Owner!.RunState.CreateCard<DeniaVirtualMatterMagneticBurst>(Owner);
        CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card, PileType.Deck), 1.2f, CardPreviewStyle.EventLayout);
        SetEventFinished(PageDescription("GAZE_AT_IT"));
    }

    private async Task FallIntoBlackHole()
    {
        await LoseHp(10);
        await RelicCmd.Obtain<DeniaFrequencyOfNothingness>(Owner!);
        SetEventFinished(PageDescription("FALL_INTO_BLACK_HOLE"));
    }

    private async Task WakeUp()
    {
        await CreatureCmd.Heal(Owner!.Creature, 10m);
        SetEventFinished(PageDescription("WAKE_UP"));
    }

    private Task LoseHp(decimal amount) => CreatureCmd.Damage(
        new ThrowingPlayerChoiceContext(),
        Owner!.Creature,
        amount,
        ValueProp.Unblockable | ValueProp.Unpowered,
        null,
        null);
}

/// <summary>第三层专属事件：在熔毁的夜空下。</summary>
public sealed class DeniaUnderMeltingNightSkyEvent : DeniaExclusiveEvent
{
    public override string? CustomInitialPortraitPath => "res://images/events/denia_event_under_melting_night_sky.png";

    public override ActModel[] Acts => new[] { ModelDb.Act<Glory>() };

    public override bool IsAllowed(IRunState runState) =>
        HasOnlyDeniaPlayers(runState)
        && runState.Players.All(player => player.Creature.CurrentHp * 2 < player.Creature.MaxHp);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => new EventOption[]
    {
        Option(AskForCake),
        Option(GoToArcade),
        Option(TakeWandererHome),
    };

    private async Task AskForCake()
    {
        await CreatureCmd.Heal(Owner!.Creature, Owner.Creature.MaxHp - Owner.Creature.CurrentHp);
        SetEventFinished(PageDescription("ASK_FOR_CAKE"));
    }

    private async Task GoToArcade()
    {
        var prefs = new CardSelectorPrefs(CardSelectorPrefs.UpgradeSelectionPrompt, 0, 5)
        {
            Cancelable = true,
        };
        foreach (CardModel card in await CardSelectCmd.FromDeckForUpgrade(Owner!, prefs))
            CardCmd.Upgrade(card);
        SetEventFinished(PageDescription("GO_TO_ARCADE"));
    }

    private async Task TakeWandererHome()
    {
        var prefs = new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 0, 3)
        {
            Cancelable = true,
        };
        await CardPileCmd.RemoveFromDeck((await CardSelectCmd.FromDeckForRemoval(Owner!, prefs)).ToList());
        SetEventFinished(PageDescription("TAKE_WANDERER_HOME"));
    }
}
