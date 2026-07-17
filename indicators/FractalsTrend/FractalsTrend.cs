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

// NT8 Version of Fractals Trend
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under a Attribution-NonCommercial-ShareAlike 4.0 International.
// The original Pine Script™ code is by BigBeluga and can be found at: https://www.tradingview.com/script/aU8krboM-Fractals-Trend-BigBeluga/
// Adaptation for NinjaTrader by jack@mydailytake.com
// Write-up + downloads: https://mydailytake.com/fractals-trend-bigbeluga-ninjatrader-8/
// Source + all conversions: https://github.com/MyDailyTake/tradingview-to-ninjatrader-8
// © 2026 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the Attribution-NonCommercial-ShareAlike 4.0 International (CC BY-NC-SA 4.0) https://creativecommons.org/licenses/by-nc-sa/4.0/
// The use of BigBeluga name or its adapted code in this work does not imply endorsement by the original authors.
//
// Notes:
//   Detects swing fractals over a 2*FractalLen+1 window, stores the most recent FCount per side,
//   and reduces them to upper / lower bands (avg, extreme, or median). Trend flips when close
//   crosses the band on the opposite side. The active band is filled toward HL2.
//
//   Non-repainting CHANNEL — the band value for any closed bar is fixed at that bar's close
//   and never revised; TrendSeries / FractalLineSeries are safe for strategy consumption.
//
//   Fractal MARKERS do appear retroactively — a pivot at bar X-FractalLen is detected and
//   drawn on bar X's close (i.e. FractalLen bars after it formed). The annotation is added to
//   an already-closed bar; price action and band history are not rewritten, but a marker that
//   wasn't on the chart a moment ago can show up on a past bar. Strategies should rely on
//   TrendSeries / FractalLineSeries, not the visual markers.
//
//   Public Series outputs: TrendSeries, FractalLineSeries.

#region Enums FractalsTrend

public enum FractalsTrend_BandsType
{
	Avg,
	MinMax,
	Median
}

