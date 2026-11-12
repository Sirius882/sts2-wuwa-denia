#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;

namespace Denia;

/// <summary>第一层专属事件：学院的食堂。</summary>
public sealed class DeniaAcademyCafeteriaEvent : DeniaExclusiveEvent
{
    private int[]? _foodChoiceIndices;

    public override string? CustomInitialPortraitPath => "res://images/events/denia_event_academy_cafeteria.png";
    public override ActModel[] Acts => new ActModel[] { ModelDb.Act<Overgrowth>(), ModelDb.Act<Underdocks>() };
    public override bool IsAllowed(IRunState runState) => HasOnlyDeniaPlayers(runState);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var options = new List<EventOption>
        {
            FoodOption<DeniaFrozenStarMossCake, DeniaMeltingAway>(TakeFrozenStarMossCake),
            FoodOption<DeniaFernAlgaeCake, DeniaHappyBirthday>(TakeFernAlgaeCake),
            FoodOption<DeniaKarakara, DeniaCrush>(TakeKarakara),
            FoodOption<DeniaTorchPineNut, DeniaDontComeIn>(TakeTorchPineNut),
            FoodOption<DeniaRainbowCandyJump, DeniaInspectionReportRa2499G>(TakeRainbowCandyJump),
            FoodOption<DeniaDetermination, DeniaEntropyBoost>(TakeDetermination),
            FoodOption<DeniaPineNeedleRockFloss, DeniaLightCall>(TakePineNeedleRockFloss),
            FoodOption<DeniaCannedFishCake, DeniaKeepRunning>(TakeCannedFishCake),
        };
        if (_foodChoiceIndices == null)
        {
            var indices = Enumerable.Range(0, options.Count).ToList();
            indices.UnstableShuffle(Rng);
            _foodChoiceIndices = indices.Take(3).ToArray();
        }
        // 返回时保留原来的三组，但重建选项以重置原生 WasChosen 标记。
        return _foodChoiceIndices.Select(i => options[i]).Append(Option(RefuseFood)).ToArray();
    }

    private EventOption FoodOption<TFood, TCard>(Func<Task> onChosen)
        where TFood : CardModel where TCard : CardModel =>
        Option(onChosen, HoverTipFactory.FromCardWithCardHoverTips<TFood>()
            .Concat(HoverTipFactory.FromCardWithCardHoverTips<TCard>()));

    private Task TakeFrozenStarMossCake() => TakeCards<DeniaFrozenStarMossCake, DeniaMeltingAway>("TAKE_FROZEN_STAR_MOSS_CAKE");
    private Task TakeFernAlgaeCake() => TakeCards<DeniaFernAlgaeCake, DeniaHappyBirthday>("TAKE_FERN_ALGAE_CAKE");
    private Task TakeKarakara() => TakeCards<DeniaKarakara, DeniaCrush>("TAKE_KARAKARA");
    private Task TakeTorchPineNut() => TakeCards<DeniaTorchPineNut, DeniaDontComeIn>("TAKE_TORCH_PINE_NUT");
    private Task TakeRainbowCandyJump() => TakeCards<DeniaRainbowCandyJump, DeniaInspectionReportRa2499G>("TAKE_RAINBOW_CANDY_JUMP");
    private Task TakeDetermination() => TakeCards<DeniaDetermination, DeniaEntropyBoost>("TAKE_DETERMINATION");
    private Task TakePineNeedleRockFloss() => TakeCards<DeniaPineNeedleRockFloss, DeniaLightCall>("TAKE_PINE_NEEDLE_ROCK_FLOSS");
    private Task TakeCannedFishCake() => TakeCards<DeniaCannedFishCake, DeniaKeepRunning>("TAKE_CANNED_FISH_CAKE");

    private async Task TakeCards<TFood, TCard>(string pageKey)
        where TFood : CardModel where TCard : CardModel
    {
        foreach (CardModel card in new CardModel[] { Owner!.RunState.CreateCard<TFood>(Owner), Owner.RunState.CreateCard<TCard>(Owner) })
            CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card, PileType.Deck), 1.2f, CardPreviewStyle.EventLayout);
        SetEventFinished(PageDescription(pageKey));
    }

    private Task RefuseFood()
    {
        SetEventState(PageDescription("REFUSE_FOOD"), new[]
        {
            Option(ConfirmStarve, "REFUSE_FOOD").ThatWillKillPlayerIf(_ => true),
            Option(ReturnToFood, "REFUSE_FOOD"),
        });
        return Task.CompletedTask;
    }

    private Task ReturnToFood()
    {
        SetEventState(InitialDescription, GenerateInitialOptions());
        return Task.CompletedTask;
    }

    private async Task ConfirmStarve()
    {
        SetEventFinished(PageDescription("STARVE"));
        await CreatureCmd.Kill(Owner!.RunState.Players.Select(player => player.Creature).ToArray(), force: true);
    }
}
