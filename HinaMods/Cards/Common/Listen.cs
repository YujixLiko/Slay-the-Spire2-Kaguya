using BaseLib.Utils;
using Godot;
using Kaguya.HinaMods.Powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using Kaguya.HinaMods;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kaguya.HinaMods.Cards;

/// <summary>
/// 倾听（Listen）
/// 本地化键：KAGUYA-SINGER_HOLD_LINE.title / .description
/// 1费 · 技能牌 · 普通 · 目标：自身 · 关键词：歌声
/// 卡面：获得5点格挡。
///       [gold]月夜2[/gold]：将[gold]弃牌堆[/gold]中的一张牌加入[gold]手牌[/gold]。
/// 代码实测：Block=5；MoonCost=2
/// 升级：Block +3
/// </summary>
public sealed class Listen : HinaModsCard
{
    public override HashSet<string> CustomTags => new HashSet<string>() { CustomCardTags.ECHO };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(5m, ValueProp.Move),
        new PowerVar<MoonlightPower>(MoonCostVarName, 2m)
    ];

    protected override IEnumerable<IHoverTip> GetCustomHoverTips()
    {
        return new IHoverTip[]
        {
            HoverTipFactory.FromPower<MoonlightPower>()
        };
    }

    public Listen()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Player player = Owner;
        if (player == null) return;

        await CreatureCmd.TriggerAnim(player.Creature, "Cast", player.Character.CastAnimDelay);

        await CommonActions.CardBlock(this, cardPlay);

        // 月夜2：把弃牌堆一张牌加入手牌
        if (!await TryConsumeMoonlight(choiceContext, DynamicVars[MoonCostVarName].BaseValue, this))
            return;

        CardPile discardPile = PileType.Discard.GetPile(player);
        List<CardModel> discardCards = discardPile.Cards.ToList();
        if (discardCards.Count == 0)
            return;

        CardSelectorPrefs prefs = new CardSelectorPrefs(SelectionScreenPrompt, 1);
        List<CardModel> selected = (
            await CardSelectCmd.FromSimpleGrid(
                context: choiceContext,
                cardsIn: discardCards,
                player: player,
                prefs: prefs
            )).ToList();

        foreach (CardModel card in selected)
        {
            await CardPileCmd.Add(card, PileType.Hand);
        }
    }

    /// <summary>升级：格挡 5 → 8；月夜门槛 2 → 1。</summary>
    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
        DynamicVars[MoonCostVarName].UpgradeValueBy(-1m);
    }
}
