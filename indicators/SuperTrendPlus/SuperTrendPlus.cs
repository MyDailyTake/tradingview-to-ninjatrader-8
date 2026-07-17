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

// NT8 Version of SuperTrend+
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the Mozilla Public License 2.0 (MPL 2.0).
// The original Pine Script™ code is by Electrified (electrifiedtrading) and can be found at: https://www.tradingview.com/script/smXJk7s5-SuperTrend/
// Dependent library (also © Electrified, MPL 2.0): https://www.tradingview.com/script/p7CZyF5N-SupportResitanceAndTrend/
// Adaptation for NinjaTrader by jack@mydailytake.com
// Write-up + downloads: https://mydailytake.com/tradingview-supertrend-plus-electrified-ninjatrader-8/
// Source + all conversions: https://github.com/MyDailyTake/tradingview-to-ninjatrader-8
// © 2026 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the Mozilla Public License 2.0. Full license details at https://mozilla.org/MPL/2.0/
// The use of Electrified name or its adapted code in this work does not imply endorsement by the original authors.
//
// Non-repainting: trend state is computed from closed-bar data and never rewritten once a bar closes.
//
// Strategy integration: BuySignal / SellSignal / Warning / Reversal are exposed as Series<bool>,
// and TrendSeries / ActiveUpper / ActiveLower as Series<int> / Series<double> for historical access.

#region Enums SuperTrendPlus

public enum SuperTrendPlus_MaMode
{
	SMA,
	EMA,
	WMA,
	VWMA,
	VAWMA,
}

