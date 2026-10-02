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

// NT8 Version of Liquidity Hunter [ChartPrime]
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the Mozilla Public License 2.0 (MPL 2.0).
// The original Pine Script™ code is by ChartPrime and can be found at: https://www.tradingview.com/script/E9z8Isgd-Liquidity-Hunter-ChartPrime/
// Adaptation for NinjaTrader by jack@mydailytake.com
// © 2026 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the Mozilla Public License 2.0. Full license details at https://mozilla.org/MPL/2.0/
// The use of ChartPrime name or its adapted code in this work does not imply endorsement by the original authors.
//
// Notes:
//   Long-bias liquidity-grab detector. A bar qualifies as a long setup when ALL of the following match
//   on the previous closed bar:
//     • Body size as % of range ≤ Body% threshold
//     • Lower-wick size as % of range ≥ Wick% threshold
//     • Slope of close[1] vs close[2] over the bar's time delta is positive
//     • Half-3-bar ATR-based "z-band" is non-decreasing (volatility steady or expanding)
//
//   On a fresh setup the indicator marks the candle, computes targets:
//     • SL = close − |close − x2|, where x2 = low − ATR(14) × 1.5 (RMA TR-based)
//     • TP = close + RR × |close − x2|
//     • CHOCH = entry + (RR/2) × |close − x2| (intermediate confirmation level)
//     • BOS = low − Z×15 (failure level)
//
//   While the trade is "on", the dashed TP line extends with the bar; CHOCH/BOS hits are labeled and the
//   target label color flips green on TP-hit / red on SL-hit.
//
//   Documented scope decisions:
//     • Pine `barcolor()` for active trade reproduced via per-bar BarBrushes recolor.
//     • Lower Wick % / Body % data-window plots dropped — exposed instead as Series outputs
//       (LowerWickPctSeries, BodyPctSeries) for strategy consumers.
//     • Z-band label rendered with a fixed 9pt font (NT has no `text_size = size.auto` equivalent).
//
//   Non-repainting: setup detection uses bar [1]'s closed values; CHOCH / BOS / TP / SL hits use closed-bar
//   high/low/close. Public Series outputs: LongActive, LowerWickPct, BodyPct.

#region Enums LiquidityHunter

public enum LiquidityHunter_TextSize
{
	Tiny,
	Small,
	Normal,
	Large
}

