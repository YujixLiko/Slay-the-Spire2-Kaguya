using Godot;
using Kaguya.HinaMods.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Kaguya.HinaMods;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kaguya.HinaMods.Cards;

/// <summary>
/// 归流（ReturnFlow）
/// 本地化键：KAGUYA-MOON_TIDE_RETURN.title / .description
/// 1费 · 技能牌 · 普通 · 目标：自身
/// 卡面：获得2层[gold]月夜[/gold]。
///       [gold]月夜2[/gold]：再获得3层[gold]月夜[/gold]。
/// 代码实测：MoonCost=2；BonusMoon=3；MoonlightPower=2
/// 升级：MoonCost -1
/// </summary>
public sealed class ReturnFlow : HinaModsCard
{
    private const string MoonCostKey = "MoonCost";
    private const string BonusMoonKey = "BonusMoon";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<MoonlightPower>(2m),   // 获得 2 层月夜
        new DynamicVar(MoonCostKey, 1m),    // 月夜1 触发
        new DynamicVar(BonusMoonKey, 2m)    // 再获得 2 层
    ];

    protected override IEnumerable<IHoverTip> GetCustomHoverTips()
    {
        return new IHoverTip[]
        {
            HoverTipFactory.FromPower<MoonlightPower>()
        };
    }

    public ReturnFlow()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);

        await PowerCmd.Apply<MoonlightPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(MoonlightPower)].BaseValue,
            Owner.Creature,
            this
        );

        if (!cardPlay.IsFirstInSeries)
            return;

        if (!await TryConsumeMoonlight(choiceContext, DynamicVars[MoonCostVarName].BaseValue, this))
            return;

        await PowerCmd.Apply<MoonlightPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[BonusMoonKey].BaseValue,
            Owner.Creature,
            this
        );
    }

    /// <summary>升级：获得量 2→3；再获得量 2→3（月夜1 门槛不变）。</summary>
    protected override void OnUpgrade()
    {
        DynamicVars[nameof(MoonlightPower)].UpgradeValueBy(1m);
        DynamicVars[BonusMoonKey].UpgradeValueBy(1m);
    }
}
