using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Ftue;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;

public partial class NTsukitoBosatsuFtue : NFtue
{
	public const string id = "tsukito_bosatsu_ftue";

	/// <summary>
	/// v0.114.0 新增的抽象成员：记录「已看过」的存档 ID。
	/// 返回 id → 只在首次进入时显示一次；官方 NAcceptTutorialsFtue 返回 null 表示每次都显示。
	/// </summary>
	protected override string? SeenOnShowId => id;

	private static readonly string[] _imagePaths = new[]
	{
		"res://images/packed/ftue/BosatsuFtue_01.png",
		"res://images/packed/ftue/BosatsuFtue_02.png",
		"res://images/packed/ftue/BosatsuFtue_03.png",
	};

	private TextureRect _image;
	private MegaRichTextLabel _bodyText;
	private Label _pageCount;
	private Label _header;
	private NButton _prevButton;
	private NButton _nextButton;

	private int _currentPage = 1;
	private const int _totalPages = 3;

	private Vector2 _imagePosition;
	private Vector2 _textPosition;
	private Tween? _pageTurnTween;
	private static readonly Vector2 _imageAnimOffset = new Vector2(200f, 0f);

	public static NTsukitoBosatsuFtue Create()
	{
		if (TestMode.IsOn) return null;
		GD.Print("[FTUE] Trying GD.Load on TSCN...");
		var scene = GD.Load<PackedScene>("res://scenes/ftue/tsukito_bosatsu_ftue.tscn");
		if (scene != null)
		{
			GD.Print("[FTUE] TSCN loaded OK, instantiating...");
			var ftue = scene.Instantiate<NTsukitoBosatsuFtue>(PackedScene.GenEditState.Disabled);
			GD.Print($"[FTUE] Instantiate: {(ftue != null ? "OK" : "NULL")}");
			if (ftue != null) return ftue;
		}
		else
		{
			GD.Print("[FTUE] GD.Load returned NULL for TSCN! Falling back to code UI.");
		}
		var fallback = new NTsukitoBosatsuFtue();
		fallback.BuildUI();
		return fallback;
	}

