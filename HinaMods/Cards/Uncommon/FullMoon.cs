using BaseLib.Utils;
using Kaguya.HinaMods.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kaguya.HinaMods.Cards;

/// <summary>
/// 满月（FullMoon）
/// 本地化键：KAGUYA-FULL_MOON.title / .description
/// 1费 · 技能牌 · 罕见 · 目标：自身 · 无关键词
/// 卡面：[gold]月夜[/gold]{MoonCost:diff()}：使所有能量低于{EnergyTo:diff()}点的[gold]友方[/gold]回复到{EnergyTo:diff()}点能量。
/// 代码实测：MoonCost=5；EnergyTo=2
/// 升级：月夜 5（不变）；EnergyTo 2 → 3
/// 说明：只补差额（当前能量已 ≥3 的友方不受影响），不会把多出来的能量削掉。
/// </summary>
public sealed class FullMoon : HinaModsCard
{
    private const string EnergyToVar = "EnergyTo";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<MoonlightPower>(MoonCostVarName, 5m),
        new DynamicVar(EnergyToVar, 2m)
    ];

    protected override IEnumerable<IHoverTip> GetCustomHoverTips()
    {
        return new IHoverTip[]
        {
            HoverTipFactory.FromPower<MoonlightPower>()
        };
    }

    /// <summary>多人联机专用：单人模式下不会出现在卡池里。</summary>
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

    public FullMoon()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    { }

    /// <summary>
    /// 月夜不足时打不出去（与「集结 / 蓦然回首 / 旗开得胜」同一套门槛写法）。
    /// 两种例外：
    ///   ① 本牌处于「获得+免费 / 药水免费」状态（<see cref="SkipsMoonlightCost"/>）—— 卡面此时是「月夜0」，
    ///      0 月夜也应当能打出去；
    ///   ② 月夜消耗本来就被降成 0（升级/其它效果）。
    /// 免费只作用于**第一次**打出：基类在 <see cref="HinaModsCard.AfterCardPlayed"/> 里还原消耗，
    /// 第二次被带回手牌后就按正常的「月夜5」（升级也还是 5）判断。
    /// </summary>
    /// <summary>满月的月夜是「打出门槛」：不够就打不出去，因此免费取得时应豁免。</summary>
    protected override bool MoonlightAsPlayGate => true;

    protected override bool IsPlayable
    {
        get
        {
            if (!base.IsPlayable)
                return false;

            // 免费状态（卡面显示月夜0）：不需要月夜也能打
            if (SkipsMoonlightCost)
                return true;

            decimal required = DynamicVars[MoonCostVarName].BaseValue;
            if (required <= 0m)
                return true;

            MoonlightPower? moon = Owner?.Creature?.GetPower<MoonlightPower>();
            return moon != null && moon.Amount >= required;
        }
    }

    protected override bool ShouldGlowGoldInternal => IsPlayable;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 免费状态（技能药水 / 获得+免费）下 card 面是「月夜0」：既免门槛也免消耗，扣 0 层
        decimal need = EffectiveMoonCost;
        if (!await TryConsumeMoonlight(choiceContext, need, this))
            return;

        int targetEnergy = (int)DynamicVars[EnergyToVar].BaseValue;
        var combat = CombatState;
        if (combat == null)
            return;

        // 「所有友方」= 联机里同一侧的所有玩家；单人时只有自己一个
        foreach (Player player in combat.Players)
        {
            if (player?.PlayerCombatState == null)
                continue;

            // 只处理「能量低于目标值」的友方：已经达到或超过 3 点的完全不受影响
            int current = player.PlayerCombatState.Energy;
            if (current >= targetEnergy)
                continue;

            await PlayerCmd.GainEnergy(targetEnergy - current, player);
        }
    }

    protected override void OnUpgrade()
    {
        // 月夜消耗不变（5）；只提高回复目标：能量低于 2 → 低于 3
        DynamicVars[EnergyToVar].UpgradeValueBy(1m);
    }
}
