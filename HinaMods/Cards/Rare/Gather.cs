using BaseLib.Utils;
using Godot;
using Kaguya.HinaMods.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kaguya.HinaMods.Cards;

/// <summary>
/// 集结（Gather）
/// 本地化键：KAGUYA-GATHER.title / .description（由类名自动推导）
/// 1费（升级后 0 费）· 技能牌 · 罕见 · 目标：AllAllies · 关键词：Exhaust
/// 卡面：[gold]月夜2[/gold]：所有玩家获得2层[gold]力量[/gold]和2层[gold]敏捷[/gold]。
/// 代码实测：MoonCost=2；StrengthPower=2；DexterityPower=2
/// 升级：费用 -1（力量/敏捷层数与月夜门槛均不变）
/// </summary>
public sealed class Gather : HinaModsCard
{
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;
    public override List<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<MoonlightPower>(MoonCostVarName, 2m),
        new PowerVar<StrengthPower>(2m),
        new PowerVar<DexterityPower>(2m)
    ];

    protected override IEnumerable<IHoverTip> GetCustomHoverTips()
    {
        return new IHoverTip[]
        {
           HoverTipFactory.FromPower<MoonlightPower>(),
           HoverTipFactory.FromPower<StrengthPower>(),
           HoverTipFactory.FromPower<DexterityPower>()
        };
    }

    /// <summary>集结的月夜是「打出门槛」：不够就打不出去，因此免费取得时应豁免。</summary>
    protected override bool MoonlightAsPlayGate => true;

    protected override bool IsPlayable
    {
        get
        {
            if (!base.IsPlayable)
                return false;

            // 「获得+免费」的牌不消耗月夜，可直接打出
            if (SkipsMoonlightCost)
                return true;

            decimal requiredMoon = base.DynamicVars[MoonCostVarName].BaseValue;
            MoonlightPower fortunePower = base.Owner.Creature.GetPower<MoonlightPower>();

            return fortunePower != null && fortunePower.Amount >= requiredMoon;
        }
    }

    protected override bool ShouldGlowGoldInternal => IsPlayable;

    public Gather()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AllAllies)
    { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        decimal cost = EffectiveMoonCost;

        // 月夜N：非首次打出，或层数不足，则整个效果都不触发（豁免时 cost=0 直接放行）
        if (!cardPlay.IsFirstInSeries)
            return;
        if (!await TryConsumeMoonlight(choiceContext, cost, this))
            return;

        IEnumerable<Creature> teammates = from c in CombatState.GetTeammatesOf(Owner.Creature)
                                          where c != null && c.IsAlive && c.IsPlayer
                                          select c;

        foreach (Creature playerCreature in teammates)
        {
            // 修复：添加 choiceContext 参数
            await PowerCmd.Apply<StrengthPower>(
                choiceContext,
                playerCreature,
                base.DynamicVars["StrengthPower"].BaseValue,
                base.Owner.Creature,
                this
            );

            // 修复：添加 choiceContext 参数
            await PowerCmd.Apply<DexterityPower>(
                choiceContext,
                playerCreature,
                base.DynamicVars["DexterityPower"].BaseValue,
                base.Owner.Creature,
                this
            );
        }
    }

    /// <summary>升级：费用 -1（力量/敏捷层数与月夜门槛均不变）。</summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}