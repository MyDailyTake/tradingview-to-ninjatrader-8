#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

// NT8 Version of Bayesian Trend Indicator
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the Mozilla Public License 2.0 (MPL 2.0).
// The original Pine Script™ code is by ChartPrime and can be found at: https://www.tradingview.com/script/rVEhAQDO-Bayesian-Trend-Indicator-ChartPrime/
// Adaptation for NinjaTrader by jack@mydailytake.com
// Write-up + downloads: https://mydailytake.com/bayesian-trend-indicator-chartprime-ninjatrader-8/
// Source + all conversions: https://github.com/MyDailyTake/tradingview-to-ninjatrader-8
// © 2026 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the Mozilla Public License 2.0. Full license details at https://mozilla.org/MPL/2.0/
// The use of ChartPrime name or its adapted code in this work does not imply endorsement by the original authors.
//
// Notes:
//   Combines four trend votes (SMA, EMA, DEMA, VWMA) at two lengths (slow + fast) into a posterior
//   probability of an uptrend via Bayes' theorem. Each vote is a smoothed gradient score in [0..1].
//   Bars are recolored by the posterior, with a soft fade to the chart background between 0.48-0.52.
//   Optional HUD table shows the per-MA contributions; optional per-bar text labels show the
//   posterior value. Diamond markers print at crossings of 0.5.
//
//   Non-repainting: all calculations use closed-bar data; bar coloring is applied on close.
//
//   Public Series outputs: PosteriorUp.

#region Enums BayesianTrend

public enum BayesianTrend_TablePosition
{
	TopLeft,
	TopRight,
	BottomLeft,
	BottomRight
}

