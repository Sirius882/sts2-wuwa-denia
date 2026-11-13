#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
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


/// <summary>第二层专属事件：夏日海滩。</summary>
public sealed class DeniaSummerBeachEvent : DeniaExclusiveEvent
{
    public override string? CustomInitialPortraitPath => "res://images/events/denia_event_summer_beach.jpg";

    public override ActModel[] Acts => new[] { ModelDb.Act<Hive>() };

    public override bool IsAllowed(IRunState runState) => HasOnlyDeniaPlayers(runState);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => new EventOption[]
    {
        Option(GetPeachIce, HoverTipFactory.FromCardWithCardHoverTips<DeniaPeachIce>()).ThatDecreasesMaxHp(12),
        Option(GetMintCandy, HoverTipFactory.FromCardWithCardHoverTips<DeniaMintCandy>()).ThatDecreasesMaxHp(12),
        Option(Leave),
    };

    private Task GetPeachIce() => ObtainCard<DeniaPeachIce>("GET_PEACH_ICE");
    private Task GetMintCandy() => ObtainCard<DeniaMintCandy>("GET_MINT_CANDY");

    private Task Leave()
    {
        SetEventFinished(PageDescription("LEAVE"));
        return Task.CompletedTask;
    }

    private async Task ObtainCard<T>(string pageKey) where T : CardModel
    {
        await CreatureCmd.LoseMaxHp(new ThrowingPlayerChoiceContext(), Owner!.Creature, 12m, cardSource: null);
        CardModel card = Owner.RunState.CreateCard<T>(Owner);
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
