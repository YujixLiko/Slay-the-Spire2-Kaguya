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
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kaguya.HinaMods.Cards;

/// <summary>
/// 嫌弃（Disgust）
/// 本地化键：KAGUYA-HINA_MODS_MOON_ECLIPSE.title / .description
/// 1费 · 攻击牌 · 普通 · 目标：全体敌人 · 无关键词
/// 卡面：对敌方全体施加{VulnerablePower:diff()}层[gold]易伤[/gold]。
///       [gold]月夜{MoonCost:diff()}[/gold]：对敌方全体造成{Damage:diff()}点伤害。
/// 代码实测：Damage=7；MoonCost=2；VulnerablePower=1
/// 升级：月夜 -1（2→1）；Damage +1（7→8）
/// </summary>
public sealed class Disgust : HinaModsCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(7m, ValueProp.Move),
        new PowerVar<MoonlightPower>(MoonCostVarName, 2m),
        new PowerVar<VulnerablePower>(1m)
    ];

    protected override IEnumerable<IHoverTip> GetCustomHoverTips()
    {
        return new IHoverTip[]
        {
            HoverTipFactory.FromPower<MoonlightPower>(),
            HoverTipFactory.FromPower<VulnerablePower>()
        };
    }

    public Disgust()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies)
    { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);

        if (!cardPlay.IsFirstInSeries)
            return;

        var enemies = Owner.Creature.CombatState?.HittableEnemies;
        if (enemies == null || enemies.Count == 0)
            return;

        // 无条件：对敌方全体施加易伤
        await PowerCmd.Apply<VulnerablePower>(
            choiceContext,
            enemies,
            DynamicVars[nameof(VulnerablePower)].BaseValue,
            Owner.Creature,
            this
        );

        // 月夜2：对敌方全体造成伤害
        if (!await TryConsumeMoonlight(choiceContext, DynamicVars[MoonCostVarName].BaseValue, this))
            return;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars[MoonCostVarName].UpgradeValueBy(-1m);
        DynamicVars.Damage.UpgradeValueBy(1m);
    }
}
