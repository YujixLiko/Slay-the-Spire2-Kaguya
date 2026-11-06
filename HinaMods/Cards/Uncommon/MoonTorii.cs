using BaseLib.Utils;
using Godot;
using Kaguya.HinaMods.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using Kaguya.HinaMods.Cards;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kaguya.HinaMods.Cards.Uncommon;

/// <summary>
/// 月之鸟居（MoonTorii）
/// 本地化键：KAGUYA-HINA_MODS_MOON_BARRIER.title / .description
/// 2费 · 技能牌 · 罕见 · 目标：自身
/// 卡面：获得12点格挡。
///       [gold]月夜5[/gold]：获得2层[gold]荆棘[/gold]，并给予所有敌人2层[gold]虚弱[/gold]。
/// 代码实测：Block=12；MoonCost=5；ThornsPower=2；WeakPower=2
/// 升级：Block +3（12→15）；MoonCost -2（5→3）；荆棘/虚弱层数不变
/// </summary>
public class MoonTorii() : HinaModsCard(2,
    CardType.Skill, CardRarity.Uncommon,
    TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(12m, ValueProp.Move),
        new DynamicVar("MoonCost", 5m),
        new PowerVar<ThornsPower>(2m),
        new PowerVar<WeakPower>(2m)
    ];

    protected override IEnumerable<IHoverTip> GetCustomHoverTips()
    {
        return new IHoverTip[]
        {
           HoverTipFactory.FromPower<MoonlightPower>(),
           HoverTipFactory.FromPower<ThornsPower>(),
           HoverTipFactory.FromPower<WeakPower>(),
        };
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 基础格挡
        await CommonActions.CardBlock(this, cardPlay);

        int moonCost = (int)DynamicVars[MoonCostVarName].BaseValue;

        // 消耗月夜（被「获得+免费」途径取得时 need=0，效果照常触发）
        if (!await TryConsumeMoonlight(choiceContext, moonCost, this))
        {
            return;
        }
        await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);

        // 自己获得荆棘
        await PowerCmd.Apply<ThornsPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(ThornsPower)].BaseValue,
            Owner.Creature,
            this);

        // 所有敌人获得虚弱
        foreach (var enemy in CombatState.HittableEnemies)
        {
            await PowerCmd.Apply<WeakPower>(
                choiceContext,
                enemy,
                DynamicVars[nameof(WeakPower)].BaseValue,
                Owner.Creature,
                this);
        }
    }

    /// <summary>升级：格挡 12 → 15；月夜门槛 5 → 3。</summary>
    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
        DynamicVars["MoonCost"].UpgradeValueBy(-2m);
    }
}