	private void BuildUI()
	{
		LayoutMode = 3;
		AnchorRight = 1.0f;
		AnchorBottom = 1.0f;
		GrowHorizontal = GrowDirection.Both;
		GrowVertical = GrowDirection.Both;

		_image = new TextureRect();
		_image.Name = "Image";
		_image.CustomMinimumSize = new Vector2(671, 512);
		_image.LayoutMode = 1;
		_image.AnchorLeft = 0.5f; _image.AnchorTop = 0.5f; _image.AnchorRight = 0.5f; _image.AnchorBottom = 0.5f;
		_image.OffsetLeft = -668f; _image.OffsetTop = -288f; _image.OffsetRight = 3f; _image.OffsetBottom = 224f;
		_image.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		_image.Texture = GD.Load<Texture2D>(_imagePaths[0]);
		_image.Modulate = Colors.Transparent;
		AddChild(_image);

		_header = new Label();
		_header.Name = "Header";
		_header.CustomMinimumSize = new Vector2(1200, 60);
		_header.LayoutMode = 1;
		_header.AnchorLeft = 0.5f; _header.AnchorTop = 0.5f; _header.AnchorRight = 0.5f; _header.AnchorBottom = 0.5f;
		_header.OffsetLeft = -600f; _header.OffsetTop = 281f; _header.OffsetRight = 600f; _header.OffsetBottom = 341f;
		_header.AddThemeColorOverride("font_color", new Color(0.937255f, 0.784314f, 0.317647f, 1f));
		_header.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.501961f));
		_header.AddThemeConstantOverride("shadow_offset_x", 3);
		_header.AddThemeConstantOverride("shadow_offset_y", 2);
		_header.AddThemeFontSizeOverride("font_size", 28);
		_header.HorizontalAlignment = HorizontalAlignment.Center;
		_header.VerticalAlignment = VerticalAlignment.Center;
		_header.Visible = false;
		AddChild(_header);

		_pageCount = new Label();
		_pageCount.Name = "PageCount";
		_pageCount.CustomMinimumSize = new Vector2(1200, 60);
		_pageCount.LayoutMode = 1;
		_pageCount.AnchorLeft = 0.5f; _pageCount.AnchorTop = 0.5f; _pageCount.AnchorRight = 0.5f; _pageCount.AnchorBottom = 0.5f;
		_pageCount.OffsetLeft = -600f; _pageCount.OffsetTop = 314f; _pageCount.OffsetRight = 600f; _pageCount.OffsetBottom = 374f;
		_pageCount.AddThemeColorOverride("font_color", new Color(0.529412f, 0.807843f, 0.921569f, 1f));
		_pageCount.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.501961f));
		_pageCount.AddThemeConstantOverride("shadow_offset_x", 3);
		_pageCount.AddThemeConstantOverride("shadow_offset_y", 2);
		_pageCount.AddThemeFontSizeOverride("font_size", 24);
		_pageCount.HorizontalAlignment = HorizontalAlignment.Center;
		_pageCount.VerticalAlignment = VerticalAlignment.Center;
		_pageCount.Visible = false;
		AddChild(_pageCount);

		_bodyText = new MegaRichTextLabel();
		_bodyText.Name = "Description";
		_bodyText.LayoutMode = 1;
		_bodyText.AnchorLeft = 0.5f; _bodyText.AnchorTop = 0.5f; _bodyText.AnchorRight = 0.5f; _bodyText.AnchorBottom = 0.5f;
		_bodyText.OffsetLeft = 45f; _bodyText.OffsetTop = -269f; _bodyText.OffsetRight = 668f; _bodyText.OffsetBottom = 214f;
		_bodyText.AddThemeColorOverride("default_color", new Color(1, 0.964706f, 0.886275f, 1));
		_bodyText.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.501961f));
		_bodyText.AddThemeConstantOverride("line_separation", -2);
		_bodyText.AddThemeConstantOverride("shadow_offset_x", 3);
		_bodyText.AddThemeConstantOverride("shadow_offset_y", 2);
		_bodyText.AddThemeFontSizeOverride("normal_font_size", 28);
		_bodyText.AddThemeFontSizeOverride("bold_font_size", 28);
		_bodyText.AddThemeFontSizeOverride("bold_italics_font_size", 28);
		_bodyText.AddThemeFontSizeOverride("italics_font_size", 28);
		_bodyText.AddThemeFontSizeOverride("mono_font_size", 28);
		_bodyText.BbcodeEnabled = true;
		_bodyText.VerticalAlignment = VerticalAlignment.Center;
		_bodyText.VisibleCharactersBehavior = TextServer.VisibleCharactersBehavior.CharsAfterShaping;
		_bodyText.Modulate = Colors.Transparent;
		AddChild(_bodyText);

		_prevButton = new NButton();
		_prevButton.Name = "LeftArrow"; _prevButton.Visible = false;
		_prevButton.AnchorTop = 0.5f; _prevButton.AnchorBottom = 0.5f;
		_prevButton.OffsetLeft = 40f; _prevButton.OffsetTop = -64f; _prevButton.OffsetRight = 168f; _prevButton.OffsetBottom = 64f;
		_prevButton.PivotOffset = new Vector2(64, 64);
		var la = new TextureRect();
		la.Texture = GD.Load<Texture2D>("res://images/packed/common_ui/settings_tiny_left_arrow.png");
		la.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		la.StretchMode = TextureRect.StretchModeEnum.KeepCentered;
		la.AnchorRight = 1f; la.AnchorBottom = 1f;
		la.MouseFilter = Control.MouseFilterEnum.Ignore;
		la.PivotOffset = new Vector2(64, 64);
		_prevButton.AddChild(la);
		_prevButton.Disable();
		AddChild(_prevButton);

		_nextButton = new NButton();
		_nextButton.Name = "RightArrow";
		_nextButton.AnchorLeft = 1f; _nextButton.AnchorTop = 0.5f; _nextButton.AnchorRight = 1f; _nextButton.AnchorBottom = 0.5f;
		_nextButton.OffsetLeft = -168f; _nextButton.OffsetTop = -64f; _nextButton.OffsetRight = -40f; _nextButton.OffsetBottom = 64f;
		_nextButton.PivotOffset = new Vector2(64, 64);
		var ra = new TextureRect();
		ra.Texture = GD.Load<Texture2D>("res://images/packed/common_ui/settings_tiny_right_arrow.png");
		ra.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		ra.StretchMode = TextureRect.StretchModeEnum.KeepCentered;
		ra.AnchorRight = 1f; ra.AnchorBottom = 1f;
		ra.MouseFilter = Control.MouseFilterEnum.Ignore;
		ra.PivotOffset = new Vector2(64, 64);
		_nextButton.AddChild(ra);
		_nextButton.Disable(); _nextButton.Visible = false;
		AddChild(_nextButton);

		_prevButton.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(ToggleLeft));
		_nextButton.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(ToggleRight));
	}

	public void Start()
	{
		NModalContainer.Instance?.ShowBackstop();
		_currentPage = 1;
		_image.Texture = GD.Load<Texture2D>(_imagePaths[0]);
		_imagePosition = _image.Position;
		_bodyText.SetTextAutoSize(new LocString("ftues", "TSUKITO_BOSATSU_FTUE_BODY_1").GetFormattedText());
		_bodyText.Modulate = Colors.White;
		_textPosition = _bodyText.Position;
		var locStr = new LocString("ftues", "TSUKITO_BOSATSU_FTUE_PAGE_COUNT");
		locStr.Add("totalPages", _totalPages); locStr.Add("currentPage", _currentPage);
		_pageCount.Text = locStr.GetFormattedText();
		_header.Text = new LocString("ftues", "TSUKITO_BOSATSU_FTUE_HEADER_1").GetFormattedText();
		_nextButton.Visible = true; _nextButton.Enable();
		_pageCount.Visible = true; _header.Visible = true;
		_pageTurnTween = CreateTween().SetParallel();
		_pageTurnTween.TweenProperty(_image, "modulate:a", 1f, 0.5).From(0f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		_pageTurnTween.TweenProperty(_bodyText, "modulate:a", 1f, 1.0).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Linear);
		_pageTurnTween.TweenProperty(_bodyText, "visible_ratio", 1f, 0.6).From(0f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Sine);
	}

	public override void _Input(InputEvent inputEvent)
	{
		if (!IsVisibleInTree()) return;
		Control c = GetViewport().GuiGetFocusOwner();
		if (!(c is TextEdit) && !(c is LineEdit))
		{
			if (inputEvent.IsActionPressed("ui_left") && _prevButton.IsEnabled) ToggleLeft(_prevButton);
			if (inputEvent.IsActionPressed("ui_right") && _nextButton.IsEnabled) ToggleRight(_nextButton);
		}
	}

	private void ToggleLeft(NButton _)
	{
		_currentPage--; ApplyPage();
		_pageTurnTween?.Kill();
		_pageTurnTween = CreateTween().SetParallel();
		_pageTurnTween.TweenProperty(_image, "modulate:a", 1f, 0.5).From(0.5f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		_pageTurnTween.TweenProperty(_image, "position", _imagePosition, 0.5).From(_imagePosition - _imageAnimOffset).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
		_pageTurnTween.TweenProperty(_bodyText, "position", _textPosition, 0.5).From(_textPosition - _imageAnimOffset).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
		_pageTurnTween.TweenProperty(_bodyText, "modulate:a", 1f, 0.6).From(0f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Linear);
		_pageTurnTween.TweenProperty(_bodyText, "visible_ratio", 1f, 0.6).From(0f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Sine);
	}

	private void ToggleRight(NButton _)
	{
		if (_currentPage == _totalPages) { _pageTurnTween?.Kill(); SaveManager.Instance.MarkFtueAsComplete(id); CloseFtue(); return; }
		_currentPage++; ApplyPage();
		_pageTurnTween?.Kill();
		_pageTurnTween = CreateTween().SetParallel();
		_pageTurnTween.TweenProperty(_image, "modulate:a", 1f, 0.5).From(0.5f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		_pageTurnTween.TweenProperty(_image, "position", _imagePosition, 0.5).From(_imagePosition + _imageAnimOffset).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
		_pageTurnTween.TweenProperty(_bodyText, "position", _textPosition, 0.5).From(_textPosition + _imageAnimOffset).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
		_pageTurnTween.TweenProperty(_bodyText, "modulate:a", 1f, 0.6).From(0f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Linear);
		_pageTurnTween.TweenProperty(_bodyText, "visible_ratio", 1f, 0.6).From(0f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Sine);
	}

	private void ApplyPage()
	{
		_image.Texture = GD.Load<Texture2D>(_imagePaths[_currentPage - 1]);
		_prevButton.Visible = _currentPage > 1;
		if (_currentPage > 1) _prevButton.Enable(); else _prevButton.Disable();
		switch (_currentPage)
		{
			case 1: _bodyText.SetTextAutoSize(new LocString("ftues", "TSUKITO_BOSATSU_FTUE_BODY_1").GetFormattedText()); _header.Text = new LocString("ftues", "TSUKITO_BOSATSU_FTUE_HEADER_1").GetFormattedText(); break;
			case 2: _bodyText.SetTextAutoSize(new LocString("ftues", "TSUKITO_BOSATSU_FTUE_BODY_2").GetFormattedText()); _header.Text = new LocString("ftues", "TSUKITO_BOSATSU_FTUE_HEADER_2").GetFormattedText(); break;
			case 3: _bodyText.SetTextAutoSize(new LocString("ftues", "TSUKITO_BOSATSU_FTUE_BODY_3").GetFormattedText()); _header.Text = new LocString("ftues", "TSUKITO_BOSATSU_FTUE_HEADER_3").GetFormattedText(); break;
		}
		var ls = new LocString("ftues", "TSUKITO_BOSATSU_FTUE_PAGE_COUNT");
		ls.Add("totalPages", _totalPages); ls.Add("currentPage", _currentPage);
		_pageCount.Text = ls.GetFormattedText();
	}
}
