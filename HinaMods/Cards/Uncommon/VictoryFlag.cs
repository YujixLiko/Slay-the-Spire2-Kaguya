using BaseLib.Utils;
using Godot;
using Kaguya.HinaMods.Powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using Kaguya.HinaMods;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kaguya.HinaMods.Cards;

/// <summary>
/// 旗开得胜（VictoryFlag）
/// 本地化键：KAGUYA-SUPPORT_MOONLIGHT_CALL.title / .description
/// 0费 · 技能牌 · 罕见 · 目标：自身 · 标签：支援牌
/// 卡面：从3张[blue]支援[/blue]牌中选择1张加入[gold]手牌[/gold]；[gold]月夜2[/gold]：将其升级。
/// 代码实测：MoonCost=2
/// 升级：月夜门槛 2 → 1
/// 说明：月夜不足时仍可打出，只是不触发升级。
/// </summary>
public class VictoryFlag() : HinaModsCard(0,
    CardType.Skill, CardRarity.Uncommon,
    TargetType.Self)
{
    /// <summary>月夜消耗量（达到该层数则把选中的支援牌升级）。</summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<MoonlightPower>(MoonCostVarName, 2m)
    ];

    public override HashSet<string> CustomTags => new HashSet<string>() { CustomCardTags.SUPPORTCARD };

    protected override IEnumerable<IHoverTip> GetCustomHoverTips()
    {
        return new IHoverTip[]
        {
            HoverTipFactory.FromPower<MoonlightPower>()
        };
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!cardPlay.IsFirstInSeries)
            return;

        List<HinaModsCard> allSupportCards = ModelDb.AllCards
            .OfType<HinaModsCard>()
            .Where(c => c.CustomTags != null
                        && c.CustomTags.Any()
                        && c.CustomTags.Contains(CustomCardTags.SUPPORT))
            .Where(c => HinaModsCard.AllowedInRun(c, Owner))
            .ToList();

        if (allSupportCards.Count == 0)
            return;

        // 随机 3 张不重复支援牌供选择
        List<CardModel> selectedCards = CardFactory
            .GetDistinctForCombat(Owner, allSupportCards.Cast<CardModel>().ToList(), 3,
                                  Owner.RunState.Rng.CombatCardGeneration)
            .ToList();

        if (selectedCards.Count == 0)
            return;

        CardModel chosenCard = await CardSelectCmd.FromChooseACardScreen(
            choiceContext, selectedCards, Owner, canSkip: true);

        if (chosenCard == null)
            return;

        // 月夜2（升级后月夜1）：将其升级。层数不足则照常加入手牌、不升级。
        decimal requiredMoon = DynamicVars[MoonCostVarName].BaseValue;
        if (await TryConsumeMoonlight(choiceContext, requiredMoon, this))
        {
            TryUpgrade(chosenCard);
        }

        await CardPileCmd.AddGeneratedCardToCombat(
            chosenCard,
            PileType.Hand,
            Owner,
            CardPilePosition.Top);
    }

    /// <summary>尝试升级一张牌（幂等）。</summary>
    private static void TryUpgrade(CardModel card)
    {
        if (card == null || !card.IsMutable || !card.IsUpgradable || card.IsUpgraded)
            return;

        card.UpgradeInternal();
        card.FinalizeUpgradeInternal();
    }

    /// <summary>升级：月夜门槛 2 → 1。</summary>
    protected override void OnUpgrade()
    {
        DynamicVars[MoonCostVarName].UpgradeValueBy(-1m);
    }
}
