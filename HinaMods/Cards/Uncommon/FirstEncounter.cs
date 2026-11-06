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

// 月辉协奏 | 0费、技能、自身目标
/// <summary>
/// 初见（FirstEncounter）
/// 本地化键：KAGUYA-HINA_MODS_MOON_CONCERTO.title / .description
/// 0费 · 技能牌 · 罕见 · 目标：自身
/// 卡面：获得1。
///       [gold]月夜2[/gold]：获得1。
/// 代码实测：Energy=1；MoonCost=2
/// 升级：Energy +1
/// </summary>
public sealed class FirstEncounter() : HinaModsCard(0,
    CardType.Skill, CardRarity.Uncommon,
    TargetType.Self)
{
    // 官方标准能量动态变量
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(1),
        new PowerVar<MoonlightPower>(MoonCostVarName, 2m)
    ];

    // 官方能量悬浮提示
    protected override IEnumerable<IHoverTip> GetCustomHoverTips()
    {
        return new IHoverTip[]
        {
            base.EnergyHoverTip,
            HoverTipFactory.FromPower<MoonlightPower>()
        };
    }
    // 核心打出逻辑
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 基础效果：所有打出次数都获得能量（无条件）
        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);

        // 仅【第一次打出】消耗月夜+额外能量
        if (cardPlay.IsFirstInSeries)
        {
            // 月夜2：消耗 2 层月夜后额外获得1点能量（豁免时 need=0，效果照常触发）
            if (await TryConsumeMoonlight(choiceContext, DynamicVars[MoonCostVarName].BaseValue, this))
            {
                await PlayerCmd.GainEnergy(1, Owner);
            }
        }
    }

    // 升级：基础能量 1 → 2
    protected override void OnUpgrade()
    {
        DynamicVars.Energy.UpgradeValueBy(1m);
    }
}