#endregion

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	#region Categories
	[Gui.CategoryOrder("Core Settings",	10100)]
	#endregion

	public class LiquidityHunter : Indicator
	{
		#region indInfo

		private string indName        = "Liquidity Hunter [ChartPrime]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by ChartPrime can be found here: https://www.tradingview.com/script/E9z8Isgd-Liquidity-Hunter-ChartPrime/";

		#endregion

		#region Properties

		[NinjaScriptProperty]
		[Range(0.1, 100.0)]
		[Display(Order = 1, GroupName = "Core Settings", Name = "Body %",
			Description = "Body size as % of bar range must be at most this value.")]
		public double BodyPct { get; set; }

		[NinjaScriptProperty]
		[Range(0.1, 100.0)]
		[Display(Order = 2, GroupName = "Core Settings", Name = "Wick %",
			Description = "Lower-wick size as % of bar range must be at least this value.")]
		public double WickPct { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 3, GroupName = "Core Settings", Name = "Show Targets",
			Description = "Render the dashed TP line and target label.")]
		public bool ShowTargets { get; set; }

		[NinjaScriptProperty]
		[Range(0.1, 50.0)]
		[Display(Order = 4, GroupName = "Core Settings", Name = "Target RR",
			Description = "Risk-reward multiplier sized into TP distance.")]
		public double TargetRR { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 5, GroupName = "Core Settings", Name = "Label Text Size",
			Description = "Text size for wick %, Target, CHOCH, and BOS labels.")]
		public LiquidityHunter_TextSize LabelTextSize { get; set; }

		#endregion

		#region Public outputs

		[Browsable(false)][XmlIgnore] public Series<bool>	LongActive         { get { Update(); return sLongActive; } }
		[Browsable(false)][XmlIgnore] public Series<double>	LowerWickPctSeries { get { Update(); return sLowerWickPct; } }
		[Browsable(false)][XmlIgnore] public Series<double>	BodyPctSeries      { get { Update(); return sBodyPct; } }

		#endregion

		#region Variables

		private ATR atr5;
		private ATR atr14;	// uses RMA TR — NT's ATR is RMA by default

		private Series<bool>	sLongActive;
		private Series<double>	sLowerWickPct;
		private Series<double>	sBodyPct;

		// Trade state.
		private bool	tradeOn;
		private bool	chochOn;
		private bool	bosOn;
		private int		longBar;
		private double	longLow;
		private double	tp, sl, choch, bos;
		private string	tpLabelTag = null;
		private string	tpLineTag  = null;

		// Cached constant-colour WPF brushes — built once, frozen. Prevents NT8's
		// "exceeded the maximum number of unique brushes" error caused by per-bar /
		// per-event `new SolidColorBrush(...)` construction of these fixed colours.
		private static readonly SolidColorBrush _setupBrush;    // 0xBDF71D — setup border + area
		private static readonly SolidColorBrush _tpLineBrush;    // 0x047B88 — TP riser + extending TP line
		private static readonly SolidColorBrush _tpLabelBgBrush; // 0x666664 — Target label background
		private static readonly SolidColorBrush _chochBrush;     // 0x185D04 — CHOCH line
		private static readonly SolidColorBrush _bosBrush;       // 0x890303 — BOS line/riser
		private static readonly SolidColorBrush _tpHitBrush;     // argb 95,0x06,0x80,0x0A — TP-hit label bg
		private static readonly SolidColorBrush _slHitBrush;     // argb 178,0xF6,0x07,0x07 — SL-hit label bg
		private static readonly SolidColorBrush _barBrush;       // 0x06F947 — active-trade bar / candle outline

		static LiquidityHunter()
		{
			_setupBrush     = new SolidColorBrush(Color.FromRgb(0xBD, 0xF7, 0x1D));  _setupBrush.Freeze();
			_tpLineBrush    = new SolidColorBrush(Color.FromRgb(0x04, 0x7B, 0x88));  _tpLineBrush.Freeze();
			_tpLabelBgBrush = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x64));  _tpLabelBgBrush.Freeze();
			_chochBrush     = new SolidColorBrush(Color.FromRgb(0x18, 0x5D, 0x04));  _chochBrush.Freeze();
			_bosBrush       = new SolidColorBrush(Color.FromRgb(0x89, 0x03, 0x03));  _bosBrush.Freeze();
			_tpHitBrush     = new SolidColorBrush(Color.FromArgb(95, 0x06, 0x80, 0x0A));   _tpHitBrush.Freeze();
			_slHitBrush     = new SolidColorBrush(Color.FromArgb(178, 0xF6, 0x07, 0x07));  _slHitBrush.Freeze();
			_barBrush       = new SolidColorBrush(Color.FromRgb(0x06, 0xF9, 0x47));  _barBrush.Freeze();
		}

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

				BodyPct			= 30;
				WickPct			= 60;
				ShowTargets		= true;
				TargetRR		= 1.5;
				LabelTextSize	= LiquidityHunter_TextSize.Small;

				// Lower Wick % / Body % are diagnostic series only — exposed for strategy / data-window
				// consumption but NOT plotted on the price overlay (their 0-100 scale would crush the
				// price y-axis on a price-panel indicator).
			}
			else if (State == State.DataLoaded)
			{
				atr5  = ATR(5);
				atr14 = ATR(14);
				sLongActive   = new Series<bool>(this,   MaximumBarsLookBack.Infinite);
				sLowerWickPct = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sBodyPct      = new Series<double>(this, MaximumBarsLookBack.Infinite);
			}
		}

		#endregion

		#region OnBarUpdate

		protected override void OnBarUpdate()
		{
			if (CurrentBar < 25) return;

			// Per-bar diagnostics for the data window / strategy consumption (not plotted).
			double bRange = Math.Abs(High[1] - Low[1]);
			double lowWick = Math.Min(Open[1], Close[1]) - Low[1];
			double lowerWickPct = bRange > 0 ? lowWick / bRange * 100.0 : 0.0;
			double bodyPctVal   = bRange > 0 ? Math.Abs(Open[1] - Close[1]) / bRange * 100.0 : 0.0;
			sLowerWickPct[0] = lowerWickPct;
			sBodyPct[0]      = bodyPctVal;

			// Half-band reference: min(ATR(len)*0.3, close*0.003) on the bar 20 bars ago, halved.
			double zBand    = HalfBand(30);
			double zBand5   = HalfBand(5);
			double zBand5_1 = HalfBand5OneAgo();

			// Long condition.
			TimeSpan timeDelta = CurrentBar > 5 ? Time[5] - Time[6] : TimeSpan.FromMinutes(1);
			double slope = timeDelta.Ticks > 0 ? (Close[1] - Close[2]) / timeDelta.Ticks : 0.0;
			double timeAxis = timeDelta.Ticks > 0 ? (double)Time[0].Ticks : 1.0;	// effective `slope * time > 0` truth check
			bool slopePositive = slope * timeAxis > 0;

			bool longCondition = bodyPctVal <= BodyPct
				&& lowerWickPct >= WickPct
				&& slopePositive
				&& zBand5 >= zBand5_1;

			if (longCondition && !tradeOn)
			{
				longBar = CurrentBar;
				longLow = Low[0];

				double trueRange = atr14[0];
				double x2 = Low[0] - trueRange * 1.5;
				double longDiffSL = Math.Abs(Close[0] - x2);

				tp    = Close[0] + TargetRR * longDiffSL;
				choch = Close[0] + TargetRR / 2.0 * longDiffSL;
				sl    = Close[0] - longDiffSL;
				bos   = Low[0] - zBand * 15;

				// Setup highlight box around the bar — entry + target zone shaded.
				var setupBorder = _setupBrush;
				var setupArea   = _setupBrush;
				setupBorder.Freeze();
				setupArea.Freeze();
				Draw.Rectangle(this, "lhSetup" + CurrentBar, false, 1, High[0] + zBand * 0.5, -1, Low[0] - zBand * 0.5,
					setupBorder, setupArea, 20);

				// Wick % label below the setup, rendered in a translucent box matching the setup tint.
				string wickMsg = string.Format("{0:0.##} %", lowerWickPct);
				Draw.Text(this, "lhWick" + CurrentBar, false, wickMsg, 0, Low[0] - zBand * 1.5, 0, Brushes.WhiteSmoke,
					new SimpleFont("Arial", FontPx()), TextAlignment.Center, Brushes.Transparent,
					setupArea, 30);

				if (ShowTargets)
				{
					// Vertical TP riser from current high → TP price.
					Draw.Line(this, "lhRise" + CurrentBar, false, 0, High[0], 0, tp,
						_tpLineBrush, DashStyleHelper.Dash, 2);

					// Horizontal TP line that extends as the trade plays out.
					tpLineTag = "lhTP" + CurrentBar;
					Draw.Line(this, tpLineTag, false, 0, tp, -2, tp,
						_tpLineBrush, DashStyleHelper.Dash, 1);

					tpLabelTag = "lhTPlbl" + CurrentBar;
					Draw.Text(this, tpLabelTag, false, "Target", -1, tp, 0, Brushes.White,
						new SimpleFont("Arial", FontPx()), TextAlignment.Center, Brushes.Transparent,
						_tpLabelBgBrush, 100);
				}

				tradeOn = true;
				chochOn = false;
				bosOn   = false;
			}

			if (tradeOn)
			{
				// Extend the TP line + target label so they track the live bar (Pine's line.set_x2).
				if (ShowTargets && tpLineTag != null)
				{
					int setupBarsAgo = Math.Max(0, CurrentBar - longBar);
					Draw.Line(this, tpLineTag, false, setupBarsAgo, tp, 0, tp,
						_tpLineBrush, DashStyleHelper.Dash, 1);
				}
				if (ShowTargets && tpLabelTag != null)
				{
					Draw.Text(this, tpLabelTag, false, "Target", 0, tp, 0, Brushes.White,
						new SimpleFont("Arial", FontPx()), TextAlignment.Center, Brushes.Transparent,
						_tpLabelBgBrush, 100);
				}

				// CHOCH cross — close ≥ choch.
				if (Close[0] >= choch && !chochOn)
				{
					int chochBarsAgo = Math.Max(0, CurrentBar - longBar);
					Draw.Line(this, "lhChoch" + CurrentBar, false, chochBarsAgo, choch, 0, choch,
						_chochBrush, DashStyleHelper.Dash, 1);
					Draw.Text(this, "lhChochLbl" + CurrentBar, false, "CHOCH", chochBarsAgo / 2, choch + zBand * 1.1, 0,
						Brushes.White, new SimpleFont("Arial", FontPx()), TextAlignment.Center, Brushes.Transparent, Brushes.Transparent, 0);
					chochOn = true;
				}

				// BOS cross — close ≤ bos.
				if (Close[0] <= bos && !bosOn)
				{
					int longBarsAgo = Math.Max(0, CurrentBar - longBar);
					Draw.Line(this, "lhBos" + CurrentBar, false, longBarsAgo, bos, 0, bos,
						_bosBrush, DashStyleHelper.Dash, 1);
					Draw.Text(this, "lhBosLbl" + CurrentBar, false, "BOS", longBarsAgo / 2, bos - zBand * 1.1, 0,
						Brushes.White, new SimpleFont("Arial", FontPx()), TextAlignment.Center, Brushes.Transparent, Brushes.Transparent, 0);
					Draw.Line(this, "lhBosV" + CurrentBar, false, longBarsAgo, longLow - zBand * 2.5, longBarsAgo, bos,
						_bosBrush, DashStyleHelper.Dash, 1);
					bosOn = true;
				}

				// TP hit / SL hit — close out the trade and recolor the target label.
				if (High[0] >= tp)
				{
					if (tpLabelTag != null)
						Draw.Text(this, tpLabelTag, false, "Target", -1, tp, 0, Brushes.White,
							new SimpleFont("Arial", FontPx()), TextAlignment.Center, Brushes.Transparent,
							_tpHitBrush, 100);
					tradeOn = false;
				}
				else if (Close[0] <= sl)
				{
					if (tpLabelTag != null)
						Draw.Text(this, tpLabelTag, false, "Target", -1, tp, 0, Brushes.White,
							new SimpleFont("Arial", FontPx()), TextAlignment.Center, Brushes.Transparent,
							_slHitBrush, 100);
					tradeOn = false;
				}
			}

			sLongActive[0] = tradeOn;
			if (tradeOn)
			{
				BarBrushes[0] = _barBrush;
				CandleOutlineBrushes[0] = BarBrushes[0];
			}
		}

		// `min(ATR(len)*0.3, close*0.003)` evaluated 20 bars ago, halved.
		private double HalfBand(int len)
		{
			if (CurrentBar < 21) return 0;
			double atrPart    = (len == 5 ? atr5[0] : atr14[0]) * 0.3;	// approximation: only ATR(5) and ATR(14) cached
			if (len != 5 && len != 14) atrPart = atr14[0] * 0.3;	// fallback for other lengths
			double pricePart  = Close[0] * 0.003;
			double current    = Math.Min(atrPart, pricePart);
			// 20-bar shift — recompute on bar 20 ago using the same formula.
			double atrPart20  = (len == 5 ? atr5[20] : atr14[20]) * 0.3;
			if (len != 5 && len != 14) atrPart20 = atr14[20] * 0.3;
			double pricePart20 = Close[20] * 0.003;
			return Math.Min(atrPart20, pricePart20) / 2.0;
		}

		private double HalfBand5OneAgo()
		{
			if (CurrentBar < 22) return 0;
			double atrPart    = atr5[1] * 0.3;
			double pricePart  = Close[1] * 0.003;
			double atrPart21  = atr5[21] * 0.3;
			double pricePart21 = Close[21] * 0.003;
			return Math.Min(atrPart21, pricePart21) / 2.0;
		}

		private int FontPx()
		{
			switch (LabelTextSize)
			{
				case LiquidityHunter_TextSize.Tiny:   return 8;
				case LiquidityHunter_TextSize.Small:  return 9;
				case LiquidityHunter_TextSize.Normal: return 12;
				case LiquidityHunter_TextSize.Large:  return 16;
			}
			return 9;
		}

		#endregion
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private indTradingView.LiquidityHunter[] cacheLiquidityHunter;
		public indTradingView.LiquidityHunter LiquidityHunter(double bodyPct, double wickPct, bool showTargets, double targetRR, LiquidityHunter_TextSize labelTextSize)
		{
			return LiquidityHunter(Input, bodyPct, wickPct, showTargets, targetRR, labelTextSize);
		}

		public indTradingView.LiquidityHunter LiquidityHunter(ISeries<double> input, double bodyPct, double wickPct, bool showTargets, double targetRR, LiquidityHunter_TextSize labelTextSize)
		{
			if (cacheLiquidityHunter != null)
				for (int idx = 0; idx < cacheLiquidityHunter.Length; idx++)
					if (cacheLiquidityHunter[idx] != null && cacheLiquidityHunter[idx].BodyPct == bodyPct && cacheLiquidityHunter[idx].WickPct == wickPct && cacheLiquidityHunter[idx].ShowTargets == showTargets && cacheLiquidityHunter[idx].TargetRR == targetRR && cacheLiquidityHunter[idx].LabelTextSize == labelTextSize && cacheLiquidityHunter[idx].EqualsInput(input))
						return cacheLiquidityHunter[idx];
			return CacheIndicator<indTradingView.LiquidityHunter>(new indTradingView.LiquidityHunter(){ BodyPct = bodyPct, WickPct = wickPct, ShowTargets = showTargets, TargetRR = targetRR, LabelTextSize = labelTextSize }, input, ref cacheLiquidityHunter);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.LiquidityHunter LiquidityHunter(double bodyPct, double wickPct, bool showTargets, double targetRR, LiquidityHunter_TextSize labelTextSize)
		{
			return indicator.LiquidityHunter(Input, bodyPct, wickPct, showTargets, targetRR, labelTextSize);
		}

		public Indicators.indTradingView.LiquidityHunter LiquidityHunter(ISeries<double> input , double bodyPct, double wickPct, bool showTargets, double targetRR, LiquidityHunter_TextSize labelTextSize)
		{
			return indicator.LiquidityHunter(input, bodyPct, wickPct, showTargets, targetRR, labelTextSize);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.LiquidityHunter LiquidityHunter(double bodyPct, double wickPct, bool showTargets, double targetRR, LiquidityHunter_TextSize labelTextSize)
		{
			return indicator.LiquidityHunter(Input, bodyPct, wickPct, showTargets, targetRR, labelTextSize);
		}

		public Indicators.indTradingView.LiquidityHunter LiquidityHunter(ISeries<double> input , double bodyPct, double wickPct, bool showTargets, double targetRR, LiquidityHunter_TextSize labelTextSize)
		{
			return indicator.LiquidityHunter(input, bodyPct, wickPct, showTargets, targetRR, labelTextSize);
		}
	}
}

#endregion
