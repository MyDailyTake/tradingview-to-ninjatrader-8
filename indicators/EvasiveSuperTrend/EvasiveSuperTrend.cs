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

// NT8 Version of Evasive SuperTrend
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under a Attribution-NonCommercial-ShareAlike 4.0 International.
// The original Pine Script™ code is by LuxAlgo and can be found at: https://www.tradingview.com/script/tfC7w3jE-Evasive-SuperTrend-LuxAlgo/
// Adaptation for NinjaTrader by jack@mydailytake.com
// Write-up + downloads: https://mydailytake.com/evasive-supertrend-luxalgo-ninjatrader-8/
// Source + all conversions: https://github.com/MyDailyTake/tradingview-to-ninjatrader-8
// © 2026 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the Attribution-NonCommercial-ShareAlike 4.0 International (CC BY-NC-SA 4.0) https://creativecommons.org/licenses/by-nc-sa/4.0/
// The use of LuxAlgo name or its adapted code in this work does not imply endorsement by the original authors.
//
// Notes:
//   Standard SuperTrend with adaptive band expansion. When price gets within Threshold * ATR of the
//   active band, the band is pushed AWAY by Alpha * ATR — the band "evades" choppy price action
//   instead of getting whipsawed. Out of the noisy zone, normal SuperTrend ratchet (max for up,
//   min for down) resumes.
//
//   Visuals: solid band line in clean state, dotted band line in noisy / expansion state, gradient
//   fill between the band and HL2 (bull color when in up-trend, bear when down). BUL / BEAR labels
//   at trend flips.
//
//   Non-repainting. Public Series outputs: BandSeries, TrendSeries.

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	#region Categories
	[Gui.CategoryOrder("Supertrend Settings",		10100)]
	[Gui.CategoryOrder("Noise Avoidance Logic",		10200)]
	[Gui.CategoryOrder("Visualization",				10300)]
	#endregion

	public class EvasiveSuperTrend : Indicator
	{
		#region indInfo

		private string indName        = "Evasive SuperTrend [LuxAlgo]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by LuxAlgo can be found here: https://www.tradingview.com/script/tfC7w3jE-Evasive-SuperTrend-LuxAlgo/";

		#endregion

		#region Properties

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 1, GroupName = "Supertrend Settings", Name = "ATR Length",
			Description = "Lookback for the ATR that drives band width.")]
		public int Length { get; set; }

		[NinjaScriptProperty]
		[Range(0.1, double.MaxValue)]
		[Display(Order = 2, GroupName = "Supertrend Settings", Name = "Base Multiplier",
			Description = "ATR multiplier defining the base band offset from HL2.")]
		public double Multiplier { get; set; }

		[NinjaScriptProperty]
		[Range(0.1, double.MaxValue)]
		[Display(Order = 1, GroupName = "Noise Avoidance Logic", Name = "Noise Threshold (xATR)",
			Description = "If price gets closer to the band than this fraction of ATR, the band is pushed away.")]
		public double Threshold { get; set; }

		[NinjaScriptProperty]
		[Range(0.0, double.MaxValue)]
		[Display(Order = 2, GroupName = "Noise Avoidance Logic", Name = "Expansion Alpha (xATR)",
			Description = "Distance (in ATRs) the band is pushed away when noise is detected.")]
		public double Alpha { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 1, GroupName = "Visualization", Name = "Show Signal Labels",
			Description = "Print BULL / BEAR text at trend flips.")]
		public bool ShowLabels { get; set; }

		[NinjaScriptProperty]
		[Range(6, 48)]
		[Display(Order = 2, GroupName = "Visualization", Name = "Label Font Size",
			Description = "Font size of the BULL / BEAR labels.")]
		public int LabelFontSize { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 3, GroupName = "Visualization", Name = "Show Gradient Fills",
			Description = "Render the per-bar trend-tinted gradient fill between the band and HL2.")]
		public bool ShowFills { get; set; }

		[XmlIgnore]
		[Display(Order = 10, GroupName = "Visualization", Name = "Bull Color",
			Description = "Band line + gradient color when in an up-trend.")]
		public Brush BullColor { get; set; }
			[Browsable(false)]
			public string BullColorSerialize
			{
				get { return Serialize.BrushToString(BullColor); }
				set { BullColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore]
		[Display(Order = 11, GroupName = "Visualization", Name = "Bear Color",
			Description = "Band line + gradient color when in a down-trend.")]
		public Brush BearColor { get; set; }
			[Browsable(false)]
			public string BearColorSerialize
			{
				get { return Serialize.BrushToString(BearColor); }
				set { BearColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		#endregion

		#region Public outputs

		[Browsable(false)][XmlIgnore] public Series<double> BandSeries  { get { Update(); return sStBand; } }
		[Browsable(false)][XmlIgnore] public Series<int>    TrendSeries { get { Update(); return sTrend; } }

		#endregion

		#region Variables

		private ATR		atrInd;

		// Persistent scalar state.
		private double	stBand;
		private int		trend;

		// OnRender reads these across the visible window — needs Infinite.
		// Also read via IsValidDataPointAt(CurrentBar - 1) in OnBarUpdate for prev-band fallback.
		private Series<double>	sStBand;
		private Series<int>		sTrend;

		// SharpDX gradient resources — stops keyed off color and rebuilt on color change.
		private SharpDX.Color4	lastBullC4;
		private SharpDX.Color4	lastBearC4;

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

				Length			= 10;
				Multiplier		= 3.0;
				Threshold		= 1.0;
				Alpha			= 0.5;
				ShowLabels		= true;
				LabelFontSize	= 14;
				ShowFills		= true;

				BullColor = new SolidColorBrush(Color.FromRgb(0x08, 0x99, 0x81));
				BearColor = new SolidColorBrush(Color.FromRgb(0xf2, 0x36, 0x45));
				EnsureFrozen(BullColor);
				EnsureFrozen(BearColor);

				// Plot strokes default DimGray — per-bar color flows from BullColor / BearColor via PlotBrushes.
				AddPlot(new Stroke(Brushes.DimGray, DashStyleHelper.Solid, 2f), PlotStyle.Line, "Solid Band");
				AddPlot(new Stroke(Brushes.DimGray, DashStyleHelper.Dot,   2f), PlotStyle.Line, "Dotted Band");
				AddPlot(new Stroke(Brushes.DimGray, DashStyleHelper.Solid, 4f), PlotStyle.Dot,  "Trend Switch Dot");
			}
			else if (State == State.DataLoaded)
			{
				atrInd = ATR(Length);

				// IsValidDataPointAt + OnRender visible-window reads → both sides of the rule require Infinite.
				sStBand = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sTrend  = new Series<int>(this, MaximumBarsLookBack.Infinite);

				stBand = 0.0;
				trend  = 1;
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
			if (CurrentBar < Length + 2)
			{
				Values[0].Reset();
				Values[1].Reset();
				Values[2].Reset();
				return;
			}

			double atrVal    = atrInd[0];
			double srcVal    = (High[0] + Low[0]) / 2.0;
			double upperBase = srcVal + Multiplier * atrVal;
			double lowerBase = srcVal - Multiplier * atrVal;

			double prevBand = sStBand.IsValidDataPointAt(CurrentBar - 1)
				? sStBand.GetValueAt(CurrentBar - 1)
				: (trend == 1 ? lowerBase : upperBase);

			bool isNoisy = Math.Abs(Close[0] - prevBand) < (atrVal * Threshold);

			int prevTrend = trend;

			if (trend == 1)
			{
				// BULL — push down on noise, ratchet up otherwise.
				if (isNoisy)
					stBand = prevBand - atrVal * Alpha;
				else
					stBand = Math.Max(lowerBase, prevBand);

				if (Close[0] < stBand)
				{
					trend  = -1;
					stBand = upperBase;
				}
			}
			else
			{
				// BEAR — push up on noise, ratchet down otherwise.
				if (isNoisy)
					stBand = prevBand + atrVal * Alpha;
				else
					stBand = Math.Min(upperBase, prevBand);

				if (Close[0] > stBand)
				{
					trend  = 1;
					stBand = lowerBase;
				}
			}

			sStBand[0] = stBand;
			sTrend[0]  = trend;

			bool trendChanged = trend != prevTrend;
			Brush trendBrush  = trend == 1 ? BullColor : BearColor;

			// Solid band line — present in clean (non-noisy) state.
			if (!isNoisy)
			{
				Values[0][0]      = stBand;
				PlotBrushes[0][0] = trendBrush;
				Values[1].Reset();
			}
			else
			{
				Values[0].Reset();
				Values[1][0]      = stBand;
				PlotBrushes[1][0] = trendBrush;
			}

			// Trend-switch dot — only on flip bars.
			if (trendChanged)
			{
				Values[2][0]      = stBand;
				PlotBrushes[2][0] = trendBrush;
			}
			else
			{
				Values[2].Reset();
			}

			// Trend-flip labels.
			if (ShowLabels && trendChanged)
			{
				SimpleFont sf = new SimpleFont("Arial", LabelFontSize);
				string txt    = trend == 1 ? "BULL" : "BEAR";
				Brush  bg     = trendBrush;
				int yOffset   = trend == 1 ? -LabelFontSize : LabelFontSize;
				Draw.Text(this, "estLbl" + CurrentBar, false, txt, 0, stBand, yOffset,
					Brushes.White, sf, TextAlignment.Center, (Brush)null, bg, 60);
			}
		}

		#endregion

		#region OnRenderTargetChanged + OnRender

		public override void OnRenderTargetChanged()
		{
			try
			{
				lastBullC4 = ToColor4(BullColor, 1.0f);
				lastBearC4 = ToColor4(BearColor, 1.0f);
			}
			catch
			{
				// Suppress during chart teardown.
			}
		}

		private void ReleaseRenderResources()
		{
			// No long-lived SharpDX resources — gradient brushes built per bar.
		}

		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			base.OnRender(chartControl, chartScale);

			if (RenderTarget == null)	return;
			if (ChartBars == null)		return;
			if (!IsVisible)				return;
			if (IsInHitTest)			return;
			if (!ShowFills)				return;
			if (sStBand == null || sTrend == null) return;

			int fromIdx = Math.Max(ChartBars.FromIndex, 0);
			int toIdx   = Math.Min(ChartBars.ToIndex,   CurrentBar);
			if (toIdx <= fromIdx) return;

			// Build ONE bull + ONE bear gradient brush per render — span the chart panel height so every
			// bar samples from the same underlying gradient. This eliminates the per-bar seams that show up
			// when each trapezoid has its own anchored gradient.
			float panelTop    = ChartPanel.Y;
			float panelBottom = ChartPanel.Y + ChartPanel.H;

			SharpDX.Color4 bullSolid = new SharpDX.Color4(lastBullC4.Red, lastBullC4.Green, lastBullC4.Blue, 0.50f);
			SharpDX.Color4 bullClear = new SharpDX.Color4(lastBullC4.Red, lastBullC4.Green, lastBullC4.Blue, 0.0f);
			SharpDX.Color4 bearSolid = new SharpDX.Color4(lastBearC4.Red, lastBearC4.Green, lastBearC4.Blue, 0.50f);
			SharpDX.Color4 bearClear = new SharpDX.Color4(lastBearC4.Red, lastBearC4.Green, lastBearC4.Blue, 0.0f);

			SharpDX.Direct2D1.GradientStopCollection bullStops = null;
			SharpDX.Direct2D1.GradientStopCollection bearStops = null;
			SharpDX.Direct2D1.LinearGradientBrush bullBrush = null;
			SharpDX.Direct2D1.LinearGradientBrush bearBrush = null;
			try
			{
				// Bull: solid at top of panel → transparent at bottom (so up-trend fills are denser at higher prices).
				bullStops = new SharpDX.Direct2D1.GradientStopCollection(RenderTarget, new[]
				{
					new SharpDX.Direct2D1.GradientStop { Position = 0f, Color = bullSolid },
					new SharpDX.Direct2D1.GradientStop { Position = 1f, Color = bullClear }
				});
				bullBrush = new SharpDX.Direct2D1.LinearGradientBrush(RenderTarget,
					new SharpDX.Direct2D1.LinearGradientBrushProperties
					{
						StartPoint = new SharpDX.Vector2(0f, panelTop),
						EndPoint   = new SharpDX.Vector2(0f, panelBottom)
					}, bullStops);

				// Bear: transparent at top → solid at bottom (down-trend fills denser at lower prices).
				bearStops = new SharpDX.Direct2D1.GradientStopCollection(RenderTarget, new[]
				{
					new SharpDX.Direct2D1.GradientStop { Position = 0f, Color = bearClear },
					new SharpDX.Direct2D1.GradientStop { Position = 1f, Color = bearSolid }
				});
				bearBrush = new SharpDX.Direct2D1.LinearGradientBrush(RenderTarget,
					new SharpDX.Direct2D1.LinearGradientBrushProperties
					{
						StartPoint = new SharpDX.Vector2(0f, panelTop),
						EndPoint   = new SharpDX.Vector2(0f, panelBottom)
					}, bearStops);

				for (int j = fromIdx; j < toIdx; j++)
				{
					if (!sStBand.IsValidDataPointAt(j) || !sStBand.IsValidDataPointAt(j + 1)) continue;
					if (!sTrend.IsValidDataPointAt(j)  || !sTrend.IsValidDataPointAt(j + 1))  continue;

					int trJ  = sTrend.GetValueAt(j);
					int trJ1 = sTrend.GetValueAt(j + 1);
					if (trJ != trJ1) continue;	// flip bars — skip to avoid color bleed

					double bandJ  = sStBand.GetValueAt(j);
					double bandJ1 = sStBand.GetValueAt(j + 1);
					double srcJ   = (Bars.GetHigh(j)     + Bars.GetLow(j))     / 2.0;
					double srcJ1  = (Bars.GetHigh(j + 1) + Bars.GetLow(j + 1)) / 2.0;

					double topJ  = Math.Max(bandJ,  srcJ);
					double topJ1 = Math.Max(bandJ1, srcJ1);
					double botJ  = Math.Min(bandJ,  srcJ);
					double botJ1 = Math.Min(bandJ1, srcJ1);

					var brush = trJ == 1 ? (SharpDX.Direct2D1.Brush)bullBrush : bearBrush;
					DrawTrapezoid(chartControl, chartScale, j, topJ, topJ1, botJ, botJ1, brush);
				}
			}
			finally
			{
				if (bullBrush != null) bullBrush.Dispose();
				if (bearBrush != null) bearBrush.Dispose();
				if (bullStops != null) bullStops.Dispose();
				if (bearStops != null) bearStops.Dispose();
			}
		}

		private void DrawTrapezoid(ChartControl chartControl, ChartScale chartScale, int barLeftIdx,
			double topPriceJ, double topPriceJ1, double botPriceJ, double botPriceJ1,
			SharpDX.Direct2D1.Brush brush)
		{
			if (brush == null) return;

			float xL = chartControl.GetXByBarIndex(ChartBars, barLeftIdx);
			float xR = chartControl.GetXByBarIndex(ChartBars, barLeftIdx + 1);

			float yTopL = (float)chartScale.GetYByValue(topPriceJ);
			float yTopR = (float)chartScale.GetYByValue(topPriceJ1);
			float yBotL = (float)chartScale.GetYByValue(botPriceJ);
			float yBotR = (float)chartScale.GetYByValue(botPriceJ1);

			SharpDX.Direct2D1.PathGeometry geom = null;
			SharpDX.Direct2D1.GeometrySink sink = null;
			try
			{
				geom = new SharpDX.Direct2D1.PathGeometry(Core.Globals.D2DFactory);
				sink = geom.Open();
				sink.SetFillMode(SharpDX.Direct2D1.FillMode.Winding);
				sink.BeginFigure(new SharpDX.Vector2(xL, yTopL), SharpDX.Direct2D1.FigureBegin.Filled);
				sink.AddLine(new SharpDX.Vector2(xR, yTopR));
				sink.AddLine(new SharpDX.Vector2(xR, yBotR));
				sink.AddLine(new SharpDX.Vector2(xL, yBotL));
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

		#region Color helpers

		private static SharpDX.Color4 ToColor4(Brush wpf, float alphaScale)
		{
			var scb = wpf as SolidColorBrush;
			System.Windows.Media.Color c = scb != null ? scb.Color : Colors.Gray;
			float wpfA = scb != null ? (float)(scb.Opacity * (c.A / 255f)) : 1f;
			return new SharpDX.Color4(c.R / 255f, c.G / 255f, c.B / 255f,
				Math.Max(0f, Math.Min(1f, alphaScale * wpfA)));
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
		private indTradingView.EvasiveSuperTrend[] cacheEvasiveSuperTrend;
		public indTradingView.EvasiveSuperTrend EvasiveSuperTrend(int length, double multiplier, double threshold, double alpha, bool showLabels, int labelFontSize, bool showFills)
		{
			return EvasiveSuperTrend(Input, length, multiplier, threshold, alpha, showLabels, labelFontSize, showFills);
		}

		public indTradingView.EvasiveSuperTrend EvasiveSuperTrend(ISeries<double> input, int length, double multiplier, double threshold, double alpha, bool showLabels, int labelFontSize, bool showFills)
		{
			if (cacheEvasiveSuperTrend != null)
				for (int idx = 0; idx < cacheEvasiveSuperTrend.Length; idx++)
					if (cacheEvasiveSuperTrend[idx] != null && cacheEvasiveSuperTrend[idx].Length == length && cacheEvasiveSuperTrend[idx].Multiplier == multiplier && cacheEvasiveSuperTrend[idx].Threshold == threshold && cacheEvasiveSuperTrend[idx].Alpha == alpha && cacheEvasiveSuperTrend[idx].ShowLabels == showLabels && cacheEvasiveSuperTrend[idx].LabelFontSize == labelFontSize && cacheEvasiveSuperTrend[idx].ShowFills == showFills && cacheEvasiveSuperTrend[idx].EqualsInput(input))
						return cacheEvasiveSuperTrend[idx];
			return CacheIndicator<indTradingView.EvasiveSuperTrend>(new indTradingView.EvasiveSuperTrend(){ Length = length, Multiplier = multiplier, Threshold = threshold, Alpha = alpha, ShowLabels = showLabels, LabelFontSize = labelFontSize, ShowFills = showFills }, input, ref cacheEvasiveSuperTrend);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.EvasiveSuperTrend EvasiveSuperTrend(int length, double multiplier, double threshold, double alpha, bool showLabels, int labelFontSize, bool showFills)
		{
			return indicator.EvasiveSuperTrend(Input, length, multiplier, threshold, alpha, showLabels, labelFontSize, showFills);
		}

		public Indicators.indTradingView.EvasiveSuperTrend EvasiveSuperTrend(ISeries<double> input , int length, double multiplier, double threshold, double alpha, bool showLabels, int labelFontSize, bool showFills)
		{
			return indicator.EvasiveSuperTrend(input, length, multiplier, threshold, alpha, showLabels, labelFontSize, showFills);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.EvasiveSuperTrend EvasiveSuperTrend(int length, double multiplier, double threshold, double alpha, bool showLabels, int labelFontSize, bool showFills)
		{
			return indicator.EvasiveSuperTrend(Input, length, multiplier, threshold, alpha, showLabels, labelFontSize, showFills);
		}

		public Indicators.indTradingView.EvasiveSuperTrend EvasiveSuperTrend(ISeries<double> input , int length, double multiplier, double threshold, double alpha, bool showLabels, int labelFontSize, bool showFills)
		{
			return indicator.EvasiveSuperTrend(input, length, multiplier, threshold, alpha, showLabels, labelFontSize, showFills);
		}
	}
}

#endregion