#endregion

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	#region Categories
	[Gui.CategoryOrder("Indicator Setup",	10100)]
	[Gui.CategoryOrder("Display",			10200)]
	#endregion

	public class FractalsTrend : Indicator
	{
		#region indInfo

		private string indName        = "Fractals Trend [BigBeluga]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by BigBeluga can be found here: https://www.tradingview.com/script/aU8krboM-Fractals-Trend-BigBeluga/";

		#endregion

		#region Properties

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 1, GroupName = "Indicator Setup", Name = "Fractals Detection Length",
			Description = "Number of bars on each side of a pivot used to confirm a swing fractal.")]
		public int FractalLen { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 2, GroupName = "Indicator Setup", Name = "Fractals Storage Qty",
			Description = "How many recent fractals (per side) feed the band calculation.")]
		public int FCount { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 3, GroupName = "Indicator Setup", Name = "Bands Type",
			Description = "How stored fractals are reduced to a band: average, extreme (max for upper / min for lower), or median.")]
		public FractalsTrend_BandsType TypeStore { get; set; }

		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Order = 4, GroupName = "Indicator Setup", Name = "Shadow Transparency",
			Description = "Trend-fill transparency (0 = solid, 100 = fill fully off).")]
		public int Shadow { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 5, GroupName = "Display", Name = "Show Fractal Markers",
			Description = "Draw a marker on each detected swing fractal at the pivot bar (FractalLen bars back).")]
		public bool DisplayMarkers { get; set; }

		[XmlIgnore]
		[Display(Order = 10, GroupName = "Display", Name = "Up Trend Color",
			Description = "Color of the line, fill, and lower-fractal markers while in an up-trend.")]
		public Brush ColorSup { get; set; }
			[Browsable(false)]
			public string ColorSupSerialize
			{
				get { return Serialize.BrushToString(ColorSup); }
				set { ColorSup = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore]
		[Display(Order = 11, GroupName = "Display", Name = "Down Trend Color",
			Description = "Color of the line, fill, and upper-fractal markers while in a down-trend.")]
		public Brush ColorRes { get; set; }
			[Browsable(false)]
			public string ColorResSerialize
			{
				get { return Serialize.BrushToString(ColorRes); }
				set { ColorRes = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		#endregion

		#region Public outputs (for strategy consumption)

		[Browsable(false)][XmlIgnore] public Series<int>    TrendSeries       { get { Update(); return sTrend;       } }
		[Browsable(false)][XmlIgnore] public Series<double> FractalLineSeries { get { Update(); return sFractalLine; } }

		#endregion

		#region Variables

		private List<double>	upperFractals;
		private List<double>	lowerFractals;
		private int				trendState;				// -1 / 0 / +1

		private Series<double>	sFractalLine;
		private Series<double>	sFractalLineUpper;
		private Series<double>	sFractalLineLower;
		private Series<int>		sTrend;

		private MAX				maxHighWin;
		private MIN				minLowWin;

		private SharpDX.Direct2D1.SolidColorBrush dxUpFill;
		private SharpDX.Direct2D1.SolidColorBrush dxDnFill;

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

				FractalLen		= 5;
				FCount			= 3;
				TypeStore		= FractalsTrend_BandsType.Avg;
				Shadow			= 80;
				DisplayMarkers	= true;

				ColorSup = Brushes.Aqua;
				ColorRes = Brushes.Orange;

				AddPlot(new Stroke(Brushes.Aqua, DashStyleHelper.Solid, 2f), PlotStyle.Line, "FractalLine");
			}
			else if (State == State.DataLoaded)
			{
				upperFractals = new List<double>();
				lowerFractals = new List<double>();
				// Zero-pad the rings — band stays below price until real fractals push the zeros out.
				for (int i = 0; i < FCount; i++)
				{
					upperFractals.Add(0.0);
					lowerFractals.Add(0.0);
				}

				trendState			= 0;
				// sFractalLine + sTrend are read across the visible window in OnRender — need Infinite.
				sFractalLine		= new Series<double>(this, MaximumBarsLookBack.Infinite);
				sTrend				= new Series<int>   (this, MaximumBarsLookBack.Infinite);
				// IsValidDataPoint(1) is called on these in OnBarUpdate — requires Infinite per QC rule L.
				sFractalLineUpper	= new Series<double>(this, MaximumBarsLookBack.Infinite);
				sFractalLineLower	= new Series<double>(this, MaximumBarsLookBack.Infinite);

				int win		= FractalLen * 2 + 1;
				maxHighWin	= MAX(High, win);
				minLowWin	= MIN(Low,  win);
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
			sTrend[0] = trendState;

			// Fractal detection requires FractalLen bars to the right of the pivot — i.e. the centered
			// MAX/MIN window of length 2*FractalLen+1 must be fully populated.
			if (CurrentBar < FractalLen * 2)
			{
				Values[0].Reset();
				sFractalLine.Reset();
				sFractalLineUpper.Reset();
				sFractalLineLower.Reset();
				return;
			}

			double maxH   = maxHighWin[0];
			double minL   = minLowWin[0];
			bool   upperF = High[FractalLen] >= maxH;
			bool   lowerF = Low [FractalLen] <= minL;

			// First matching case wins — push beats trim, so order matters.
			if (upperF)
				upperFractals.Add(High[FractalLen]);
			else if (lowerF)
				lowerFractals.Add(Low[FractalLen]);
			else if (upperFractals.Count > FCount)
				upperFractals.RemoveAt(0);
			else if (lowerFractals.Count > FCount)
				lowerFractals.RemoveAt(0);

			double fractalLineUpper = ReduceBand(upperFractals, true);
			double fractalLineLower = ReduceBand(lowerFractals, false);
			sFractalLineUpper[0]    = fractalLineUpper;
			sFractalLineLower[0]    = fractalLineLower;

			int prevTrend = trendState;

			if (sFractalLineUpper.IsValidDataPoint(1))
			{
				double prevUp = sFractalLineUpper[1];
				if (Close[1] <= prevUp && Close[0] > fractalLineUpper)
					trendState = +1;
			}
			if (sFractalLineLower.IsValidDataPoint(1))
			{
				double prevDn = sFractalLineLower[1];
				if (Close[1] >= prevDn && Close[0] < fractalLineLower)
					trendState = -1;
			}

			sTrend[0] = trendState;

			double fractalLine = (trendState == +1) ? fractalLineLower
								: (trendState == -1) ? fractalLineUpper
								: double.NaN;
			if (!double.IsNaN(fractalLine))
				sFractalLine[0] = fractalLine;
			else
				sFractalLine.Reset();

			// Plot — break the line on trend flips.
			bool flipped = (trendState != prevTrend);
			if (trendState != 0 && !flipped && !double.IsNaN(fractalLine))
			{
				Values[0][0]      = fractalLine;
				PlotBrushes[0][0] = (trendState == +1) ? ColorSup : ColorRes;
			}
			else
			{
				Values[0].Reset();
			}

			// Markers at the actual pivot bar (FractalLen bars ago).
			if (DisplayMarkers)
			{
				if (upperF)
					Draw.TriangleDown(this, "uF" + (CurrentBar - FractalLen), false, FractalLen,
						High[FractalLen], WithAlpha(ColorRes, 127));
				if (lowerF)
					Draw.TriangleUp(this, "lF" + (CurrentBar - FractalLen), false, FractalLen,
						Low[FractalLen], WithAlpha(ColorSup, 127));
			}
		}

		#endregion

		#region OnRenderTargetChanged

		public override void OnRenderTargetChanged()
		{
			try
			{
				ReleaseRenderResources();
				if (RenderTarget == null) return;

				float alpha = Math.Max(0f, Math.Min(1f, (100 - Shadow) / 100f));
				dxUpFill = MakeDXBrush(ColorSup, alpha);
				dxDnFill = MakeDXBrush(ColorRes, alpha);
			}
			catch
			{
				// Suppress during chart teardown.
			}
		}

		private void ReleaseRenderResources()
		{
			if (dxUpFill != null) { dxUpFill.Dispose(); dxUpFill = null; }
			if (dxDnFill != null) { dxDnFill.Dispose(); dxDnFill = null; }
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
			if (Shadow >= 100)			return;	// fill fully off
			if (sTrend == null || sFractalLine == null) return;

			int fromIdx = Math.Max(ChartBars.FromIndex, 0);
			int toIdx   = Math.Min(ChartBars.ToIndex,   CurrentBar);
			if (toIdx <= fromIdx) return;

			for (int j = fromIdx; j < toIdx; j++)
			{
				if (!sTrend.IsValidDataPointAt(j) || !sTrend.IsValidDataPointAt(j + 1)) continue;
				if (!sFractalLine.IsValidDataPointAt(j) || !sFractalLine.IsValidDataPointAt(j + 1)) continue;

				int trJ  = sTrend.GetValueAt(j);
				int trJ1 = sTrend.GetValueAt(j + 1);
				if (trJ == 0 || trJ1 == 0) continue;
				if (trJ != trJ1) continue; // Skip flip bars so fills don't bleed across the seam.

				double flJ   = sFractalLine.GetValueAt(j);
				double flJ1  = sFractalLine.GetValueAt(j + 1);
				double hl2J  = (Bars.GetHigh(j)     + Bars.GetLow(j))     / 2.0;
				double hl2J1 = (Bars.GetHigh(j + 1) + Bars.GetLow(j + 1)) / 2.0;

				if (trJ == +1)
				{
					// Up-trend: fractalLine = lower band; fill rises from line up to hl2.
					FillBarTrapezoid(chartControl, chartScale, j,
						topLeftPrice:  hl2J,  botLeftPrice:  flJ,
						topRightPrice: hl2J1, botRightPrice: flJ1,
						brush: dxUpFill);
				}
				else
				{
					// Down-trend: fractalLine = upper band; fill drops from line down to hl2.
					FillBarTrapezoid(chartControl, chartScale, j,
						topLeftPrice:  flJ,  botLeftPrice:  hl2J,
						topRightPrice: flJ1, botRightPrice: hl2J1,
						brush: dxDnFill);
				}
			}
		}

		private void FillBarTrapezoid(ChartControl chartControl, ChartScale chartScale, int barLeftIdx,
			double topLeftPrice, double botLeftPrice, double topRightPrice, double botRightPrice,
			SharpDX.Direct2D1.Brush brush)
		{
			if (brush == null) return;

			float xL  = chartControl.GetXByBarIndex(ChartBars, barLeftIdx);
			float xR  = chartControl.GetXByBarIndex(ChartBars, barLeftIdx + 1);
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

		#endregion

		#region Helpers

		private double ReduceBand(List<double> arr, bool upper)
		{
			if (arr.Count == 0) return double.NaN;
			switch (TypeStore)
			{
				case FractalsTrend_BandsType.MinMax:
					return upper ? arr.Max() : arr.Min();
				case FractalsTrend_BandsType.Median:
					return Median(arr);
				default:
					return arr.Average();
			}
		}

		private static double Median(List<double> list)
		{
			if (list.Count == 0) return double.NaN;
			var sorted = new List<double>(list);
			sorted.Sort();
			int n = sorted.Count;
			if ((n & 1) == 1) return sorted[n / 2];
			return (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
		}

		private SharpDX.Direct2D1.SolidColorBrush MakeDXBrush(Brush wpf, float alpha)
		{
			if (RenderTarget == null) return null;

			var scb = wpf as SolidColorBrush;
			System.Windows.Media.Color c = scb != null ? scb.Color : Colors.Gray;
			float wpfA = scb != null ? (float)scb.Opacity : 1f;

			return new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
				new SharpDX.Color4(c.R / 255f, c.G / 255f, c.B / 255f,
					Math.Max(0f, Math.Min(1f, alpha * wpfA))));
		}

		private static Brush WithAlpha(Brush src, byte alpha)
		{
			var scb = src as SolidColorBrush;
			if (scb == null) return src;
			var c = scb.Color;
			return EnsureFrozen(new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B)));
		}

		private static Brush EnsureFrozen(Brush b)
		{
			if (b != null && b.CanFreeze && !b.IsFrozen)
				b.Freeze();
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
		private indTradingView.FractalsTrend[] cacheFractalsTrend;
		public indTradingView.FractalsTrend FractalsTrend(int fractalLen, int fCount, FractalsTrend_BandsType typeStore, int shadow, bool displayMarkers)
		{
			return FractalsTrend(Input, fractalLen, fCount, typeStore, shadow, displayMarkers);
		}

		public indTradingView.FractalsTrend FractalsTrend(ISeries<double> input, int fractalLen, int fCount, FractalsTrend_BandsType typeStore, int shadow, bool displayMarkers)
		{
			if (cacheFractalsTrend != null)
				for (int idx = 0; idx < cacheFractalsTrend.Length; idx++)
					if (cacheFractalsTrend[idx] != null && cacheFractalsTrend[idx].FractalLen == fractalLen && cacheFractalsTrend[idx].FCount == fCount && cacheFractalsTrend[idx].TypeStore == typeStore && cacheFractalsTrend[idx].Shadow == shadow && cacheFractalsTrend[idx].DisplayMarkers == displayMarkers && cacheFractalsTrend[idx].EqualsInput(input))
						return cacheFractalsTrend[idx];
			return CacheIndicator<indTradingView.FractalsTrend>(new indTradingView.FractalsTrend(){ FractalLen = fractalLen, FCount = fCount, TypeStore = typeStore, Shadow = shadow, DisplayMarkers = displayMarkers }, input, ref cacheFractalsTrend);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.FractalsTrend FractalsTrend(int fractalLen, int fCount, FractalsTrend_BandsType typeStore, int shadow, bool displayMarkers)
		{
			return indicator.FractalsTrend(Input, fractalLen, fCount, typeStore, shadow, displayMarkers);
		}

		public Indicators.indTradingView.FractalsTrend FractalsTrend(ISeries<double> input , int fractalLen, int fCount, FractalsTrend_BandsType typeStore, int shadow, bool displayMarkers)
		{
			return indicator.FractalsTrend(input, fractalLen, fCount, typeStore, shadow, displayMarkers);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.FractalsTrend FractalsTrend(int fractalLen, int fCount, FractalsTrend_BandsType typeStore, int shadow, bool displayMarkers)
		{
			return indicator.FractalsTrend(Input, fractalLen, fCount, typeStore, shadow, displayMarkers);
		}

		public Indicators.indTradingView.FractalsTrend FractalsTrend(ISeries<double> input , int fractalLen, int fCount, FractalsTrend_BandsType typeStore, int shadow, bool displayMarkers)
		{
			return indicator.FractalsTrend(input, fractalLen, fCount, typeStore, shadow, displayMarkers);
		}
	}
}

#endregion
