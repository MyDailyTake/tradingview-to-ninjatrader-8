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

// NT8 Version of Volume Divergence Reversal Signals
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the Mozilla Public License 2.0 (MPL 2.0).
// The original Pine Script™ code is by AlgoAlpha and can be found at: https://www.tradingview.com/script/JSULpbV7-Volume-Divergence-Reversal-Signals-AlgoAlpha/
// Adaptation for NinjaTrader by jack@mydailytake.com
// Write-up + downloads: https://mydailytake.com/volume-divergence-reversal-signals-algoalpha-ninjatrader-8/
// Source + all conversions: https://github.com/MyDailyTake/tradingview-to-ninjatrader-8
// © 2026 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the Mozilla Public License 2.0. Full license details at https://mozilla.org/MPL/2.0/
// The use of AlgoAlpha name or its adapted code in this work does not imply endorsement by the original authors.
//
// Notes:
//   Volume normalized to a 0-100 scale by the highest volume in NormalizationLength bars. A peak-volume
//   pivot (LookbackLeft / LookbackRight bars on either side of the pivot bar) is the divergence anchor.
//
//   Bullish divergence: at the new volume pivot, price made a lower low than the prior pivot bar AND
//   volume made a higher peak — a "buyers absorbing" signal.
//   Bearish divergence: price made a higher high than the prior pivot but volume made a lower peak —
//   "weak rally on declining participation."
//
//   Documented scope decisions:
//     • Pine `ta.requestVolumeDelta(...)` (which polls a lower-timeframe series for buy − sell volume)
//       is TV-specific and not portable to NT8 without an extra tick-data subscription. The delta line
//       and the per-divergence "Vol / Δ" label are stripped — the divergence detection itself is
//       unchanged.
//     • Pine `color.from_gradient(volNorm, 0, 100, purple, yellow)` per-bar volume column color reproduced
//       via per-bar PlotBrushes interpolation.
//     • 3 alertconditions stripped per QC convention.
//
//   Non-repainting once the LookbackRight bars have closed (the pivot is confirmed N bars after the
//   peak). Public Series outputs: VolumeNormalized, BullDivergence, BearDivergence.

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	#region Categories
	[Gui.CategoryOrder("Volume",			10100)]
	[Gui.CategoryOrder("Divergence",		10200)]
	[Gui.CategoryOrder("Appearance",		10300)]
	#endregion

	public class VolumeDivergenceReversalSignals : Indicator
	{
		#region indInfo

		private string indName        = "Volume Divergence Reversal Signals [AlgoAlpha]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by AlgoAlpha can be found here: https://www.tradingview.com/script/JSULpbV7-Volume-Divergence-Reversal-Signals-AlgoAlpha/";

		#endregion

		#region Properties

		[NinjaScriptProperty]
		[Range(10, int.MaxValue)]
		[Display(Order = 1, GroupName = "Volume", Name = "Normalization Length",
			Description = "Lookback used to normalize volume into a 0-100 scale.")]
		public int NormalizationLength { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 2, GroupName = "Volume", Name = "Calculate Divergence",
			Description = "Detect regular bullish/bearish divergences between price and normalized volume.")]
		public bool CalculateDivergence { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 1, GroupName = "Divergence", Name = "Pivot Lookback Left",
			Description = "Bars to the left of a candidate pivot that must be lower for confirmation.")]
		public int LookbackLeft { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 2, GroupName = "Divergence", Name = "Pivot Lookback Right",
			Description = "Bars to the right of a candidate pivot that must be lower for confirmation.")]
		public int LookbackRight { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 3, GroupName = "Divergence", Name = "Max Bars Between Pivots",
			Description = "Maximum spacing between two consecutive pivots used in divergence checks.")]
		public int RangeUpper { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 4, GroupName = "Divergence", Name = "Min Bars Between Pivots",
			Description = "Minimum spacing between two consecutive pivots used in divergence checks.")]
		public int RangeLower { get; set; }

		[XmlIgnore]
		[Display(Order = 1, GroupName = "Appearance", Name = "Bullish Color",
			Description = "Color used for bullish divergence visuals.")]
		public Brush BullColor { get; set; }
			[Browsable(false)]
			public string BullColorSerialize
			{
				get { return Serialize.BrushToString(BullColor); }
				set { BullColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore]
		[Display(Order = 2, GroupName = "Appearance", Name = "Bearish Color",
			Description = "Color used for bearish divergence visuals.")]
		public Brush BearColor { get; set; }
			[Browsable(false)]
			public string BearColorSerialize
			{
				get { return Serialize.BrushToString(BearColor); }
				set { BearColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		#endregion

		#region Public outputs

		[Browsable(false)][XmlIgnore] public Series<double>	VolumeNormalized { get { return Values[0]; } }
		[Browsable(false)][XmlIgnore] public Series<bool>	BullDivergence    { get { Update(); return sBullDiv; } }
		[Browsable(false)][XmlIgnore] public Series<bool>	BearDivergence    { get { Update(); return sBearDiv; } }

		#endregion

		#region Variables

		private Series<double>	sVolNorm;
		// Pre-built purple→yellow column palette (32 steps) — the ramp is a fixed hard-coded gradient,
		// so per-bar colouring indexes this instead of allocating a new brush every bar.
		private const int	GradSteps = 32;
		private Brush[]		rampPY;
		private Series<bool>	sBullDiv;
		private Series<bool>	sBearDiv;
		private MAX				maxVol;

		// Most-recent confirmed pivot anchors (volume-pivot value, price-low, price-high, bar index).
		private double	lastPivotVol  = double.NaN;
		private double	lastPivotLow  = double.NaN;
		private double	lastPivotHigh = double.NaN;
		private int		lastPivotBar  = -1;

		#endregion

		#region OnStateChange

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description					= indDescription;
				Name						= indName;
				Calculate					= Calculate.OnBarClose;
				IsOverlay					= false;
				DisplayInDataBox			= true;
				DrawOnPricePanel			= false;
				DrawHorizontalGridLines		= true;
				DrawVerticalGridLines		= true;
				PaintPriceMarkers			= true;
				ScaleJustification			= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				IsSuspendedWhileInactive	= true;

				NormalizationLength		= 1000;
				CalculateDivergence		= true;
				LookbackLeft			= 21;
				LookbackRight			= 5;
				RangeUpper				= 60;
				RangeLower				= 5;

				BullColor = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0xBB));
				BearColor = new SolidColorBrush(Color.FromRgb(0xFF, 0x11, 0x00));
				EnsureFrozen(BullColor);
				EnsureFrozen(BearColor);

				AddPlot(new Stroke(Brushes.DimGray, DashStyleHelper.Solid, 1f), PlotStyle.Bar, "Volume Normalized");

				AddLine(new Stroke(new SolidColorBrush(Color.FromArgb(96, 192, 192, 192)), DashStyleHelper.Dot, 1f), 100, "Top");
			}
			else if (State == State.DataLoaded)
			{
				// NormalizationLength can exceed default 256 lookback — pass the user's length
				// directly to the NT MAX primitive, which sizes its own buffer.
				maxVol   = MAX(Volume, NormalizationLength);
				sVolNorm = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sBullDiv = new Series<bool>(this,   MaximumBarsLookBack.Infinite);
				sBearDiv = new Series<bool>(this,   MaximumBarsLookBack.Infinite);

				// Build the fixed purple→yellow column palette once.
				rampPY = new Brush[GradSteps];
				for (int i = 0; i < GradSteps; i++)
					rampPY[i] = GradientPurpleYellow((double)i / (GradSteps - 1) * 100.0);
			}
		}

		#endregion

		#region OnBarUpdate

		protected override void OnBarUpdate()
		{
			double curVol = Volume[0];
			double maxV   = maxVol[0];
			double vn     = maxV > 0 ? curVol / maxV * 100.0 : 0.0;
			sVolNorm[0]   = vn;
			Values[0][0]  = vn;

			// Per-bar gradient for the column color: dark purple (RGB 106,0,125) at 0 → yellow at 100.
			double vnc = vn;
			if (vnc < 0)   vnc = 0;
			if (vnc > 100) vnc = 100;
			int pyIdx = Math.Min(GradSteps - 1, Math.Max(0, (int)Math.Round(vnc / 100.0 * (GradSteps - 1))));
			PlotBrushes[0][0] = rampPY[pyIdx];

			sBullDiv[0] = false;
			sBearDiv[0] = false;

			if (!CalculateDivergence) return;

			// Pivot confirmation requires LookbackLeft past + LookbackRight future bars around the pivot.
			int totalNeeded = LookbackLeft + LookbackRight;
			if (CurrentBar < totalNeeded) return;

			// Candidate pivot bar is `LookbackRight` bars in the past.
			int pivotBarsAgo = LookbackRight;
			double pivotVol  = sVolNorm[pivotBarsAgo];
			double pivotLow  = Low[pivotBarsAgo];
			double pivotHigh = High[pivotBarsAgo];

			// Confirm pivot — the candidate is a peak vs. all bars on both sides within their lookbacks.
			bool isPivot = true;
			for (int k = 1; k <= LookbackLeft && isPivot; k++)
				if (sVolNorm[pivotBarsAgo + k] >= pivotVol) isPivot = false;
			for (int k = 1; k <= LookbackRight && isPivot; k++)
				if (sVolNorm[pivotBarsAgo - k] >= pivotVol) isPivot = false;

			if (!isPivot) return;

			int pivotBarIdx = CurrentBar - pivotBarsAgo;

			if (!double.IsNaN(lastPivotVol))
			{
				int gap = pivotBarIdx - lastPivotBar;
				if (gap >= RangeLower && gap <= RangeUpper)
				{
					bool volHL    = pivotVol > lastPivotVol;	// volume made a higher peak
					bool volLH    = pivotVol < lastPivotVol;	// volume made a lower peak
					bool priceLL  = pivotLow < lastPivotLow;
					bool priceHH  = pivotHigh > lastPivotHigh;

					if (priceLL && volHL)
					{
						sBullDiv[pivotBarsAgo] = true;
						Draw.TriangleUp(this, "vdrBull" + pivotBarIdx, false, pivotBarsAgo, pivotVol, BullColor);
						DrawOnPricePanel = true;
						Draw.TriangleUp(this, "vdrBullPx" + pivotBarIdx, true, pivotBarsAgo, pivotLow - TickSize * 4, BullColor);
						Draw.Line(this, "vdrBullLn" + pivotBarIdx, true,
							CurrentBar - lastPivotBar + pivotBarsAgo, lastPivotLow,
							pivotBarsAgo, pivotLow, BullColor, DashStyleHelper.Solid, 2);
						DrawOnPricePanel = false;
						Draw.Line(this, "vdrBullVLn" + pivotBarIdx, false,
							CurrentBar - lastPivotBar + pivotBarsAgo, lastPivotVol,
							pivotBarsAgo, pivotVol, BullColor, DashStyleHelper.Solid, 2);
					}

					if (priceHH && volLH)
					{
						sBearDiv[pivotBarsAgo] = true;
						Draw.TriangleDown(this, "vdrBear" + pivotBarIdx, false, pivotBarsAgo, pivotVol, BearColor);
						DrawOnPricePanel = true;
						Draw.TriangleDown(this, "vdrBearPx" + pivotBarIdx, true, pivotBarsAgo, pivotHigh + TickSize * 4, BearColor);
						Draw.Line(this, "vdrBearLn" + pivotBarIdx, true,
							CurrentBar - lastPivotBar + pivotBarsAgo, lastPivotHigh,
							pivotBarsAgo, pivotHigh, BearColor, DashStyleHelper.Solid, 2);
						DrawOnPricePanel = false;
						Draw.Line(this, "vdrBearVLn" + pivotBarIdx, false,
							CurrentBar - lastPivotBar + pivotBarsAgo, lastPivotVol,
							pivotBarsAgo, pivotVol, BearColor, DashStyleHelper.Solid, 2);
					}
				}
			}

			lastPivotVol  = pivotVol;
			lastPivotLow  = pivotLow;
			lastPivotHigh = pivotHigh;
			lastPivotBar  = pivotBarIdx;
		}

		#endregion

		#region Helpers

		private static Brush GradientPurpleYellow(double v)
		{
			// Linear gradient from RGB(106,0,125) at v=0 to yellow at v=100.
			double t = Math.Max(0.0, Math.Min(1.0, v / 100.0));
			byte R = (byte)(106 + (255 - 106) * t);
			byte G = (byte)(  0 + (255 -   0) * t);
			byte B = (byte)(125 + (  0 - 125) * t);
			var b = new SolidColorBrush(Color.FromRgb(R, G, B));
			b.Freeze();
			return b;
		}

		private static Brush EnsureFrozen(Brush b)
		{
			if (b != null && b.CanFreeze && !b.IsFrozen) b.Freeze();
			return b;
		}

		#endregion
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private indTradingView.VolumeDivergenceReversalSignals[] cacheVolumeDivergenceReversalSignals;
		public indTradingView.VolumeDivergenceReversalSignals VolumeDivergenceReversalSignals(int normalizationLength, bool calculateDivergence, int lookbackLeft, int lookbackRight, int rangeUpper, int rangeLower)
		{
			return VolumeDivergenceReversalSignals(Input, normalizationLength, calculateDivergence, lookbackLeft, lookbackRight, rangeUpper, rangeLower);
		}

		public indTradingView.VolumeDivergenceReversalSignals VolumeDivergenceReversalSignals(ISeries<double> input, int normalizationLength, bool calculateDivergence, int lookbackLeft, int lookbackRight, int rangeUpper, int rangeLower)
		{
			if (cacheVolumeDivergenceReversalSignals != null)
				for (int idx = 0; idx < cacheVolumeDivergenceReversalSignals.Length; idx++)
					if (cacheVolumeDivergenceReversalSignals[idx] != null && cacheVolumeDivergenceReversalSignals[idx].NormalizationLength == normalizationLength && cacheVolumeDivergenceReversalSignals[idx].CalculateDivergence == calculateDivergence && cacheVolumeDivergenceReversalSignals[idx].LookbackLeft == lookbackLeft && cacheVolumeDivergenceReversalSignals[idx].LookbackRight == lookbackRight && cacheVolumeDivergenceReversalSignals[idx].RangeUpper == rangeUpper && cacheVolumeDivergenceReversalSignals[idx].RangeLower == rangeLower && cacheVolumeDivergenceReversalSignals[idx].EqualsInput(input))
						return cacheVolumeDivergenceReversalSignals[idx];
			return CacheIndicator<indTradingView.VolumeDivergenceReversalSignals>(new indTradingView.VolumeDivergenceReversalSignals(){ NormalizationLength = normalizationLength, CalculateDivergence = calculateDivergence, LookbackLeft = lookbackLeft, LookbackRight = lookbackRight, RangeUpper = rangeUpper, RangeLower = rangeLower }, input, ref cacheVolumeDivergenceReversalSignals);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.VolumeDivergenceReversalSignals VolumeDivergenceReversalSignals(int normalizationLength, bool calculateDivergence, int lookbackLeft, int lookbackRight, int rangeUpper, int rangeLower)
		{
			return indicator.VolumeDivergenceReversalSignals(Input, normalizationLength, calculateDivergence, lookbackLeft, lookbackRight, rangeUpper, rangeLower);
		}

		public Indicators.indTradingView.VolumeDivergenceReversalSignals VolumeDivergenceReversalSignals(ISeries<double> input , int normalizationLength, bool calculateDivergence, int lookbackLeft, int lookbackRight, int rangeUpper, int rangeLower)
		{
			return indicator.VolumeDivergenceReversalSignals(input, normalizationLength, calculateDivergence, lookbackLeft, lookbackRight, rangeUpper, rangeLower);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.VolumeDivergenceReversalSignals VolumeDivergenceReversalSignals(int normalizationLength, bool calculateDivergence, int lookbackLeft, int lookbackRight, int rangeUpper, int rangeLower)
		{
			return indicator.VolumeDivergenceReversalSignals(Input, normalizationLength, calculateDivergence, lookbackLeft, lookbackRight, rangeUpper, rangeLower);
		}

		public Indicators.indTradingView.VolumeDivergenceReversalSignals VolumeDivergenceReversalSignals(ISeries<double> input , int normalizationLength, bool calculateDivergence, int lookbackLeft, int lookbackRight, int rangeUpper, int rangeLower)
		{
			return indicator.VolumeDivergenceReversalSignals(input, normalizationLength, calculateDivergence, lookbackLeft, lookbackRight, rangeUpper, rangeLower);
		}
	}
}

#endregion