#endregion

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	#region Categories
	[Gui.CategoryOrder("Indicator Setup",	10100)]
	[Gui.CategoryOrder("Display",			10200)]
	[Gui.CategoryOrder("Table",				10300)]
	[Gui.CategoryOrder("Labels",			10400)]
	#endregion

	public class BayesianTrend : Indicator
	{
		#region indInfo

		private string indName        = "Bayesian Trend [ChartPrime]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by ChartPrime can be found here: https://www.tradingview.com/script/rVEhAQDO-Bayesian-Trend-Indicator-ChartPrime/";

		#endregion

		#region Properties

		[NinjaScriptProperty]
		[Display(Order = 1, GroupName = "Indicator Setup", Name = "Source",
			Description = "Price input fed into all moving averages.")]
		public PriceType Source { get; set; }

		[NinjaScriptProperty]
		[Range(2, int.MaxValue)]
		[Display(Order = 2, GroupName = "Indicator Setup", Name = "MA's Length",
			Description = "Lookback for the slow MA quartet (SMA, EMA, DEMA, VWMA).")]
		public int Length { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 3, GroupName = "Indicator Setup", Name = "Gap Length Between Fast And Slow MA's",
			Description = "Difference subtracted from MA's Length to derive the fast quartet length. Higher = stronger separation.")]
		public int GapLength { get; set; }

		[NinjaScriptProperty]
		[Range(10, int.MaxValue)]
		[Display(Order = 4, GroupName = "Indicator Setup", Name = "Gap Signals",
			Description = "Bar offset for the smoothed gradient score cascade. Higher = less sensitive.")]
		public int Gap { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 5, GroupName = "Table", Name = "Show HUD Table",
			Description = "Render the per-MA posterior table on the chart panel.")]
		public bool ShowTable { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 6, GroupName = "Labels", Name = "Show Probability Labels",
			Description = "Print the posterior probability (as %) above / below each bar (chatty — default off).")]
		public bool ShowLabels { get; set; }

		[XmlIgnore]
		[Display(Order = 10, GroupName = "Display", Name = "Up Trend Color",
			Description = "Bar color when the posterior is bullish (>0.52).")]
		public Brush ColorUp { get; set; }
			[Browsable(false)]
			public string ColorUpSerialize
			{
				get { return Serialize.BrushToString(ColorUp); }
				set { ColorUp = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore]
		[Display(Order = 11, GroupName = "Display", Name = "Down Trend Color",
			Description = "Bar color when the posterior is bearish (<0.48).")]
		public Brush ColorDn { get; set; }
			[Browsable(false)]
			public string ColorDnSerialize
			{
				get { return Serialize.BrushToString(ColorDn); }
				set { ColorDn = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		// ── Table ────────────────────────────────────────────────────────────

		[NinjaScriptProperty]
		[Display(Order = 20, GroupName = "Table", Name = "Position",
			Description = "Corner of the chart panel where the HUD table is anchored.")]
		public BayesianTrend_TablePosition TablePosition { get; set; }

		[NinjaScriptProperty]
		[Range(8, 48)]
		[Display(Order = 21, GroupName = "Table", Name = "Title Font Size",
			Description = "Font size of the table title.")]
		public int TableTitleFontSize { get; set; }

		[NinjaScriptProperty]
		[Range(8, 48)]
		[Display(Order = 22, GroupName = "Table", Name = "Value Font Size",
			Description = "Font size of the large posterior-probability value.")]
		public int TableValueFontSize { get; set; }

		[NinjaScriptProperty]
		[Range(6, 32)]
		[Display(Order = 23, GroupName = "Table", Name = "Header Font Size",
			Description = "Font size of the column-header row (Moving Average / Slow / Fast).")]
		public int TableHeaderFontSize { get; set; }

		[NinjaScriptProperty]
		[Range(6, 32)]
		[Display(Order = 24, GroupName = "Table", Name = "Body Font Size",
			Description = "Font size of the per-MA value rows.")]
		public int TableBodyFontSize { get; set; }

		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Order = 25, GroupName = "Table", Name = "Background Opacity",
			Description = "Opacity (0–100) of the dark backdrop behind the table. 0 = no backdrop.")]
		public int TableBackgroundOpacity { get; set; }

		[XmlIgnore]
		[Display(Order = 30, GroupName = "Table", Name = "Text Color",
			Description = "Color of title text and per-MA labels.")]
		public Brush TableTextColor { get; set; }
			[Browsable(false)]
			public string TableTextColorSerialize
			{
				get { return Serialize.BrushToString(TableTextColor); }
				set { TableTextColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore]
		[Display(Order = 31, GroupName = "Table", Name = "Header / Muted Color",
			Description = "Color of muted row (column headers, Probability label).")]
		public Brush TableMutedColor { get; set; }
			[Browsable(false)]
			public string TableMutedColorSerialize
			{
				get { return Serialize.BrushToString(TableMutedColor); }
				set { TableMutedColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		// ── Labels ───────────────────────────────────────────────────────────

		[NinjaScriptProperty]
		[Range(6, 48)]
		[Display(Order = 40, GroupName = "Labels", Name = "Font Size",
			Description = "Font size of the per-bar probability text.")]
		public int LabelFontSize { get; set; }

		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Order = 41, GroupName = "Labels", Name = "Tick Offset",
			Description = "Number of price ticks above / below the bar to anchor the label.")]
		public int LabelTickOffset { get; set; }

		#endregion

		#region Public outputs (for strategy consumption)

		[Browsable(false)][XmlIgnore] public Series<double> PosteriorUp { get { Update(); return sPosteriorUp; } }

		#endregion

		#region Variables

		private ISeries<double>	src;
		private EMA				ema_, emaInner, emaInnerInner;
		private SMA				sma_;
		private VWMA			vwma_;
		private EMA				emaFast, emaInnerFast, emaInnerInnerFast;
		private SMA				smaFast;
		private VWMA			vwmaFast;
		private Series<double>	demaCompositeSlow, demaCompositeFast;
		private EMA				demaSlow, demaFast;

		private Series<double>	sScoreEmaSlow,  sScoreSmaSlow,  sScoreDemaSlow,  sScoreVwmaSlow;
		private Series<double>	sScoreEmaFast,  sScoreSmaFast,  sScoreDemaFast,  sScoreVwmaFast;

		private EMA				emaTrendInd, smaTrendInd, demaTrendInd, vwmaTrendInd;
		private EMA				emaTrendFInd, smaTrendFInd, demaTrendFInd, vwmaTrendFInd;

		private Series<double>	sPosteriorUp;

		// Last-bar HUD values
		private double	hudPosterior;
		private double	hudSmaSlow, hudSmaFast;
		private double	hudEmaSlow, hudEmaFast;
		private double	hudDemaSlow, hudDemaFast;
		private double	hudVwmaSlow, hudVwmaFast;

		// SharpDX HUD resources
		private SharpDX.DirectWrite.TextFormat	tfTitle, tfHeader, tfBody, tfBig, tfLabel;
		private SharpDX.Direct2D1.SolidColorBrush dxFg, dxMuted, dxColorUp, dxColorDn;
		private int lastTitleSize = -1, lastValueSize = -1, lastHeaderSize = -1, lastBodySize = -1, lastLabelSize = -1;

		#endregion

		#region OnStateChange

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description					= indDescription;
				Name						= indName;
				Calculate					= Calculate.OnBarClose;
				IsOverlay					= true;
				DisplayInDataBox			= true;
				DrawOnPricePanel			= true;
				DrawHorizontalGridLines		= true;
				DrawVerticalGridLines		= true;
				PaintPriceMarkers			= true;
				ScaleJustification			= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				IsSuspendedWhileInactive	= true;

				Source		= PriceType.Typical;
				Length		= 60;
				GapLength	= 20;
				Gap			= 10;
				ShowTable	= true;
				ShowLabels	= false;

				ColorUp = new SolidColorBrush(Color.FromRgb(0x0f, 0xac, 0x16));
				ColorDn = new SolidColorBrush(Color.FromRgb(0xc5, 0x12, 0x12));
				EnsureFrozen(ColorUp);
				EnsureFrozen(ColorDn);

				TablePosition			= BayesianTrend_TablePosition.TopRight;
				TableTitleFontSize		= 18;
				TableValueFontSize		= 22;
				TableHeaderFontSize		= 14;
				TableBodyFontSize		= 16;
				TableBackgroundOpacity	= 55;
				TableTextColor			= Brushes.WhiteSmoke;
				TableMutedColor			= Brushes.Gray;

				LabelFontSize			= 12;
				LabelTickOffset			= 8;
			}
			else if (State == State.DataLoaded)
			{
				src = GetSource(Source);

				ema_			= EMA(src, Length);
				sma_			= SMA(src, Length);
				vwma_			= VWMA(src, Length);
				emaInner		= EMA(src, Length);
				emaInnerInner	= EMA(emaInner, Length);

				int fastLen = Math.Max(2, Length - GapLength);
				emaFast				= EMA(src, fastLen);
				smaFast				= SMA(src, fastLen);
				vwmaFast			= VWMA(src, fastLen);
				emaInnerFast		= EMA(src, fastLen);
				emaInnerInnerFast	= EMA(emaInnerFast, fastLen);

				// EMA wraps these with Length / fastLen (user-configurable, can exceed 256) — needs Infinite for the seed bar.
				demaCompositeSlow	= new Series<double>(this, MaximumBarsLookBack.Infinite);
				demaCompositeFast	= new Series<double>(this, MaximumBarsLookBack.Infinite);
				demaSlow			= EMA(demaCompositeSlow, Length);
				demaFast			= EMA(demaCompositeFast, fastLen);

				sScoreEmaSlow	= new Series<double>(this);
				sScoreSmaSlow	= new Series<double>(this);
				sScoreDemaSlow	= new Series<double>(this);
				sScoreVwmaSlow	= new Series<double>(this);
				sScoreEmaFast	= new Series<double>(this);
				sScoreSmaFast	= new Series<double>(this);
				sScoreDemaFast	= new Series<double>(this);
				sScoreVwmaFast	= new Series<double>(this);

				emaTrendInd		= EMA(sScoreEmaSlow,  4);
				smaTrendInd		= EMA(sScoreSmaSlow,  4);
				demaTrendInd	= EMA(sScoreDemaSlow, 4);
				vwmaTrendInd	= EMA(sScoreVwmaSlow, 4);
				emaTrendFInd	= EMA(sScoreEmaFast,  4);
				smaTrendFInd	= EMA(sScoreSmaFast,  4);
				demaTrendFInd	= EMA(sScoreDemaFast, 4);
				vwmaTrendFInd	= EMA(sScoreVwmaFast, 4);

				// OnRender per-bar label rendering reads this across the visible window — needs Infinite.
				sPosteriorUp	= new Series<double>(this, MaximumBarsLookBack.Infinite);
			}
			else if (State == State.Realtime)
			{
				if (ChartControl == null) return;

				OnRenderTargetChanged();
				if (Dispatcher.CheckAccess())
					ChartControl.InvalidateVisual();
				else
					ChartControl.Dispatcher.InvokeAsync(() => ChartControl.InvalidateVisual());
			}
			else if (State == State.Terminated)
			{
				ReleaseRenderResources();
			}
		}

		#endregion

		#region OnBarUpdate

		protected override void OnBarUpdate()
		{
			int needed = Math.Max(Length + Gap + 12, GapLength + Gap + 12);
			if (CurrentBar < needed) return;

			// DEMA composite = 2*EMA - EMA(EMA); the outer EMA wrap is applied via demaSlow / demaFast.
			demaCompositeSlow[0] = 2.0 * emaInner[0] - emaInnerInner[0];
			demaCompositeFast[0] = 2.0 * emaInnerFast[0] - emaInnerInnerFast[0];

			sScoreEmaSlow [0] = ScoreCascade(ema_,    Gap);
			sScoreSmaSlow [0] = ScoreCascade(sma_,    Gap);
			sScoreDemaSlow[0] = ScoreCascade(demaSlow, Gap);
			sScoreVwmaSlow[0] = ScoreCascade(vwma_,   Gap);

			sScoreEmaFast [0] = ScoreCascade(emaFast,    Gap);
			sScoreSmaFast [0] = ScoreCascade(smaFast,    Gap);
			sScoreDemaFast[0] = ScoreCascade(demaFast,   Gap);
			sScoreVwmaFast[0] = ScoreCascade(vwmaFast,   Gap);

			double emaTrend  = emaTrendInd[0];
			double smaTrend  = smaTrendInd[0];
			double demaTrend = demaTrendInd[0];
			double vwmaTrend = vwmaTrendInd[0];

			double emaTrendF  = emaTrendFInd[0];
			double smaTrendF  = smaTrendFInd[0];
			double demaTrendF = demaTrendFInd[0];
			double vwmaTrendF = vwmaTrendFInd[0];

			double priorUp      = (emaTrend  + smaTrend  + demaTrend  + vwmaTrend ) / 4.0;
			double priorDown    = 1.0 - priorUp;
			double likeUp       = (emaTrendF + smaTrendF + demaTrendF + vwmaTrendF) / 4.0;
			double likeDown     = 1.0 - likeUp;
			double denom        = priorUp * likeUp + priorDown * likeDown;
			double posteriorUp  = denom == 0.0 ? 0.0 : priorUp * likeUp / denom;
			if (double.IsNaN(posteriorUp)) posteriorUp = 0.0;

			sPosteriorUp[0] = posteriorUp;

			// Cache last-bar values for the HUD.
			hudPosterior	= posteriorUp;
			hudEmaSlow		= emaTrend;	hudEmaFast	= emaTrendF;
			hudSmaSlow		= smaTrend;	hudSmaFast	= smaTrendF;
			hudDemaSlow		= demaTrend; hudDemaFast = demaTrendF;
			hudVwmaSlow		= vwmaTrend; hudVwmaFast = vwmaTrendF;

			// Bar coloring — gradient: dn → bg → up across [0..0.48..0.52..1].
			BarBrushes[0]               = GradientBarBrush(posteriorUp);
			CandleOutlineBrushes[0]     = BarBrushes[0];

			double markerOffset = TickSize * 8.0;

			// Crossover diamond markers (◆) below/above the bar at 0.5 cross.
			if (CurrentBar > 0)
			{
				double prev = sPosteriorUp[1];
				if (prev <= 0.5 && posteriorUp > 0.5)
					Draw.Text(this, "btUp" + CurrentBar, "◆", 0, Low[0]  - markerOffset, ColorUp);
				if (prev >= 0.5 && posteriorUp < 0.5)
					Draw.Text(this, "btDn" + CurrentBar, "◆", 0, High[0] + markerOffset, ColorDn);
			}
			// Per-bar probability labels are rendered in OnRender (SharpDX) so the font / color stay
			// fully controllable without API padding.
		}

		#endregion

		#region Smoothed gradient score

		// Stepwise rank score: 1.0 if source clears the MA `gap` bars back, 0.9 at gap-1, … 0.1 at gap-9, else 0.
		private double ScoreCascade(ISeries<double> series, int gap)
		{
			if (CurrentBar < gap) return 0.0;
			double s0 = src[0];
			if      (s0 >= series[gap])     return 1.0;
			else if (s0 >= series[gap - 1]) return 0.9;
			else if (s0 >= series[gap - 2]) return 0.8;
			else if (s0 >= series[gap - 3]) return 0.7;
			else if (s0 >= series[gap - 4]) return 0.6;
			else if (s0 >= series[gap - 5]) return 0.5;
			else if (s0 >= series[gap - 6]) return 0.4;
			else if (s0 >= series[gap - 7]) return 0.3;
			else if (s0 >= series[gap - 8]) return 0.2;
			else if (s0 >= series[gap - 9]) return 0.1;
			else                            return 0.0;
		}

		#endregion

		#region OnRenderTargetChanged

		public override void OnRenderTargetChanged()
		{
			try
			{
				ReleaseRenderResources();
				if (RenderTarget == null) return;

				BuildTextFormats();

				dxFg      = MakeDXBrush(TableTextColor,  0.95f);
				dxMuted   = MakeDXBrush(TableMutedColor, 0.85f);
				dxColorUp = MakeDXBrush(ColorUp,         1.0f);
				dxColorDn = MakeDXBrush(ColorDn,         1.0f);
			}
			catch
			{
				// Suppress during chart teardown.
			}
		}

		private void BuildTextFormats()
		{
			DisposeTextFormats();

			tfTitle = new SharpDX.DirectWrite.TextFormat(Core.Globals.DirectWriteFactory, "Arial",
				SharpDX.DirectWrite.FontWeight.Bold, SharpDX.DirectWrite.FontStyle.Normal, TableTitleFontSize)
			{ TextAlignment = SharpDX.DirectWrite.TextAlignment.Center,
			  ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center };

			tfBig = new SharpDX.DirectWrite.TextFormat(Core.Globals.DirectWriteFactory, "Arial",
				SharpDX.DirectWrite.FontWeight.Bold, SharpDX.DirectWrite.FontStyle.Normal, TableValueFontSize)
			{ TextAlignment = SharpDX.DirectWrite.TextAlignment.Trailing,
			  ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center };

			tfHeader = new SharpDX.DirectWrite.TextFormat(Core.Globals.DirectWriteFactory, "Arial",
				SharpDX.DirectWrite.FontWeight.Bold, SharpDX.DirectWrite.FontStyle.Normal, TableHeaderFontSize)
			{ ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center };

			tfBody = new SharpDX.DirectWrite.TextFormat(Core.Globals.DirectWriteFactory, "Arial",
				SharpDX.DirectWrite.FontWeight.Normal, SharpDX.DirectWrite.FontStyle.Normal, TableBodyFontSize)
			{ ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center };

			tfLabel = new SharpDX.DirectWrite.TextFormat(Core.Globals.DirectWriteFactory, "Arial",
				SharpDX.DirectWrite.FontWeight.Bold, SharpDX.DirectWrite.FontStyle.Normal, LabelFontSize)
			{ TextAlignment = SharpDX.DirectWrite.TextAlignment.Center,
			  ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center };

			lastTitleSize  = TableTitleFontSize;
			lastValueSize  = TableValueFontSize;
			lastHeaderSize = TableHeaderFontSize;
			lastBodySize   = TableBodyFontSize;
			lastLabelSize  = LabelFontSize;
		}

		private void EnsureTextFormats()
		{
			if (tfTitle == null
				|| lastTitleSize  != TableTitleFontSize
				|| lastValueSize  != TableValueFontSize
				|| lastHeaderSize != TableHeaderFontSize
				|| lastBodySize   != TableBodyFontSize
				|| lastLabelSize  != LabelFontSize)
				BuildTextFormats();
		}

		private void DisposeTextFormats()
		{
			if (tfTitle   != null) { tfTitle.Dispose();   tfTitle   = null; }
			if (tfBig     != null) { tfBig.Dispose();     tfBig     = null; }
			if (tfHeader  != null) { tfHeader.Dispose();  tfHeader  = null; }
			if (tfBody    != null) { tfBody.Dispose();    tfBody    = null; }
			if (tfLabel   != null) { tfLabel.Dispose();   tfLabel   = null; }
		}

		private void ReleaseRenderResources()
		{
			if (dxFg      != null) { dxFg.Dispose();      dxFg      = null; }
			if (dxMuted   != null) { dxMuted.Dispose();   dxMuted   = null; }
			if (dxColorUp != null) { dxColorUp.Dispose(); dxColorUp = null; }
			if (dxColorDn != null) { dxColorDn.Dispose(); dxColorDn = null; }
			DisposeTextFormats();
		}

		#endregion

		#region OnRender — HUD table

		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			base.OnRender(chartControl, chartScale);

			if (RenderTarget == null)	return;
			if (ChartBars == null)		return;
			if (!IsVisible)				return;
			if (IsInHitTest)			return;
			if (!ShowTable && !ShowLabels) return;

			EnsureTextFormats();
			if (dxFg == null) return;	// render resources not yet built

			if (ShowLabels) RenderPerBarLabels(chartControl, chartScale);
			if (!ShowTable) return;

			// Sizes scale off the body font so the layout stays proportional when the user resizes.
			float baseSize	= TableBodyFontSize;
			float colWidth	= baseSize * 7.0f;
			float rowH		= baseSize * 1.6f;
			float padX		= baseSize * 0.6f;
			float padY		= baseSize * 0.6f;
			float titleH	= TableTitleFontSize  * 1.5f;
			float valueH	= TableValueFontSize  * 1.4f;
			float headerH	= TableHeaderFontSize * 1.4f;
			float tableW	= colWidth * 3.0f;
			float tableH	= titleH + valueH + headerH + rowH * 4.0f + padY * 2.0f;

			float xLeft, yTop;
			switch (TablePosition)
			{
				case BayesianTrend_TablePosition.TopLeft:
					xLeft = ChartPanel.X + padX;
					yTop  = ChartPanel.Y + padY;
					break;
				case BayesianTrend_TablePosition.BottomLeft:
					xLeft = ChartPanel.X + padX;
					yTop  = ChartPanel.Y + ChartPanel.H - padY - tableH;
					break;
				case BayesianTrend_TablePosition.BottomRight:
					xLeft = ChartPanel.X + ChartPanel.W - padX - tableW;
					yTop  = ChartPanel.Y + ChartPanel.H - padY - tableH;
					break;
				default:	// TopRight
					xLeft = ChartPanel.X + ChartPanel.W - padX - tableW;
					yTop  = ChartPanel.Y + padY;
					break;
			}

			// Translucent backdrop
			if (TableBackgroundOpacity > 0)
			{
				float bgAlpha = Math.Max(0f, Math.Min(1f, TableBackgroundOpacity / 100f));
				using (var bg = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color4(0f, 0f, 0f, bgAlpha)))
				{
					RenderTarget.FillRectangle(new SharpDX.RectangleF(xLeft - padX, yTop - padY, tableW + padX * 2f, tableH), bg);
				}
			}

			float y = yTop;

			// Title
			DrawCell("Bayesian Trend Indicator", tfTitle, dxFg,
				xLeft, y, tableW, titleH, SharpDX.DirectWrite.TextAlignment.Center);
			y += titleH;

			// Posterior probability — large, gradient-colored
			DrawCell("Probability of Up Trend:", tfHeader, dxMuted, xLeft, y, colWidth * 1.6f, valueH, SharpDX.DirectWrite.TextAlignment.Leading);
			using (var pcb = MakeDXBrush(GradientBrush(hudPosterior, 0.0, 1.0, ColorDn, ColorUp), 1.0f))
			{
				string pct = (hudPosterior * 100.0).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%";
				DrawCell(pct, tfBig, pcb, xLeft + colWidth * 1.6f, y, tableW - colWidth * 1.6f, valueH, SharpDX.DirectWrite.TextAlignment.Trailing);
			}
			y += valueH;

			// Header row
			DrawCell("Moving Average", tfHeader, dxMuted, xLeft,                  y, colWidth, headerH, SharpDX.DirectWrite.TextAlignment.Leading);
			DrawCell("Slow",           tfHeader, dxMuted, xLeft + colWidth,        y, colWidth, headerH, SharpDX.DirectWrite.TextAlignment.Center);
			DrawCell("Fast",           tfHeader, dxMuted, xLeft + colWidth * 2.0f, y, colWidth, headerH, SharpDX.DirectWrite.TextAlignment.Center);
			y += headerH;

			DrawMaRow(xLeft, y, colWidth, rowH, "SMA",  hudSmaSlow,  hudSmaFast);  y += rowH;
			DrawMaRow(xLeft, y, colWidth, rowH, "EMA",  hudEmaSlow,  hudEmaFast);  y += rowH;
			DrawMaRow(xLeft, y, colWidth, rowH, "DEMA", hudDemaSlow, hudDemaFast); y += rowH;
			DrawMaRow(xLeft, y, colWidth, rowH, "VWMA", hudVwmaSlow, hudVwmaFast); y += rowH;
		}

		private void RenderPerBarLabels(ChartControl chartControl, ChartScale chartScale)
		{
			if (sPosteriorUp == null || tfLabel == null) return;

			int fromIdx = Math.Max(ChartBars.FromIndex, 0);
			int toIdx   = Math.Min(ChartBars.ToIndex,   CurrentBar);
			if (toIdx < fromIdx) return;

			double offset = TickSize * LabelTickOffset;
			float w		= LabelFontSize * 4.0f;
			float h		= LabelFontSize * 1.6f;

			for (int j = fromIdx; j <= toIdx; j++)
			{
				if (!sPosteriorUp.IsValidDataPointAt(j)) continue;

				double p      = sPosteriorUp.GetValueAt(j);
				bool   up     = p > 0.5;
				double yPrice = up ? Bars.GetHigh(j) + offset : Bars.GetLow(j) - offset;

				float xCenter = chartControl.GetXByBarIndex(ChartBars, j);
				float yCenter = (float)chartScale.GetYByValue(yPrice);

				string text = (p * 100.0).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%";
				var rect = new SharpDX.RectangleF(xCenter - w * 0.5f, yCenter - h * 0.5f, w, h);
				RenderTarget.DrawText(text, tfLabel, rect, up ? dxColorUp : dxColorDn);
			}
		}

		private void DrawMaRow(float x, float y, float colW, float rowH, string label, double slow, double fast)
		{
			DrawCell(label, tfBody, dxFg, x, y, colW, rowH, SharpDX.DirectWrite.TextAlignment.Leading);
			using (var bSlow = MakeDXBrush(GradientBrush(slow, 0.0, 1.0, ColorDn, ColorUp), 1.0f))
				DrawCell(slow.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), tfBody, bSlow,
					x + colW, y, colW, rowH, SharpDX.DirectWrite.TextAlignment.Center);
			using (var bFast = MakeDXBrush(GradientBrush(fast, 0.0, 1.0, ColorDn, ColorUp), 1.0f))
				DrawCell(fast.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), tfBody, bFast,
					x + colW * 2.0f, y, colW, rowH, SharpDX.DirectWrite.TextAlignment.Center);
		}

		private void DrawCell(string text, SharpDX.DirectWrite.TextFormat tf, SharpDX.Direct2D1.Brush brush,
			float x, float y, float w, float h, SharpDX.DirectWrite.TextAlignment align)
		{
			if (brush == null || tf == null) return;
			tf.TextAlignment = align;
			RenderTarget.DrawText(text, tf, new SharpDX.RectangleF(x, y, w, h), brush);
		}

		#endregion

		#region Helpers

		private Brush GradientBarBrush(double posterior)
		{
			Brush bg = ChartBgBrush();
			Brush bgFade = WithAlpha(bg, 204);

			if (posterior < 0.48)
				return GradientBrush(posterior, 0.0,  0.48, ColorDn, bgFade);
			if (posterior > 0.52)
				return GradientBrush(posterior, 0.52, 1.0,  bgFade,  ColorUp);
			return bgFade;
		}

		private Brush ChartBgBrush()
		{
			if (ChartControl != null && ChartControl.Properties != null && ChartControl.Properties.ChartBackground is SolidColorBrush scb)
				return scb;
			return Brushes.Black;
		}

		// Linear interpolation of two colors based on a value's position in [lo..hi].
		private static Brush GradientBrush(double v, double lo, double hi, Brush a, Brush b)
		{
			double t = (hi == lo) ? 0.0 : (v - lo) / (hi - lo);
			t = Math.Max(0.0, Math.Min(1.0, t));

			var ca = (a as SolidColorBrush)?.Color ?? Colors.Gray;
			var cb = (b as SolidColorBrush)?.Color ?? Colors.Gray;

			byte A = (byte)(ca.A + (cb.A - ca.A) * t);
			byte R = (byte)(ca.R + (cb.R - ca.R) * t);
			byte G = (byte)(ca.G + (cb.G - ca.G) * t);
			byte B = (byte)(ca.B + (cb.B - ca.B) * t);

			return EnsureFrozen(new SolidColorBrush(Color.FromArgb(A, R, G, B)));
		}

		private static Brush WithAlpha(Brush src, byte alpha)
		{
			var scb = src as SolidColorBrush;
			if (scb == null) return src;
			var c = scb.Color;
			return EnsureFrozen(new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B)));
		}

		private SharpDX.Direct2D1.SolidColorBrush MakeDXBrush(Brush wpf, float alpha)
		{
			if (RenderTarget == null) return null;

			var scb = wpf as SolidColorBrush;
			System.Windows.Media.Color c = scb != null ? scb.Color : Colors.Gray;
			float wpfA = scb != null ? (float)(scb.Opacity * (c.A / 255f)) : 1f;

			return new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
				new SharpDX.Color4(c.R / 255f, c.G / 255f, c.B / 255f,
					Math.Max(0f, Math.Min(1f, alpha * wpfA))));
		}

		private static Brush EnsureFrozen(Brush b)
		{
			if (b != null && b.CanFreeze && !b.IsFrozen)
				b.Freeze();
			return b;
		}

		private ISeries<double> GetSource(PriceType priceType)
		{
			switch (priceType)
			{
				case PriceType.Close:    return Close;
				case PriceType.High:     return High;
				case PriceType.Low:      return Low;
				case PriceType.Median:   return Median;
				case PriceType.Open:     return Open;
				case PriceType.Typical:  return Typical;
				case PriceType.Weighted: return Weighted;
				default:                 return Input;
			}
		}

		#endregion
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private indTradingView.BayesianTrend[] cacheBayesianTrend;
		public indTradingView.BayesianTrend BayesianTrend(PriceType source, int length, int gapLength, int gap, bool showTable, bool showLabels, BayesianTrend_TablePosition tablePosition, int tableTitleFontSize, int tableValueFontSize, int tableHeaderFontSize, int tableBodyFontSize, int tableBackgroundOpacity, int labelFontSize, int labelTickOffset)
		{
			return BayesianTrend(Input, source, length, gapLength, gap, showTable, showLabels, tablePosition, tableTitleFontSize, tableValueFontSize, tableHeaderFontSize, tableBodyFontSize, tableBackgroundOpacity, labelFontSize, labelTickOffset);
		}

		public indTradingView.BayesianTrend BayesianTrend(ISeries<double> input, PriceType source, int length, int gapLength, int gap, bool showTable, bool showLabels, BayesianTrend_TablePosition tablePosition, int tableTitleFontSize, int tableValueFontSize, int tableHeaderFontSize, int tableBodyFontSize, int tableBackgroundOpacity, int labelFontSize, int labelTickOffset)
		{
			if (cacheBayesianTrend != null)
				for (int idx = 0; idx < cacheBayesianTrend.Length; idx++)
					if (cacheBayesianTrend[idx] != null && cacheBayesianTrend[idx].Source == source && cacheBayesianTrend[idx].Length == length && cacheBayesianTrend[idx].GapLength == gapLength && cacheBayesianTrend[idx].Gap == gap && cacheBayesianTrend[idx].ShowTable == showTable && cacheBayesianTrend[idx].ShowLabels == showLabels && cacheBayesianTrend[idx].TablePosition == tablePosition && cacheBayesianTrend[idx].TableTitleFontSize == tableTitleFontSize && cacheBayesianTrend[idx].TableValueFontSize == tableValueFontSize && cacheBayesianTrend[idx].TableHeaderFontSize == tableHeaderFontSize && cacheBayesianTrend[idx].TableBodyFontSize == tableBodyFontSize && cacheBayesianTrend[idx].TableBackgroundOpacity == tableBackgroundOpacity && cacheBayesianTrend[idx].LabelFontSize == labelFontSize && cacheBayesianTrend[idx].LabelTickOffset == labelTickOffset && cacheBayesianTrend[idx].EqualsInput(input))
						return cacheBayesianTrend[idx];
			return CacheIndicator<indTradingView.BayesianTrend>(new indTradingView.BayesianTrend(){ Source = source, Length = length, GapLength = gapLength, Gap = gap, ShowTable = showTable, ShowLabels = showLabels, TablePosition = tablePosition, TableTitleFontSize = tableTitleFontSize, TableValueFontSize = tableValueFontSize, TableHeaderFontSize = tableHeaderFontSize, TableBodyFontSize = tableBodyFontSize, TableBackgroundOpacity = tableBackgroundOpacity, LabelFontSize = labelFontSize, LabelTickOffset = labelTickOffset }, input, ref cacheBayesianTrend);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.BayesianTrend BayesianTrend(PriceType source, int length, int gapLength, int gap, bool showTable, bool showLabels, BayesianTrend_TablePosition tablePosition, int tableTitleFontSize, int tableValueFontSize, int tableHeaderFontSize, int tableBodyFontSize, int tableBackgroundOpacity, int labelFontSize, int labelTickOffset)
		{
			return indicator.BayesianTrend(Input, source, length, gapLength, gap, showTable, showLabels, tablePosition, tableTitleFontSize, tableValueFontSize, tableHeaderFontSize, tableBodyFontSize, tableBackgroundOpacity, labelFontSize, labelTickOffset);
		}

		public Indicators.indTradingView.BayesianTrend BayesianTrend(ISeries<double> input , PriceType source, int length, int gapLength, int gap, bool showTable, bool showLabels, BayesianTrend_TablePosition tablePosition, int tableTitleFontSize, int tableValueFontSize, int tableHeaderFontSize, int tableBodyFontSize, int tableBackgroundOpacity, int labelFontSize, int labelTickOffset)
		{
			return indicator.BayesianTrend(input, source, length, gapLength, gap, showTable, showLabels, tablePosition, tableTitleFontSize, tableValueFontSize, tableHeaderFontSize, tableBodyFontSize, tableBackgroundOpacity, labelFontSize, labelTickOffset);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.BayesianTrend BayesianTrend(PriceType source, int length, int gapLength, int gap, bool showTable, bool showLabels, BayesianTrend_TablePosition tablePosition, int tableTitleFontSize, int tableValueFontSize, int tableHeaderFontSize, int tableBodyFontSize, int tableBackgroundOpacity, int labelFontSize, int labelTickOffset)
		{
			return indicator.BayesianTrend(Input, source, length, gapLength, gap, showTable, showLabels, tablePosition, tableTitleFontSize, tableValueFontSize, tableHeaderFontSize, tableBodyFontSize, tableBackgroundOpacity, labelFontSize, labelTickOffset);
		}

		public Indicators.indTradingView.BayesianTrend BayesianTrend(ISeries<double> input , PriceType source, int length, int gapLength, int gap, bool showTable, bool showLabels, BayesianTrend_TablePosition tablePosition, int tableTitleFontSize, int tableValueFontSize, int tableHeaderFontSize, int tableBodyFontSize, int tableBackgroundOpacity, int labelFontSize, int labelTickOffset)
		{
			return indicator.BayesianTrend(input, source, length, gapLength, gap, showTable, showLabels, tablePosition, tableTitleFontSize, tableValueFontSize, tableHeaderFontSize, tableBodyFontSize, tableBackgroundOpacity, labelFontSize, labelTickOffset);
		}
	}
}

#endregion