#endregion

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	#region Categories

	[Gui.CategoryOrder("Average True Range",	10100)]
	[Gui.CategoryOrder("Confirmation",			10200)]
	[Gui.CategoryOrder("Display",				10300)]

	#endregion

	public class SuperTrendPlus : Indicator
	{
		#region indInfo

		private string indName			= "SuperTrend+ [Electrified]";
		private string indDescription	= "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by Electrified can be found here: https://www.tradingview.com/script/smXJk7s5-SuperTrend/";

		#endregion

		#region Properties

		// ── Average True Range ───────────────────────────────────────────────

		[NinjaScriptProperty]
		[Display(Order = 01, GroupName = "Average True Range", Name = "Mode",
			Description = "Averaging function used to smooth the True Range into an ATR value.")]
		public SuperTrendPlus_MaMode Mode { get; set; }

		[NinjaScriptProperty]
		[Range(2, int.MaxValue)]
		[Display(Order = 02, GroupName = "Average True Range", Name = "Period",
			Description = "Number of bars used when computing the ATR value.")]
		public int AtrPeriod { get; set; }

		[NinjaScriptProperty]
		[Range(0.01, double.MaxValue)]
		[Display(Order = 03, GroupName = "Average True Range", Name = "Multiplier",
			Description = "Multiplier applied to the ATR when defining the SuperTrend upper / lower bands.")]
		public double AtrMultiplier { get; set; }

		[NinjaScriptProperty]
		[Range(0, double.MaxValue)]
		[Display(Order = 04, GroupName = "Average True Range", Name = "Max Deviation",
			Description = "Standard-deviation limit for filtering True Range outliers before averaging. 0 disables the filter.")]
		public double MaxDeviation { get; set; }

		// ── Confirmation ─────────────────────────────────────────────────────

		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Order = 01, GroupName = "Confirmation", Name = "Closed Bars",
			Description = "Number of closed bars that must exceed the SuperTrend value before a trend reversal is confirmed.")]
		public int CloseBars { get; set; }

		// ── Display ──────────────────────────────────────────────────────────

		[NinjaScriptProperty]
		[Display(Order = 01, GroupName = "Display", Name = "Show Buy / Sell Labels",
			Description = "Draw text 'Buy' / 'Sell' labels at trend reversals, in addition to the reversal dot.")]
		public bool ShowSignals { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 02, GroupName = "Display", Name = "Highlighter On",
			Description = "Draw translucent fill between the OHLC4 midline and the active trend line.")]
		public bool Highlighting { get; set; }

		[NinjaScriptProperty]
		[Range(1, 100)]
		[Display(Order = 03, GroupName = "Display", Name = "Highlighter Opacity (%)",
			Description = "Opacity of the highlighter fill, 1–100.")]
		public int HighlightOpacity { get; set; }

		[NinjaScriptProperty]
		[Range(6, 48)]
		[Display(Order = 04, GroupName = "Display", Name = "Label Text Size",
			Description = "Font size of the 'Buy' / 'Sell' labels rendered on the chart.")]
		public int TextSize { get; set; }

		[XmlIgnore()]
		[Display(Order = 10, GroupName = "Display", Name = "Up Trend Color", Description = "Color of the up-trend line and bullish highlighter fill.")]
		public Brush UpColor { get; set; }
			[Browsable(false)]
			public string UpColorSerialize
			{
				get { return Serialize.BrushToString(UpColor); }
				set { UpColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore()]
		[Display(Order = 11, GroupName = "Display", Name = "Down Trend Color", Description = "Color of the down-trend line and bearish highlighter fill.")]
		public Brush DnColor { get; set; }
			[Browsable(false)]
			public string DnColorSerialize
			{
				get { return Serialize.BrushToString(DnColor); }
				set { DnColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore()]
		[Display(Order = 12, GroupName = "Display", Name = "Unconfirmed Color", Description = "Color of the active trend line while a reversal is being tested but not yet confirmed.")]
		public Brush UnconfirmedColor { get; set; }
			[Browsable(false)]
			public string UnconfirmedColorSerialize
			{
				get { return Serialize.BrushToString(UnconfirmedColor); }
				set { UnconfirmedColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore()]
		[Display(Order = 13, GroupName = "Display", Name = "Buy Label Text Color", Description = "Color of the 'Buy' reversal text label.")]
		public Brush BuyTextColor { get; set; }
			[Browsable(false)]
			public string BuyTextColorSerialize
			{
				get { return Serialize.BrushToString(BuyTextColor); }
				set { BuyTextColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore()]
		[Display(Order = 14, GroupName = "Display", Name = "Sell Label Text Color", Description = "Color of the 'Sell' reversal text label.")]
		public Brush SellTextColor { get; set; }
			[Browsable(false)]
			public string SellTextColorSerialize
			{
				get { return Serialize.BrushToString(SellTextColor); }
				set { SellTextColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		#endregion

		#region Public outputs (for strategy consumption)

		[Browsable(false)][XmlIgnore] public Series<double> UpTrend    { get { return Values[0]; } }
		[Browsable(false)][XmlIgnore] public Series<double> DnTrend    { get { return Values[1]; } }

		[Browsable(false)][XmlIgnore] public Series<bool>   BuySignal   { get { Update(); return sBuySignal;   } }
		[Browsable(false)][XmlIgnore] public Series<bool>   SellSignal  { get { Update(); return sSellSignal;  } }
		[Browsable(false)][XmlIgnore] public Series<bool>   Warning     { get { Update(); return sWarning;     } }
		[Browsable(false)][XmlIgnore] public Series<bool>   Reversal    { get { Update(); return sReversal;    } }

		[Browsable(false)][XmlIgnore] public Series<int>    TrendSeries { get { Update(); return sTrend;       } }
		[Browsable(false)][XmlIgnore] public Series<double> ActiveUpper { get { Update(); return sActiveUpper; } }
		[Browsable(false)][XmlIgnore] public Series<double> ActiveLower { get { Update(); return sActiveLower; } }

		#endregion

		#region Variables

		private Series<double>	trSeries;
		private Series<double>	cleanedTrSeries;
		private Series<double>	upRawSeries;
		private Series<double>	dnRawSeries;
		private Series<int>		unconfirmedSeries;

		private Series<int>		sTrend;
		private Series<double>	sActiveUpper;
		private Series<double>	sActiveLower;
		private Series<bool>	sBuySignal;
		private Series<bool>	sSellSignal;
		private Series<bool>	sWarning;
		private Series<bool>	sReversal;

		private double	lastU;
		private double	lastD;
		private int		trendState;		// -1 / 0 / +1
		private int		confirmation;
		private int		unconfirmed;

		private ISeries<double>	atr;
		private Series<double>	atrVawmaSeries;

		private SharpDX.Direct2D1.SolidColorBrush	dxUpFill;
		private SharpDX.Direct2D1.SolidColorBrush	dxDnFill;
		private SharpDX.Direct2D1.SolidColorBrush	dxBuyTextBrush;
		private SharpDX.Direct2D1.SolidColorBrush	dxSellTextBrush;
		private SharpDX.DirectWrite.TextFormat		textFormat;
		private int									textFormatSize;

		private Dictionary<int, double>	buyLabels;
		private Dictionary<int, double>	sellLabels;

		#endregion

		#region OnStateChange

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description								= indDescription;
				Name									= indName;
				Calculate								= Calculate.OnBarClose;
				IsOverlay								= true;
				DisplayInDataBox						= true;
				DrawOnPricePanel						= true;
				DrawHorizontalGridLines					= true;
				DrawVerticalGridLines					= true;
				PaintPriceMarkers						= true;
				ScaleJustification						= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				IsSuspendedWhileInactive				= true;

				Mode				= SuperTrendPlus_MaMode.VAWMA;
				AtrPeriod			= 120;
				AtrMultiplier		= 3.0;
				MaxDeviation		= 0.0;
				CloseBars			= 2;
				ShowSignals			= false;
				Highlighting		= true;
				HighlightOpacity	= 25;
				TextSize			= 12;

				UpColor				= Brushes.LimeGreen;
				DnColor				= Brushes.Red;
				UnconfirmedColor	= Brushes.Yellow;
				BuyTextColor		= Brushes.White;
				SellTextColor		= Brushes.White;

				AddPlot(new Stroke(Brushes.LimeGreen, DashStyleHelper.Solid, 2f), PlotStyle.Line, "UpTrend");
				AddPlot(new Stroke(Brushes.Red,       DashStyleHelper.Solid, 2f), PlotStyle.Line, "DnTrend");
			}
			else if (State == State.DataLoaded)
			{
				trSeries			= new Series<double>(this, MaximumBarsLookBack.Infinite);
				cleanedTrSeries		= new Series<double>(this, MaximumBarsLookBack.Infinite);
				upRawSeries			= new Series<double>(this, MaximumBarsLookBack.Infinite);
				dnRawSeries			= new Series<double>(this, MaximumBarsLookBack.Infinite);
				unconfirmedSeries	= new Series<int>   (this, MaximumBarsLookBack.Infinite);

				sTrend				= new Series<int>   (this, MaximumBarsLookBack.Infinite);
				sActiveUpper		= new Series<double>(this, MaximumBarsLookBack.Infinite);
				sActiveLower		= new Series<double>(this, MaximumBarsLookBack.Infinite);
				sBuySignal			= new Series<bool>  (this, MaximumBarsLookBack.Infinite);
				sSellSignal			= new Series<bool>  (this, MaximumBarsLookBack.Infinite);
				sWarning			= new Series<bool>  (this, MaximumBarsLookBack.Infinite);
				sReversal			= new Series<bool>  (this, MaximumBarsLookBack.Infinite);

				switch (Mode)
				{
					case SuperTrendPlus_MaMode.SMA:		atr = SMA (cleanedTrSeries, AtrPeriod); break;
					case SuperTrendPlus_MaMode.EMA:		atr = EMA (cleanedTrSeries, AtrPeriod); break;
					case SuperTrendPlus_MaMode.WMA:		atr = WMA (cleanedTrSeries, AtrPeriod); break;
					case SuperTrendPlus_MaMode.VWMA:	atr = VWMA(cleanedTrSeries, AtrPeriod); break;
					case SuperTrendPlus_MaMode.VAWMA:
						atrVawmaSeries	= new Series<double>(this, MaximumBarsLookBack.Infinite);
						atr				= atrVawmaSeries;
						break;
				}

				buyLabels	= new Dictionary<int, double>();
				sellLabels	= new Dictionary<int, double>();

				lastU			= 0;
				lastD			= 0;
				trendState		= 0;
				confirmation	= 0;
				unconfirmed		= 0;

				textFormatSize	= -1;
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
			sBuySignal[0]	= false;
			sSellSignal[0]	= false;
			sWarning[0]		= false;
			sReversal[0]	= false;

			// ── True Range ───────────────────────────────────────────────────
			double tr0;
			if (CurrentBar == 0)
			{
				tr0 = High[0] - Low[0];
			}
			else
			{
				double hl = High[0] - Low[0];
				double hc = Math.Abs(High[0] - Close[1]);
				double lc = Math.Abs(Low[0]  - Close[1]);
				tr0 = Math.Max(hl, Math.Max(hc, lc));
			}
			trSeries[0] = tr0;

			// ── Clean TR (outlier filter) ────────────────────────────────────
			if (MaxDeviation <= 0.0 || CurrentBar < AtrPeriod + 1)
			{
				cleanedTrSeries[0] = tr0;
			}
			else
			{
				double sum = 0.0;
				for (int i = 1; i <= AtrPeriod; i++) sum += trSeries[i];
				double avg = sum / AtrPeriod;

				double varSum = 0.0;
				for (int i = 1; i <= AtrPeriod; i++)
				{
					double d = trSeries[i] - avg;
					varSum += d * d;
				}
				double sd  = Math.Sqrt(varSum / AtrPeriod);
				double lim = sd * MaxDeviation;

				cleanedTrSeries[0] = (tr0 > avg + lim || tr0 < avg - lim) ? avg : tr0;
			}

			// ── Warm-up gate ─────────────────────────────────────────────────
			if (CurrentBar < AtrPeriod - 1)
			{
				upRawSeries.Reset();
				dnRawSeries.Reset();
				sTrend[0]				= 0;
				unconfirmedSeries[0]	= 0;
				sActiveUpper.Reset();
				sActiveLower.Reset();
				Values[0].Reset();
				Values[1].Reset();
				return;
			}

			// ── ATR ──────────────────────────────────────────────────────────
			if (atrVawmaSeries != null)
			{
				double vawma;
				if (ComputeVAWMA(cleanedTrSeries, AtrPeriod, out vawma))
					atrVawmaSeries[0] = vawma;
				else
					atrVawmaSeries.Reset();
			}

			if (!atr.IsValidDataPoint(0) || atr[0] <= 0)
			{
				upRawSeries.Reset();
				dnRawSeries.Reset();
				sTrend[0]				= trendState;
				unconfirmedSeries[0]	= unconfirmed;
				sActiveUpper.Reset();
				sActiveLower.Reset();
				Values[0].Reset();
				Values[1].Reset();
				return;
			}
			double atrVal = atr[0];

			// ── SuperTrend bands ─────────────────────────────────────────────
			double mult = AtrMultiplier;
			double upBase = Low[0]  - mult * atrVal;
			double dnBase = High[0] + mult * atrVal;

			bool hadPrior = upRawSeries.IsValidDataPoint(1);
			double up1 = hadPrior ? upRawSeries[1] : upBase;
			double dn1 = dnRawSeries.IsValidDataPoint(1) ? dnRawSeries[1] : dnBase;

			double up = (Low[1]  > up1) ? Math.Max(upBase, up1) : upBase;
			double dn = (High[1] < dn1) ? Math.Min(dnBase, dn1) : dnBase;

			upRawSeries[0] = up;
			dnRawSeries[0] = dn;

			// Initialize persistent state on the first bar the bands become valid.
			if (!hadPrior)
			{
				lastU			= up1;
				lastD			= dn1;
				trendState		= 0;
				confirmation	= 0;
				unconfirmed		= 0;
			}

			int prevTrend = trendState;

			// Gap cases — instant flip when price leaps past the opposite band.
			if (trendState != +1 && Low[0]  > lastD)
			{
				trendState = +1;
			}
			else if (trendState != -1 && High[0] < lastU)
			{
				trendState = -1;
			}
			// Confirmed cases — require CloseBars consecutive closes beyond the band.
			else if (trendState != +1 && High[0] > lastD)
			{
				unconfirmed += 1;
				if (confirmation < CloseBars && Close[1] > lastD)
					confirmation += 1;
				if (confirmation >= CloseBars)
					trendState = +1;
			}
			else if (trendState != -1 && Low[0]  < lastU)
			{
				unconfirmed += 1;
				if (confirmation < CloseBars && Close[1] < lastU)
					confirmation += 1;
				if (confirmation >= CloseBars)
					trendState = -1;
			}

			// Flip vs continuation handling.
			if (prevTrend != trendState)
			{
				lastU			= up1;
				lastD			= dn1;
				confirmation	= 0;
				unconfirmed		= 0;
			}
			else
			{
				lastU = (trendState == +1) ? Math.Max(lastU, up1) : up1;
				lastD = (trendState == -1) ? Math.Min(lastD, dn1) : dn1;

				// Trend clearly continuing — reset confirmation / unconfirmed counters.
				if (dnRawSeries.IsValidDataPoint(2) && upRawSeries.IsValidDataPoint(2))
				{
					double dn1Now  = dnRawSeries[1];
					double dn1Prev = dnRawSeries[2];
					double up1Now  = upRawSeries[1];
					double up1Prev = upRawSeries[2];
					if ((trendState == +1 && dn1Now > dn1Prev) ||
					    (trendState == -1 && up1Now < up1Prev))
					{
						confirmation	= 0;
						unconfirmed		= 0;
					}
				}
			}

			sTrend[0]				= trendState;
			unconfirmedSeries[0]	= unconfirmed;
			sActiveUpper[0]			= lastU;
			sActiveLower[0]			= lastD;

			int uncPrev = unconfirmedSeries[1];
			sWarning[0]  = (uncPrev == 0) && (unconfirmed > 0);
			sReversal[0] = (prevTrend != trendState);

			// ── Plots ────────────────────────────────────────────────────────
			if (trendState == +1)
			{
				Values[0][0] = lastU;
				Values[1].Reset();
				PlotBrushes[0][0] = (unconfirmed == 0) ? UpColor : UnconfirmedColor;
			}
			else
			{
				Values[0].Reset();
				Values[1][0] = lastD;
				PlotBrushes[1][0] = (unconfirmed == 0) ? DnColor : UnconfirmedColor;
			}

			// ── Buy / Sell reversal signals ──────────────────────────────────
			int priorTrend = sTrend[1];
			sBuySignal[0]  = (trendState == +1) && (priorTrend == -1);
			sSellSignal[0] = (trendState == -1) && (priorTrend == +1);

			if (sBuySignal[0])
			{
				Draw.Dot(this, "stUp_" + CurrentBar, true, 0, lastU, UpColor);
				if (ShowSignals)
					buyLabels[CurrentBar] = lastU;
			}
			if (sSellSignal[0])
			{
				Draw.Dot(this, "stDn_" + CurrentBar, true, 0, lastD, DnColor);
				if (ShowSignals)
					sellLabels[CurrentBar] = lastD;
			}
		}

		#endregion

		#region OnRenderTargetChanged

		public override void OnRenderTargetChanged()
		{
			try
			{
				ReleaseRenderResources();

				if (RenderTarget == null)
					return;

				float alpha = Math.Max(0.01f, Math.Min(1f, HighlightOpacity / 100f));
				dxUpFill		= MakeDXBrush(UpColor,       alpha);
				dxDnFill		= MakeDXBrush(DnColor,       alpha);
				dxBuyTextBrush	= MakeDXBrush(BuyTextColor,  1f);
				dxSellTextBrush	= MakeDXBrush(SellTextColor, 1f);

				BuildTextFormat();
			}
			catch
			{
				// Suppress during chart teardown.
			}
		}

		private void BuildTextFormat()
		{
			if (textFormat != null) { textFormat.Dispose(); textFormat = null; }
			textFormat = new SharpDX.DirectWrite.TextFormat(
				Core.Globals.DirectWriteFactory,
				"Arial",
				SharpDX.DirectWrite.FontWeight.Bold,
				SharpDX.DirectWrite.FontStyle.Normal,
				TextSize)
			{
				TextAlignment		= SharpDX.DirectWrite.TextAlignment.Center,
				ParagraphAlignment	= SharpDX.DirectWrite.ParagraphAlignment.Center
			};
			textFormatSize = TextSize;
		}

		private void ReleaseRenderResources()
		{
			if (dxUpFill        != null) { dxUpFill.Dispose();        dxUpFill        = null; }
			if (dxDnFill        != null) { dxDnFill.Dispose();        dxDnFill        = null; }
			if (dxBuyTextBrush  != null) { dxBuyTextBrush.Dispose();  dxBuyTextBrush  = null; }
			if (dxSellTextBrush != null) { dxSellTextBrush.Dispose(); dxSellTextBrush = null; }
			if (textFormat      != null) { textFormat.Dispose();      textFormat      = null; }
		}

		#endregion

		#region OnRender

		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			base.OnRender(chartControl, chartScale);

			if (RenderTarget == null)	return;
			if (ChartBars == null)		return;
			if (!IsVisible)				return;
			if (IsInHitTest)			return;

			bool drawFills  = Highlighting;
			bool drawLabels = ShowSignals && buyLabels != null && sellLabels != null &&
			                  (buyLabels.Count > 0 || sellLabels.Count > 0);

			if (!drawFills && !drawLabels)
				return;

			if (drawLabels && (textFormat == null || textFormatSize != TextSize))
				BuildTextFormat();

			int fromIdx = Math.Max(ChartBars.FromIndex, 0);
			int toIdx   = Math.Min(ChartBars.ToIndex,   CurrentBar);
			if (toIdx <= fromIdx) return;

			// ── Highlighter fills ────────────────────────────────────────────
			if (drawFills && sTrend != null && sActiveUpper != null && sActiveLower != null)
			{
				for (int j = fromIdx; j < toIdx; j++)
				{
					if (!sActiveUpper.IsValidDataPointAt(j) || !sActiveUpper.IsValidDataPointAt(j + 1)) continue;
					if (!sActiveLower.IsValidDataPointAt(j) || !sActiveLower.IsValidDataPointAt(j + 1)) continue;

					int trJ  = sTrend.GetValueAt(j);
					int trJ1 = sTrend.GetValueAt(j + 1);
					if (trJ == 0 || trJ1 == 0) continue;
					if (trJ != trJ1) continue; // skip trend-flip bar so fills don't bleed

					double upJ  = sActiveUpper.GetValueAt(j);
					double upJ1 = sActiveUpper.GetValueAt(j + 1);
					double dnJ  = sActiveLower.GetValueAt(j);
					double dnJ1 = sActiveLower.GetValueAt(j + 1);
					double c4J  = (Bars.GetOpen(j)     + Bars.GetHigh(j)     + Bars.GetLow(j)     + Bars.GetClose(j))     / 4.0;
					double c4J1 = (Bars.GetOpen(j + 1) + Bars.GetHigh(j + 1) + Bars.GetLow(j + 1) + Bars.GetClose(j + 1)) / 4.0;

					if (trJ == +1)
					{
						FillBarTrapezoid(chartControl, chartScale, j,
							topLeftPrice:  c4J,  botLeftPrice:  upJ,
							topRightPrice: c4J1, botRightPrice: upJ1,
							brush: dxUpFill);
					}
					else
					{
						FillBarTrapezoid(chartControl, chartScale, j,
							topLeftPrice:  dnJ,  botLeftPrice:  c4J,
							topRightPrice: dnJ1, botRightPrice: c4J1,
							brush: dxDnFill);
					}
				}
			}

			// ── Buy / Sell text labels ───────────────────────────────────────
			if (drawLabels)
			{
				foreach (var kv in buyLabels)
				{
					int bar = kv.Key;
					if (bar < fromIdx || bar > toIdx || bar > CurrentBar) continue;
					DrawSignalLabel(chartControl, chartScale, bar, kv.Value, "Buy",  anchorAbove: false, brush: dxBuyTextBrush);
				}

				foreach (var kv in sellLabels)
				{
					int bar = kv.Key;
					if (bar < fromIdx || bar > toIdx || bar > CurrentBar) continue;
					DrawSignalLabel(chartControl, chartScale, bar, kv.Value, "Sell", anchorAbove: true,  brush: dxSellTextBrush);
				}
			}
		}

		private void FillBarTrapezoid(ChartControl chartControl, ChartScale chartScale, int barLeftIdx,
			double topLeftPrice, double botLeftPrice, double topRightPrice, double botRightPrice,
			SharpDX.Direct2D1.Brush brush)
		{
			if (brush == null) return;

			float xL = chartControl.GetXByBarIndex(ChartBars, barLeftIdx);
			float xR = chartControl.GetXByBarIndex(ChartBars, barLeftIdx + 1);
			float yTL = (float)chartScale.GetYByValue(topLeftPrice);
			float yBL = (float)chartScale.GetYByValue(botLeftPrice);
			float yTR = (float)chartScale.GetYByValue(topRightPrice);
			float yBR = (float)chartScale.GetYByValue(botRightPrice);

			SharpDX.Direct2D1.PathGeometry geom = null;
			SharpDX.Direct2D1.GeometrySink sink = null;
			try
			{
				geom = new SharpDX.Direct2D1.PathGeometry(Core.Globals.D2DFactory);
				sink = geom.Open();
				sink.SetFillMode(SharpDX.Direct2D1.FillMode.Winding);
				sink.BeginFigure(new SharpDX.Vector2(xL, yTL), SharpDX.Direct2D1.FigureBegin.Filled);
				sink.AddLine(new SharpDX.Vector2(xR, yTR));
				sink.AddLine(new SharpDX.Vector2(xR, yBR));
				sink.AddLine(new SharpDX.Vector2(xL, yBL));
				sink.EndFigure(SharpDX.Direct2D1.FigureEnd.Closed);
				sink.Close();
				RenderTarget.FillGeometry(geom, brush);
			}
			finally
			{
				if (sink != null) sink.Dispose();
				if (geom != null) geom.Dispose();
			}
		}

		private void DrawSignalLabel(ChartControl chartControl, ChartScale chartScale,
			int absBarIdx, double priceAnchor, string text, bool anchorAbove,
			SharpDX.Direct2D1.SolidColorBrush brush)
		{
			if (textFormat == null || brush == null) return;

			float x = chartControl.GetXByBarIndex(ChartBars, absBarIdx);
			float y = (float)chartScale.GetYByValue(priceAnchor);

			float offsetPx = TextSize + 4f;
			y += anchorAbove ? -offsetPx - TextSize : offsetPx;

			float halfWidth = TextSize * 2.2f;
			var layoutRect = new SharpDX.RectangleF(x - halfWidth, y - TextSize, halfWidth * 2f, TextSize * 2f);
			RenderTarget.DrawText(text, textFormat, layoutRect, brush);
		}

		#endregion

		#region Helpers

		private SharpDX.Direct2D1.SolidColorBrush MakeDXBrush(Brush wpf, float alpha)
		{
			if (RenderTarget == null) return null;

			var scb = wpf as SolidColorBrush;
			System.Windows.Media.Color c = scb != null ? scb.Color : Colors.Gray;
			float wpfA = scb != null ? (float)scb.Opacity : 1f;

			return new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
				new SharpDX.Color4(c.R / 255f, c.G / 255f, c.B / 255f, Math.Max(0f, Math.Min(1f, alpha * wpfA))));
		}

		private static Brush EnsureFrozen(Brush b)
		{
			if (b != null && b.CanFreeze && !b.IsFrozen)
				b.Freeze();
			return b;
		}

		/// <summary>
		/// Volume-Adjusted Weighted Moving Average. Weights combine linear recency
		/// (most recent bar = period) with bar volume.
		/// </summary>
		private bool ComputeVAWMA(Series<double> src, int period, out double result)
		{
			result = 0;
			if (CurrentBar < period - 1) return false;

			double sum = 0, vol = 0;
			int last = period - 1;
			for (int i = 0; i <= last; i++)
			{
				if (!src.IsValidDataPoint(i)) continue;
				double s = src[i];
				double v = Volume[i];

				int m = last - i + 1;
				double vw = v * m;
				vol += vw;
				sum += s * vw;
			}
			if (vol == 0) return false;
			result = sum / vol;
			return true;
		}

		#endregion
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private indTradingView.SuperTrendPlus[] cacheSuperTrendPlus;
		public indTradingView.SuperTrendPlus SuperTrendPlus(SuperTrendPlus_MaMode mode, int atrPeriod, double atrMultiplier, double maxDeviation, int closeBars, bool showSignals, bool highlighting, int highlightOpacity, int textSize)
		{
			return SuperTrendPlus(Input, mode, atrPeriod, atrMultiplier, maxDeviation, closeBars, showSignals, highlighting, highlightOpacity, textSize);
		}

		public indTradingView.SuperTrendPlus SuperTrendPlus(ISeries<double> input, SuperTrendPlus_MaMode mode, int atrPeriod, double atrMultiplier, double maxDeviation, int closeBars, bool showSignals, bool highlighting, int highlightOpacity, int textSize)
		{
			if (cacheSuperTrendPlus != null)
				for (int idx = 0; idx < cacheSuperTrendPlus.Length; idx++)
					if (cacheSuperTrendPlus[idx] != null && cacheSuperTrendPlus[idx].Mode == mode && cacheSuperTrendPlus[idx].AtrPeriod == atrPeriod && cacheSuperTrendPlus[idx].AtrMultiplier == atrMultiplier && cacheSuperTrendPlus[idx].MaxDeviation == maxDeviation && cacheSuperTrendPlus[idx].CloseBars == closeBars && cacheSuperTrendPlus[idx].ShowSignals == showSignals && cacheSuperTrendPlus[idx].Highlighting == highlighting && cacheSuperTrendPlus[idx].HighlightOpacity == highlightOpacity && cacheSuperTrendPlus[idx].TextSize == textSize && cacheSuperTrendPlus[idx].EqualsInput(input))
						return cacheSuperTrendPlus[idx];
			return CacheIndicator<indTradingView.SuperTrendPlus>(new indTradingView.SuperTrendPlus(){ Mode = mode, AtrPeriod = atrPeriod, AtrMultiplier = atrMultiplier, MaxDeviation = maxDeviation, CloseBars = closeBars, ShowSignals = showSignals, Highlighting = highlighting, HighlightOpacity = highlightOpacity, TextSize = textSize }, input, ref cacheSuperTrendPlus);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.SuperTrendPlus SuperTrendPlus(SuperTrendPlus_MaMode mode, int atrPeriod, double atrMultiplier, double maxDeviation, int closeBars, bool showSignals, bool highlighting, int highlightOpacity, int textSize)
		{
			return indicator.SuperTrendPlus(Input, mode, atrPeriod, atrMultiplier, maxDeviation, closeBars, showSignals, highlighting, highlightOpacity, textSize);
		}

		public Indicators.indTradingView.SuperTrendPlus SuperTrendPlus(ISeries<double> input , SuperTrendPlus_MaMode mode, int atrPeriod, double atrMultiplier, double maxDeviation, int closeBars, bool showSignals, bool highlighting, int highlightOpacity, int textSize)
		{
			return indicator.SuperTrendPlus(input, mode, atrPeriod, atrMultiplier, maxDeviation, closeBars, showSignals, highlighting, highlightOpacity, textSize);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.SuperTrendPlus SuperTrendPlus(SuperTrendPlus_MaMode mode, int atrPeriod, double atrMultiplier, double maxDeviation, int closeBars, bool showSignals, bool highlighting, int highlightOpacity, int textSize)
		{
			return indicator.SuperTrendPlus(Input, mode, atrPeriod, atrMultiplier, maxDeviation, closeBars, showSignals, highlighting, highlightOpacity, textSize);
		}

		public Indicators.indTradingView.SuperTrendPlus SuperTrendPlus(ISeries<double> input , SuperTrendPlus_MaMode mode, int atrPeriod, double atrMultiplier, double maxDeviation, int closeBars, bool showSignals, bool highlighting, int highlightOpacity, int textSize)
		{
			return indicator.SuperTrendPlus(input, mode, atrPeriod, atrMultiplier, maxDeviation, closeBars, showSignals, highlighting, highlightOpacity, textSize);
		}
	}
}

#endregion
