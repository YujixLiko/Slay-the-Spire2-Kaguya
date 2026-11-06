﻿using BaseLib.Abstracts;
using BaseLib.Audio;
using BaseLib.Extensions;
using BaseLib.Utils;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using Kaguya.HinaMods;
using Kaguya.HinaMods.Powers;
using Kaguya.HinaMods.Character;
using Kaguya.HinaMods.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kaguya.HinaMods.Cards;

/// <summary>
/// 辉夜侧卡牌基类（所有 <c>Kaguya.HinaMods.Cards.*</c> 的父类）。提供：
///   · 【歌声】机制：<see cref="GetResultLocationForCardPlay"/> 让歌声牌打出后进消耗堆，
///     本回合结束时（<see cref="AfterSideTurnEnd"/>）洗回抽牌堆；子类可覆盖
///     <see cref="ShouldArmEcho"/> 控制某次打出是否回音。
///   · 月夜消耗闸门：<c>TryConsumeMoonlight(ctx, need, source)</c> —— need &lt;= 0 直接放行
///     （配合 <c>MarkFreePlay</c> 的免费打出豁免），否则扣 MoonlightPower 层数；
///     耗月夜的动态变量统一命名 <c>MoonCost</c>（基类常量 <c>MoonCostVarName</c>）。
///   · 批量自动打出安全封装（避免歌声牌在 Exhaust→Play→Exhaust 反复搬家导致 UI 脱节）。
/// 基础设施类，无本地化键。
/// </summary>
[Pool(typeof(HinaModsCardPool))]
public abstract class HinaModsCard(int cost, CardType type, CardRarity rarity, TargetType target) :
	CustomCardModel(cost, type, rarity, target)
{
	public virtual HashSet<string> CustomTags { get; } = new HashSet<string>();

	/// <summary>
	/// 待回音标记：本牌【手动】打出时置真，下个回合抽牌前把它从消耗堆洗回抽牌堆后清零。
	/// 自动打出（AutoPlay）的牌不置真，避免与「余音」这类反复打出的效果形成无限循环。
	/// </summary>
	private bool _echoPending;

	public HinaModsCard() : this(0, CardType.Skill, CardRarity.Basic, TargetType.Self)
	{
	}

	/// <summary>
	/// 「获得+免费」标记：由生成方（八方援护 / 星河入梦 / 支援药水 等）在授予本牌「免费打出」时置真。
	/// </summary>
	private bool _freePlayByEffect;

	/// <summary>
	/// 被「获得+免费」置 0 之前的月夜消耗原值；null 表示当前没有处于免费状态。
	/// 打出一次后由 <see cref="ClearFreePlayByEffect"/> 还原。
	/// </summary>
	private decimal? _moonCostBeforeFree;

	/// <summary>本牌是否被「获得+免费」途径显式标记。</summary>
	public bool IsFreePlayByEffect => _freePlayByEffect;

	/// <summary>
	/// 「打出时消耗的月夜层数」动态变量的统一变量名。
	/// 凡是【把月夜当打出代价】的卡都把这个变量命名为 "MoonCost"，基类就能在打「获得+免费」标记时
	/// 把它置 0 —— 卡面随之显示「月夜0」，且打出时不消耗月夜、后续效果照常触发。
	/// ⚠ 只给「消耗型」变量用这个名字；「获得月夜」的变量仍用 MoonlightPower / MoonlightGain 等。
	/// </summary>
	public const string MoonCostVarName = "MoonCost";

	/// <summary>本牌是否为「打出时需要消耗月夜」的卡。</summary>
	public bool UsesMoonlightCost => DynamicVars.ContainsKey(MoonCostVarName);

	/// <summary>
	/// 尝试消耗月夜：need ≤ 0（被「获得+免费」途径取得）时直接返回 true，让后续效果照常触发。
	/// </summary>
	protected async Task<bool> TryConsumeMoonlight(PlayerChoiceContext ctx, decimal need, CardModel source)
	{
		if (need <= 0m)
			return true;

		MoonlightPower moon = Owner?.Creature?.GetPower<MoonlightPower>();
		if (moon == null || moon.Amount < need)
			return false;

		await PowerCmd.ModifyAmount(ctx, moon, -need, Owner.Creature, source);
		return true;
	}

	/// <summary>
	/// 标记本牌为「获得+免费」生成：这一次打出跳过月夜消耗，卡面显示「月夜0」。
	/// ⚠ 只对**第一次**打出有效 —— 打出后由 <see cref="ClearFreePlayByEffect"/> 还原月夜消耗。
	/// </summary>
	public void MarkFreePlayByEffect() => ApplyFreePlayMoonCost();

	/// <summary>
	/// 静态入口：把任意生成出来的卡牌标记为「获得+免费」。
	/// 所有「生成牌并使其免费打出」的途径（八方援护 / 宴会 / 寻忆 / 你和我 / 习惯寂寞 /
	/// 学业压力 / 竹取物语 等）统一调用这里，使这类牌打出时不消耗月夜。
	/// </summary>
	public static void MarkFreePlay(CardModel card)
	{
		if (card is not HinaModsCard modCard)
			return;

		modCard.ApplyFreePlayMoonCost();
	}

	/// <summary>
	/// 进入「获得+免费」状态：记下原月夜消耗并置 0（卡面显示「月夜0」）。
	/// 已经处于免费状态时不重复记录，避免把 0 当原值存进去。
	/// </summary>
	private void ApplyFreePlayMoonCost()
	{
		_freePlayByEffect = true;

		if (!DynamicVars.ContainsKey(MoonCostVarName))
			return;

		DynamicVar costVar = DynamicVars[MoonCostVarName];
		if (costVar.BaseValue <= 0m)
			return;

		_moonCostBeforeFree ??= costVar.BaseValue;
		costVar.BaseValue = 0m;
		Owner?.PlayerCombatState?.RecalculateCardValues();
	}

	/// <summary>
	/// 结束「获得+免费」状态并还原月夜消耗。
	/// 由 <see cref="AfterCardPlayed"/> 在本牌打出后调用 —— 于是免费只作用于**第一次**打出，
	/// 第二次（被歌声/星海等带回手牌后）会按原月夜消耗结算。
	/// </summary>
	public void ClearFreePlayByEffect()
	{
		if (!_freePlayByEffect && _moonCostBeforeFree == null)
			return;

		_freePlayByEffect = false;

		// 还原 = 被置 0 前的原值 + 免费期间发生的改动（升级会写在已置 0 的变量上，5→0→3）
		if (_moonCostBeforeFree is decimal original && DynamicVars.ContainsKey(MoonCostVarName))
			DynamicVars[MoonCostVarName].BaseValue = original + DynamicVars[MoonCostVarName].BaseValue;

		_moonCostBeforeFree = null;
		Owner?.PlayerCombatState?.RecalculateCardValues();
	}

	/// <summary>
	/// 本牌的月夜是否属于「**打出门槛**」而非「**效果消耗**」。
	/// <para>
	/// 门槛卡（集结 / 满月）没有足够月夜就完全打不出去，所以被「免费打出」途径取得时
	/// 必须豁免月夜，否则这张牌在手里永远无法使用。
	/// </para>
	/// <para>
	/// 其余月夜牌（反击开始、卸力、终幕 …）的月夜是**效果代价**，
	/// 无论从哪个渠道获得（癫狂之触 / 欧洛巴斯之酸 / 液态记忆 / 攻击药水 …）都要照扣。
	/// </para>
	/// </summary>
	protected virtual bool MoonlightAsPlayGate => false;

	/// <summary>
	/// 本牌本次打出是否豁免月夜消耗。
	/// <para>
	/// **只有「门槛卡」+「生成方显式标记的免费打出」两个条件同时成立才豁免。**
	/// </para>
	/// <para>
	/// ⚠ 不要把「本回合费用被降到 0」也当成免费：引擎的 <c>LocalCostModifier</c>
	/// 不记录来源，「液态记忆」（仅改费用）与「欧洛巴斯之酸」（本回合免费）数据上完全一致，
	/// 无法区分。若一律按 0 费豁免，则「反击开始」这类「减费 + 返回手牌」的牌
	/// 会在费用归零后不再扣月夜，形成无限返回循环。
	/// </para>
	/// </summary>
	public bool SkipsMoonlightCost => MoonlightAsPlayGate && _freePlayByEffect;

	/// <summary>
	/// 本次打出**实际要扣**的月夜层数：被「生成方显式标记为免费」时为 0，否则取 MoonCost 变量。
	/// 所有「月夜N：」的牌都应通过它调用 <see cref="TryConsumeMoonlight"/>，
	/// 否则免费状态下（变量仍是 N）会照扣层数。
	/// <para>
	/// 注意：外部来源（药水、事件、其它 mod）把费用改成 0 **不会**让这里归零，
	/// 月夜照扣 —— 见 <see cref="SkipsMoonlightCost"/> 的说明。
	/// </para>
	/// </summary>
	protected decimal EffectiveMoonCost =>
		SkipsMoonlightCost ? 0m : (DynamicVars.ContainsKey(MoonCostVarName) ? DynamicVars[MoonCostVarName].BaseValue : 0m);

	/// <summary>
	/// 这张牌在当前 run 里是否允许出现（联机 / 单人专用约束）。
	/// ⚠ 引擎只在 <c>CardPoolModel.GetUnlockedCards</c> 里过滤（sts2:67371），
	///   所以凡是「自己扫 ModelDb.AllCards 拼池子」的地方都必须显式调这个，否则
	///   单人模式会拿到 MultiplayerOnly 卡（月落 / 你和我 曾经就是这样漏出去的）。
	/// </summary>
	public static bool AllowedInRun(CardModel? card, Player? player)
	{
		if (card == null)
			return false;

		CardMultiplayerConstraint constraint = card.MultiplayerConstraint;
		if (constraint == CardMultiplayerConstraint.None)
			return true;

		// 拿不到 run 状态时放行（宁可少拦，也不要因为空引用把整池清空）
		return player?.RunState != null && constraint == player.RunState.CardMultiplayerConstraint;
	}

	/// <summary>
	/// 是否拥有【回音】：只有显式带 ECHO 标签的牌才拥有回音。
	/// 子类可覆盖（例如「旋转打击」在消耗月夜之后才获得回音）。
	/// </summary>
	public virtual bool HasEcho => CustomTags.Contains(CustomCardTags.ECHO);

	/// <summary>是否拥有【支援】标签（决定支援牌本体的机制：消耗+虚无、以及打出时的临时力量/敏捷）。</summary>
	public bool HasSupportTag => CustomTags.Contains(CustomCardTags.SUPPORT);

	/// <summary>
	/// 子类可覆盖：本次打出后是否还要为回音待命。
	/// 返回 false 表示这张牌这一次不回音（进消耗堆后不再回来）。
	/// </summary>
	protected virtual bool ShouldArmEcho(CardPlay cardPlay) => true;

	// 支援牌自带消耗 + 虚无，其余沿用官方原生关键词
	public override List<CardKeyword> CanonicalKeywords
	{
		get
		{
			List<CardKeyword> keywords =
				CustomTags.Contains(CustomCardTags.SUPPORT)
				? [
					CardKeyword.Exhaust,
					CardKeyword.Ethereal
				]
				: base.CanonicalKeywords.ToList();

			// 回音：走 BaseLib 自定义关键词（KaguyaKeywords.Echo），与原生「消耗」同一条渲染路径，
			// 卡牌描述末尾会自动追加一行「回音」，子类无需各自声明。
			if (HasEcho)
				keywords.Add(KaguyaKeywords.Echo);

			return keywords;
		}
	}

	/// <summary>
	/// 密封重写，禁止子类覆盖，保证基类逻辑永不失效。
	/// 合并顺序：官方原生提示 → 子类自定义提示 → 标签提示（回音/支援/月见）
	/// </summary>
	protected sealed override IEnumerable<IHoverTip> ExtraHoverTips
	{
		get
		{
			// 1. 官方原生悬浮提示
			foreach (var tip in base.ExtraHoverTips)
				yield return tip;

			// 2. 子类自定义悬浮提示（易伤/力量/敏捷等）
			foreach (var tip in GetCustomHoverTips())
				yield return tip;

			// 3. 标签悬浮提示（回音已由 KaguyaKeywords.Echo 关键词自动提供，勿在此重复挂）
			if (CustomTags.Contains(CustomCardTags.SUPPORT))
			{
				yield return new HoverTip(
					new LocString("card_keywords", "SUPPORT.title"),
					new LocString("card_keywords", "SUPPORT.description")
				);
			}
			if (CustomTags.Contains(CustomCardTags.SUPPORTCARD))
			{
				yield return new HoverTip(
					new LocString("card_keywords", "SUPPORTCARD.title"),
					new LocString("card_keywords", "SUPPORTCARD.description")
				);
			}
		}
	}

	/// <summary>
	/// 子类专用：重写此方法添加自定义悬浮提示。
	/// </summary>
	protected virtual IEnumerable<IHoverTip> GetCustomHoverTips()
	{
		return Enumerable.Empty<IHoverTip>();
	}

	// ================================================================================
	// 回音 · 第一步：打出后进入消耗堆
	// ================================================================================
	protected override CardLocation GetResultLocationForCardPlay()
	{
		if (HasEcho)
		{
			return new CardLocation(Owner, PileType.Exhaust, CardPilePosition.Bottom);
		}

		return base.GetResultLocationForCardPlay();
	}

	/// <summary>
	/// 出牌瞬间播攻击音。官方是攻击动画一开始就响（CreatureCmd.TriggerAnim → SfxCmd.Play），
	/// 不是等结算结束。多段攻击牌（舞步、决死一击等）中间还有 CustomScaledWait，
	/// 若挂在 AfterCardPlayed 会明显滞后。
	/// </summary>
	public override Task BeforeCardPlayed(CardPlay cardPlay)
	{
		if (ReferenceEquals(cardPlay.Card, this))
			PlayCardSfx(cardPlay);
		return base.BeforeCardPlayed(cardPlay);
	}

	/// <summary>
	/// 标记本牌本回合被【手动】打出，等待下个回合抽牌前回音。
	/// </summary>
	public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await base.AfterCardPlayed(choiceContext, cardPlay);

		// 音效已移到 BeforeCardPlayed（出牌瞬间响，不再等结算完）

		// 「获得+免费」只免第一次打出：本牌打出后立刻把月夜消耗还原。
		// 不还原的话，这张牌被歌声/星海/蓦然回首等带回手牌后，第二次打出仍然是月夜0。
		if (ReferenceEquals(cardPlay.Card, this))
			ClearFreePlayByEffect();

		await GrantSupportBonus(choiceContext, cardPlay);

		if (cardPlay.Card != this || cardPlay.IsAutoPlay || !HasEcho || !ShouldArmEcho(cardPlay))
			return;

		_echoPending = true;
	}

	// ================================================================================
	// 打出音效
	//   完全对齐官方：每个角色只有 **一个** 攻击音
	//   （CharacterModel.AttackSfx → CreatureCmd.TriggerAnim("Attack") → SfxCmd.Play），
	//   技能牌 / 能力牌都不发声。强度变化官方走 FMOD 参数渐变、不换样本，
	//   这里用 pitchVariation 近似。
	// ================================================================================
	private const string SfxAttack = "res://images/hinamods/audio/kaguya_attack.ogg";

	/// <summary>
	/// 多段攻击的「每段命中音」（短版 0.35s）。
	/// ⚠ WithHitFx 的 tmpSfx 参数只认文件名：引擎会拼成 res://debug_audio/&lt;名字&gt;
	/// （TmpSfx.GetPath，sts2:193366），所以文件必须放在工程的 debug_audio/ 目录里。
	/// </summary>
	protected const string SfxHitName = "kaguya_hit.ogg";

	/// <summary>
	/// 循环结算伤害的卡（Annihilation / DanceSteps / WayHome）每段命中后调用。
	/// 走 DamageCmd 多段（WithHitCount）的卡不用它，引擎会自动按段播 SfxHitName。
	/// </summary>
	protected static void PlayHitSfx()
	{
		ModAudio.PlaySound("res://debug_audio/" + SfxHitName, pitchVariation: 0.05f);
	}

	private void PlayCardSfx(CardPlay cardPlay)
	{
		// 一次「打出」只响一声：重放（playCount &gt; 1）只在第一拍响；
		// 自动打出（回音/批量结算）不响，否则一串牌会同时炸成噪音。
		if (cardPlay.PlayIndex > 0 || cardPlay.IsAutoPlay)
			return;

		if (Type == CardType.Attack)
			ModAudio.PlaySound(SfxAttack, pitchVariation: 0.04f);
	}

	// ================================================================================
	// 支援牌联动：token 支援牌打出时给正面 buff
	// ================================================================================
	/// <summary>
	/// 打出【衍生无色 token 支援牌】时：
	///   攻击牌 → 获得 1 层临时力量（本回合内，回合结束失去，对标官方「预备打击」）；
	///   技能牌 → 获得 1 层临时敏捷（本回合内，回合结束失去，对标官方「预判」）。
	/// 若身上有【交换武器】(<see cref="WeaponSwapPower"/>)，则改为直接给永久力量 / 永久敏捷。
	/// </summary>
	private async Task GrantSupportBonus(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (cardPlay.Card != this) return;
		if (Rarity != CardRarity.Token) return;
		if (!HasSupportTag) return;
		if (Owner?.Creature is not Creature self || !self.IsAlive) return;

		// 交换武器：本场战斗中把「临时」变为「永久」
		bool permanent = self.GetPower<WeaponSwapPower>() != null;

		if (Type == CardType.Attack)
		{
			if (permanent)
				await PowerCmd.Apply<StrengthPower>(choiceContext, self, 1m, self, this);
			else
				await PowerCmd.Apply<SupportStrengthPower>(choiceContext, self, 1m, self, this);
		}
		else if (Type == CardType.Skill)
		{
			if (permanent)
				await PowerCmd.Apply<DexterityPower>(choiceContext, self, 1m, self, this);
			else
				await PowerCmd.Apply<SupportDexterityPower>(choiceContext, self, 1m, self, this);
		}
	}

	// ================================================================================
	// 回音 · 第二步：**本回合结束时**（自己这一侧的回合结束）随机洗回抽牌堆
	// 与旧版的区别：旧版放在 BeforeHandDraw（下回合抽牌前），现在提前到回合结束，
	// 于是歌声牌在「敌方回合期间」就已经躺在抽牌堆里（不再占着消耗堆）。
	// ⚠ 已知影响（接受该取舍才改）：
	//    · 星海 / 闭幕 这类「读消耗堆」的效果，下回合开始时看不到上一回合的歌声牌；
	//    · 官方的 DarkEmbracePower（回合结束按虚无牌数抽牌）若排在回洗之后，
	//      可能把歌声牌同回合抽回（多打一张）。
	// ================================================================================
	public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
	{
		await base.AfterSideTurnEnd(choiceContext, side, participants);

		if (!HasEcho)
			return;

		// AfterSideTurnEnd 对双方各广播一次：只在自己这一侧结束时处理。
		// （旧版用的是「Owner == player」，那是 BeforeHandDraw 的归属判断，这里换成 side 判断。）
		if (Owner?.Creature == null || side != Owner.Creature.Side)
			return;

		if (!_echoPending)
			return;

		_echoPending = false;   // 先清标记，避免同一张牌被重复回洗

		// ── 唯一性守卫（歌声容易出 bug 的地方，三条都要留着）──
		// ① 必须是「这一张实例」还躺在消耗堆里：若它已经被其它效果搬到手牌/弃牌堆/抽牌堆，
		//    就不要再凭空造一张出来，否则会出现「同一张牌存在两份」。
		if (Pile == null || Pile.Type != PileType.Exhaust)
			return;

		// ② 抽牌堆里已经有一份的话，绝不能重复加入（防止同一实例被塞两次）
		CardPile drawPile = PileType.Draw.GetPile(Owner);
		if (drawPile.Cards.Any(c => ReferenceEquals(c, this)))
			return;

		// ③ 位置仍然用 Random（按需求保留）
		await CardPileCmd.Add(this, PileType.Draw, CardPilePosition.Random);
	}

	// ================================================================================
	// 批量自动打出的安全封装
	// ================================================================================
	/// <summary>
	/// 安全地「批量自动打出」一组牌。
	///
	/// 【为什么必须走这个，而不是直接 CardCmd.AutoPlay 循环】
	/// 引擎的 CardCmd.AutoPlay 在牌「已不在 Play 堆」时会做两件危险的事：
	///   ① 若 card.Pile == null 才补 Add(Play) —— 已在 Exhaust/Draw 堆的牌不会被搬运，
	///      紧接着 OnPlayWrapper 里的 Add(Play) 才真正把它搬走；
	///   ② skipCardPileVisuals=true 时，引擎完全跳过 NCard 节点的迁移与销毁。
	///      而本 mod 的【歌声】机制让每一张歌声牌的 GetResultLocationForCardPlay()
	///      恒返回 Exhaust —— 于是这些牌在「Exhaust → Play → Exhaust」之间反复搬家，
	///      节点却没跟着搬：UI 上会残留「有占位、但点不亮也拖不动」的空白卡，
	///      手牌区与 Play 区的显示就和真实牌堆脱节了。
	/// 官方 CardPileCmd.AutoPlayFromDrawPile 用的正是「先全部搬到 Play，再逐张默认参数
	/// AutoPlay」的两段式写法，三色流光/毕业演出/蓦然回首全部照此重写。
	///
	/// 【额外做的防护】
	///   · 快照时剔除已离场/已移除的牌（HasBeenRemovedFromState）—— 能力牌打出后
	///     会 RemoveFromCombat，若此时被重打，引擎会抛 InvalidOperationException。
	///   · 第一段搬运用 skipVisuals:false（让节点正常迁移），第二段用默认参数
	///     （skipCardPileVisuals:false），全程不跳视觉，节点与牌堆始终一致。
	///   · 循环中每张都检查战斗是否已结束 / 拥有者是否死亡。
	/// </summary>
	/// <param name="choiceContext">玩家选择上下文。</param>
	/// <param name="cards">要打出的牌（内部会先做快照，不要求调用方已 ToList）。</param>
	/// <param name="target">目标；为 null 时由引擎按牌自身 TargetType 自行选取。</param>
	public static async Task PlayCardsSafely(
		PlayerChoiceContext choiceContext,
		IEnumerable<CardModel> cards,
		Creature? target = null)
	{
		if (CombatManager.Instance.IsOverOrEnding)
			return;

		// 快照：先剔除已经不在战斗中 / 已被移除的牌，避免引擎 Add 时抛异常
		List<CardModel> snapshot = cards
			.Where(c => c != null
			            && !c.HasBeenRemovedFromState
			            && c.Owner != null
			            && !c.Owner.Creature.IsDead
			            && c.Owner.Creature.CombatState != null
			            && c.Owner.Creature.CombatState.ContainsCard(c))
			.ToList();

		if (snapshot.Count == 0)
			return;

		// 第一段：整批先搬进 Play 堆（走完整视觉，节点同步迁移）
		foreach (CardModel card in snapshot)
		{
			if (card.Pile?.Type == PileType.Play)
				continue;

			await CardPileCmd.Add(card, PileType.Play);
		}

		// 第二段：逐张自动打出（默认参数，不跳视觉）
		foreach (CardModel card in snapshot)
		{
			if (CombatManager.Instance.IsOverOrEnding)
				break;

			if (card.Owner == null || card.Owner.Creature.IsDead)
				break;

			// 打出过程中可能已被其它效果移出战斗（例如打出后被消耗 / 被移除）
			if (card.HasBeenRemovedFromState || card.Pile == null)
				continue;

			// 目标类型不兼容时（自身/全体/无目标）不要硬塞目标，交给引擎处理
			Creature? effectiveTarget = card.TargetType is TargetType.AnyEnemy or TargetType.RandomEnemy
				? target
				: null;

			await CardCmd.AutoPlay(choiceContext, card, effectiveTarget);
		}
	}

	/// <summary>
	/// 判定一张「已被自动打出过」的牌此刻是否仍可被再次自动打出。
	/// 能力牌打出后会 RemoveFromCombat（HasBeenRemovedFromState = true），
	/// 此时再 AutoPlay 会让引擎抛 InvalidOperationException。
	/// </summary>
	public static bool CanReplayAutomatically(CardModel? card)
	{
		if (card == null || card.HasBeenRemovedFromState)
			return false;

		if (card.Owner == null || card.Owner.Creature.IsDead)
			return false;

		ICombatState? combatState = card.Owner.Creature.CombatState;
		return combatState != null && combatState.ContainsCard(card);
	}

	// ================================================================================
	// 普通预览复用现有专属大图，避免落到缺失的小图路径。
	// ================================================================================
	public override string CustomPortraitPath
	{
		get
		{
			var path = $"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".BigCardImagePath();
			return ResourceLoader.Exists(path) ? path : "card.png".BigCardImagePath();
		}
	}

	public override string PortraitPath
	{
		get
		{
			return CustomPortraitPath;
		}
	}

	public override string BetaPortraitPath
	{
		get
		{
			var path = $"Beta/{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".CardImagePath();
			return ResourceLoader.Exists(path) ? path : PortraitPath;
		}
	}
}
