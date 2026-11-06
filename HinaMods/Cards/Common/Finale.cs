using BaseLib.Utils;
using Godot;
using Kaguya.HinaMods.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kaguya.HinaMods.Cards;

/// <summary>
/// 收场（Finale，旧显示名「预备」）
/// 本地化键：KAGUYA-SUPPORT_ALLOCATION.title / .description
/// 2费 · 攻击牌 · 普通 · 目标：单个敌人 · 标签：支援牌
/// 卡面：造成{Damage:diff()}点伤害，下个回合开始时获得1张[blue]全垒打[/blue]。
///       [gold]月夜{MoonCost:diff()}[/gold]：全垒打费用减少2点。
/// 代码实测：Damage=12；MoonCost=3
/// 升级：Damage +3（12→15）；月夜 -1（3→2）
/// </summary>
public sealed class Finale : HinaModsCard
{
    /// <summary>支援牌标签（本牌会获得支援牌）。</summary>
    public override HashSet<string> CustomTags => new HashSet<string>() { CustomCardTags.SUPPORTCARD };

    private const int HomeRunDiscount = 2;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(18m, ValueProp.Move),
        new PowerVar<MoonlightPower>(MoonCostVarName, 3m)
    ];

    protected override IEnumerable<IHoverTip> GetCustomHoverTips()
    {
        return new IHoverTip[]
        {
            HoverTipFactory.FromCard<HomeRun>(),
            HoverTipFactory.FromPower<FinalePower>(),
            HoverTipFactory.FromPower<MoonlightPower>()
        };
    }

    public Finale()
        : base(3, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay).Execute(choiceContext);

        if (!cardPlay.IsFirstInSeries)
            return;

        bool discounted = await TryConsumeMoonlight(
            choiceContext,
            DynamicVars[MoonCostVarName].BaseValue,
            this);

        await PowerCmd.Apply<FinalePower>(
            choiceContext,
            Owner.Creature,
            1,
            Owner.Creature,
            this);

        FinalePower power = Owner.Creature.GetPower<FinalePower>();
        if (power != null)
        {
            power.Discount = discounted ? HomeRunDiscount : 0;
        }
    }

    /// <summary>升级：费用 -1（伤害与月夜门槛均不变）。</summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
