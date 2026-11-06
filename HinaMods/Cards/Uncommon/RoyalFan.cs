using BaseLib.Utils;
using Godot;
using Kaguya.HinaMods.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kaguya.HinaMods.Cards;

/// <summary>
/// 御扇（RoyalFan）
/// 本地化键：KAGUYA-MOON_LIGHT_ARIA.title / .description
/// 1费 · 技能牌 · 普通 · 目标：自身
/// 卡面：将1张[gold]升级过[/gold]的[blue]招架[/blue]加入[gold]手牌[/gold]。
///       [gold]月夜2[/gold]：使其费用 -1。
/// 代码实测：MoonCost=2
/// 升级：无
/// </summary>
public sealed class RoyalFan : HinaModsCard
{

    /// <summary>支援牌标签（本牌会获得支援牌）。</summary>
    public override HashSet<string> CustomTags => new HashSet<string>() { CustomCardTags.SUPPORTCARD };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<MoonlightPower>(MoonCostVarName, 2m)
    ];

    protected override IEnumerable<IHoverTip> GetCustomHoverTips()
    {
        return new IHoverTip[]
        {
            HoverTipFactory.FromCard<SupportParry>(),
            HoverTipFactory.FromPower<MoonlightPower>()
        };
    }

    public RoyalFan()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardModel newHandCard = CardScope.CreateCard<SupportParry>(Owner);
        if (IsUpgraded)
        {
            CardCmd.Upgrade(newHandCard);
        }

        if (cardPlay.IsFirstInSeries)
        {
            if (await TryConsumeMoonlight(choiceContext, DynamicVars[MoonCostVarName].BaseValue, this))
            {
                newHandCard.EnergyCost.AddThisCombat(-1);
            }
        }

        await CardPileCmd.AddGeneratedCardToCombat(newHandCard, PileType.Hand, Owner);
    }

    protected override void OnUpgrade()
    {
    }
}
