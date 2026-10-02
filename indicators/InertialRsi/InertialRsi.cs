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

// NT8 Version of Inertial RSI
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the Mozilla Public License 2.0 (MPL 2.0).
// The original Pine Script™ code is by LuxAlgo and can be found at: https://www.tradingview.com/script/eg4t9sX8-Inertial-RSI-LuxAlgo/
// Adaptation for NinjaTrader by jack@mydailytake.com
// © 2026 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the Mozilla Public License 2.0. Full license details at https://mozilla.org/MPL/2.0/
// The use of LuxAlgo name or its adapted code in this work does not imply endorsement by the original authors.
//
// Notes:
//   For each closed bar, scans every RMA-based RSI from MinLen..MaxLen and picks the one whose value
//   sits closest to the previous bar's chosen value — the inertia-weighted RSI resists noise.
//   K = SMA of inertial RSI; Signal = SMA of K. Cross-over above OsLevel = bullish triangle on price
//   panel; cross-under below ObLevel = bearish triangle.
//   Vertical gradient fills (bull above 50, bear below) via SharpDX LinearGradientBrush.
//
//   Non-repainting. Public Series outputs: RsiLine, SignalLine, InertialRsi, BullSignal, BearSignal.

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	#region Categories
	[Gui.CategoryOrder("Settings",	10100)]
	[Gui.CategoryOrder("Visuals",	10200)]
	#endregion

	public class InertialRsi : Indicator
	{
		#region indInfo

		private string indName        = "Inertial RSI [LuxAlgo]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by LuxAlgo can be found here: https://www.tradingview.com/script/eg4t9sX8-Inertial-RSI-LuxAlgo/";

		#endregion

		#region Properties

		[NinjaScriptProperty]
		[Range(2, 256)]
		[Display(Order = 1, GroupName = "Settings", Name = "Minimum Length",
			Description = "Smallest RSI length the inertia scan considers.")]
		public int MinLen { get; set; }

		[NinjaScriptProperty]
		[Range(2, 256)]
		[Display(Order = 2, GroupName = "Settings", Name = "Maximum Length",
			Description = "Largest RSI length the inertia scan considers (capped at 256 — see standard Series lookback).")]
		public int MaxLen { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 3, GroupName = "Settings", Name = "RSI Smoothing",
			Description = "SMA length applied to the inertia RSI to produce the RSI line.")]
		public int Smooth { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 4, GroupName = "Settings", Name = "Signal Line Length",
			Description = "SMA length applied to the RSI line to produce the signal line.")]
		public int SignalLen { get; set; }

		[NinjaScriptProperty]
		[Range(50.0, 100.0)]
		[Display(Order = 5, GroupName = "Settings", Name = "Overbought Level",
			Description = "Cross-under fires a bearish signal triangle.")]
		public double ObLevel { get; set; }

		[NinjaScriptProperty]
		[Range(0.0, 50.0)]
		[Display(Order = 6, GroupName = "Settings", Name = "Oversold Level",
			Description = "Cross-over fires a bullish signal triangle.")]
		public double OsLevel { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 1, GroupName = "Visuals", Name = "Show Gradient Fill",
			Description = "Render the bullish / bearish vertical gradient fills.")]
		public bool ShowFill { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 2, GroupName = "Visuals", Name = "Show Signal Triangles",
			Description = "Bull / bear triangles on the price panel at OS / OB cross events.")]
		public bool ShowSignals { get; set; }

		[XmlIgnore]
		[Display(Order = 10, GroupName = "Visuals", Name = "Bullish Color",
			Description = "RSI line and gradient color above 50 / bullish triangles.")]
		public Brush BullColor { get; set; }
			[Browsable(false)]
			public string BullColorSerialize
			{
				get { return Serialize.BrushToString(BullColor); }
				set { BullColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore]
		[Display(Order = 11, GroupName = "Visuals", Name = "Bearish Color",
			Description = "RSI line and gradient color below 50 / bearish triangles.")]
		public Brush BearColor { get; set; }
			[Browsable(false)]
			public string BearColorSerialize
			{
				get { return Serialize.BrushToString(BearColor); }
				set { BearColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		#endregion

		#region Public outputs

		[Browsable(false)][XmlIgnore] public Series<double> RsiLine     { get { return Values[0]; } }
		[Browsable(false)][XmlIgnore] public Series<double> SignalLine  { get { return Values[1]; } }
		[Browsable(false)][XmlIgnore] public Series<double> InertialRsiSeries { get { Update(); return sInertialRsi; } }
		[Browsable(false)][XmlIgnore] public Series<bool>   BullSignal  { get { Update(); return sBullSignal; } }
		[Browsable(false)][XmlIgnore] public Series<bool>   BearSignal  { get { Update(); return sBearSignal; } }

		#endregion

		#region Variables

		// Persistent across bars; seeded to 50.0 in DataLoaded.
		private double			inertialRsi;

		// Per-length RMA state — sized MaxLen+1 so indices 0..MaxLen are addressable. Zero-initialized.
		private double[]		aRmaGains;
		private double[]		aRmaLosses;

		private Series<double>	sGain;
		private Series<double>	sLoss;
		private Series<double>	sInertialRsi;
		private Series<double>	sRsiLine;	// Infinite — OnRender visible-window read.
		private Series<bool>	sBullSignal;
		private Series<bool>	sBearSignal;

		private SMA				smaRsi;
		private SMA				smaSignal;

		// SharpDX gradient resources — stops cached, brush rebuilt per render.
		private SharpDX.Direct2D1.GradientStopCollection	bullStops;
		private SharpDX.Direct2D1.GradientStopCollection	bearStops;
		private SharpDX.Color4								lastBullC4;
		private SharpDX.Color4								lastBearC4;

		// Reference brush for the OB/OS hlines.
		private Brush refBrush;

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
				DrawOnPricePanel			= true;	// Signal triangles render on the price panel.
				DrawHorizontalGridLines		= true;
				DrawVerticalGridLines		= true;
				PaintPriceMarkers			= true;
				ScaleJustification			= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				IsSuspendedWhileInactive	= true;

				MinLen		= 10;
				MaxLen		= 40;
				Smooth		= 3;
				SignalLen	= 14;
				ObLevel		= 60.0;
				OsLevel		= 40.0;
				ShowFill	= true;
				ShowSignals	= true;

				BullColor = new SolidColorBrush(Color.FromRgb(0x08, 0x99, 0x81));
				BearColor = new SolidColorBrush(Color.FromRgb(0xf2, 0x36, 0x45));
				EnsureFrozen(BullColor);
				EnsureFrozen(BearColor);

				// RSI plot stroke defaults DimGray — per-bar color is driven by BullColor / BearColor via PlotBrushes.
				AddPlot(new Stroke(Brushes.DimGray, DashStyleHelper.Solid, 2f), PlotStyle.Line, "RSI");
				// Signal line is solid — the standard NT plot dialog handles user overrides.
				AddPlot(new Stroke(new SolidColorBrush(Color.FromArgb(160, 192, 192, 192)), DashStyleHelper.Dot, 2f), PlotStyle.Line, "Signal");
			}
			else if (State == State.DataLoaded)
			{
				inertialRsi = 50.0;

				aRmaGains  = new double[MaxLen + 1];
				aRmaLosses = new double[MaxLen + 1];

				sGain        = new Series<double>(this);
				sLoss        = new Series<double>(this);
				// SMA wraps with Smooth (user-configurable, can exceed 256) — needs Infinite.
				sInertialRsi = new Series<double>(this, MaximumBarsLookBack.Infinite);
				// OnRender consumes the RSI line across the visible window — needs Infinite.
				sRsiLine     = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sBullSignal  = new Series<bool>(this);
				sBearSignal  = new Series<bool>(this);

				smaRsi    = SMA(sInertialRsi, Smooth);
				smaSignal = SMA(smaRsi,       SignalLen);

				refBrush = new SolidColorBrush(Color.FromArgb(128, 192, 192, 192));
				EnsureFrozen(refBrush);
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
			sBullSignal[0] = false;
			sBearSignal[0] = false;

			// Bar-over-bar close change split into gain / loss components.
			double change = CurrentBar > 0 ? Close[0] - Close[1] : 0.0;
			double gain   = Math.Max(change, 0.0);
			double loss   = Math.Max(-change, 0.0);
			sGain[0] = gain;
			sLoss[0] = loss;

			// Reference hlines — placed on bar 0 with the user's OB / OS levels (re-runs on indicator reload).
			if (CurrentBar == 0)
			{
				Draw.HorizontalLine(this, "irOb", false, ObLevel, refBrush, DashStyleHelper.Dash, 1);
				Draw.HorizontalLine(this, "irOs", false, OsLevel, refBrush, DashStyleHelper.Dash, 1);
			}

			// Inertia scan — for each candidate length, advance its persistent RMA, then test the resulting RSI.
			double bestRsi = 50.0;
			double minDiff = 1e10;
			bool anyEligible = false;

			for (int len = MinLen; len <= MaxLen; len++)
			{
				double prevGain = aRmaGains[len];
				double prevLoss = aRmaLosses[len];

				double currGain, currLoss;
				if (CurrentBar < len - 1)
				{
					currGain = 0.0;
					currLoss = 0.0;
				}
				else if (CurrentBar == len - 1)
				{
					double sumG = 0.0, sumL = 0.0;
					for (int j = 0; j < len; j++)
					{
						sumG += sGain[j];
						sumL += sLoss[j];
					}
					currGain = sumG / len;
					currLoss = sumL / len;
				}
				else
				{
					currGain = prevGain + (gain - prevGain) / len;
					currLoss = prevLoss + (loss - prevLoss) / len;
				}

				aRmaGains[len]  = currGain;
				aRmaLosses[len] = currLoss;

				if (CurrentBar >= len - 1)
				{
					double rsi  = currLoss == 0.0 ? 100.0 : 100.0 - (100.0 / (1.0 + currGain / currLoss));
					double diff = Math.Abs(rsi - inertialRsi);
					if (diff < minDiff)
					{
						minDiff = diff;
						bestRsi = rsi;
						anyEligible = true;
					}
				}
			}

			if (CurrentBar >= MinLen - 1 && anyEligible)
				inertialRsi = bestRsi;

			sInertialRsi[0] = inertialRsi;

			// Smoothing chain ready only after both SMAs warm up.
			int needed = MaxLen + Smooth + SignalLen;
			if (CurrentBar < needed) return;

			double rsiLineVal    = smaRsi[0];
			double signalLineVal = smaSignal[0];

			Values[0][0]      = rsiLineVal;
			Values[1][0]      = signalLineVal;
			sRsiLine[0]       = rsiLineVal;
			PlotBrushes[0][0] = (rsiLineVal > 50.0) ? BullColor : BearColor;

			// Cross-over / cross-under detection on the prior vs current RSI line.
			double prevRsi = sRsiLine.IsValidDataPointAt(CurrentBar - 1) ? sRsiLine.GetValueAt(CurrentBar - 1) : rsiLineVal;
			bool bull = prevRsi <= OsLevel && rsiLineVal > OsLevel;
			bool bear = prevRsi >= ObLevel && rsiLineVal < ObLevel;
			sBullSignal[0] = bull;
			sBearSignal[0] = bear;

			if (ShowSignals)
			{
				if (bull)
					Draw.TriangleUp(this, "irBull" + CurrentBar, true, 0, Low[0]  - TickSize * 4, BullColor);
				if (bear)
					Draw.TriangleDown(this, "irBear" + CurrentBar, true, 0, High[0] + TickSize * 4, BearColor);
			}
		}

		#endregion

		#region OnRenderTargetChanged + helpers

		public override void OnRenderTargetChanged()
		{
			try
			{
				ReleaseRenderResources();
				if (RenderTarget == null) return;

				BuildGradientStops();
			}
			catch
			{
				// Suppress during chart teardown.
			}
		}

		private void BuildGradientStops()
		{
			DisposeGradientStops();
			if (RenderTarget == null) return;

			SharpDX.Color4 bullSolid = ToColor4(BullColor, 1.0f);
			SharpDX.Color4 bullClear = new SharpDX.Color4(bullSolid.Red, bullSolid.Green, bullSolid.Blue, 0f);
			SharpDX.Color4 bearSolid = ToColor4(BearColor, 1.0f);
			SharpDX.Color4 bearClear = new SharpDX.Color4(bearSolid.Red, bearSolid.Green, bearSolid.Blue, 0f);

			bullStops = new SharpDX.Direct2D1.GradientStopCollection(RenderTarget, new[]
			{
				new SharpDX.Direct2D1.GradientStop { Position = 0f, Color = bullSolid },
				new SharpDX.Direct2D1.GradientStop { Position = 1f, Color = bullClear }
			});
			bearStops = new SharpDX.Direct2D1.GradientStopCollection(RenderTarget, new[]
			{
				new SharpDX.Direct2D1.GradientStop { Position = 0f, Color = bearClear },
				new SharpDX.Direct2D1.GradientStop { Position = 1f, Color = bearSolid }
			});

			lastBullC4 = bullSolid;
			lastBearC4 = bearSolid;
		}

		private void EnsureGradientStops()
		{
			if (bullStops == null || bearStops == null)
			{
				BuildGradientStops();
				return;
			}

			SharpDX.Color4 currentBull = ToColor4(BullColor, 1.0f);
			SharpDX.Color4 currentBear = ToColor4(BearColor, 1.0f);
			if (!ColorsEqual(currentBull, lastBullC4) || !ColorsEqual(currentBear, lastBearC4))
				BuildGradientStops();
		}

		private void DisposeGradientStops()
		{
			if (bullStops != null) { bullStops.Dispose(); bullStops = null; }
			if (bearStops != null) { bearStops.Dispose(); bearStops = null; }
		}

		private void ReleaseRenderResources()
		{
			DisposeGradientStops();
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
			if (!ShowFill)				return;
			if (sRsiLine == null)		return;

			EnsureGradientStops();
			if (bullStops == null || bearStops == null) return;

			int fromIdx = Math.Max(ChartBars.FromIndex, 0);
			int toIdx   = Math.Min(ChartBars.ToIndex,   CurrentBar);
			if (toIdx <= fromIdx) return;

			float yAt100 = (float)chartScale.GetYByValue(100.0);
			float yAt50  = (float)chartScale.GetYByValue(50.0);
			float yAt0   = (float)chartScale.GetYByValue(0.0);

			SharpDX.Direct2D1.LinearGradientBrush bullBrush = null;
			SharpDX.Direct2D1.LinearGradientBrush bearBrush = null;
			try
			{
				bullBrush = new SharpDX.Direct2D1.LinearGradientBrush(RenderTarget,
					new SharpDX.Direct2D1.LinearGradientBrushProperties
					{
						StartPoint = new SharpDX.Vector2(0f, yAt100),
						EndPoint   = new SharpDX.Vector2(0f, yAt50)
					},
					bullStops);

				bearBrush = new SharpDX.Direct2D1.LinearGradientBrush(RenderTarget,
					new SharpDX.Direct2D1.LinearGradientBrushProperties
					{
						StartPoint = new SharpDX.Vector2(0f, yAt50),
						EndPoint   = new SharpDX.Vector2(0f, yAt0)
					},
					bearStops);

				for (int j = fromIdx; j < toIdx; j++)
				{
					if (!sRsiLine.IsValidDataPointAt(j) || !sRsiLine.IsValidDataPointAt(j + 1)) continue;

					double rJ  = sRsiLine.GetValueAt(j);
					double rJ1 = sRsiLine.GetValueAt(j + 1);

					if (rJ > 50.0 || rJ1 > 50.0)
					{
						float topL = (float)chartScale.GetYByValue(Math.Max(rJ,  50.0));
						float topR = (float)chartScale.GetYByValue(Math.Max(rJ1, 50.0));
						FillBarTrapezoid(chartControl, j,
							topLeftY: topL, topRightY: topR,
							botLeftY: yAt50, botRightY: yAt50,
							brush: bullBrush);
					}

					if (rJ < 50.0 || rJ1 < 50.0)
					{
						float botL = (float)chartScale.GetYByValue(Math.Min(rJ,  50.0));
						float botR = (float)chartScale.GetYByValue(Math.Min(rJ1, 50.0));
						FillBarTrapezoid(chartControl, j,
							topLeftY: yAt50, topRightY: yAt50,
							botLeftY: botL, botRightY: botR,
							brush: bearBrush);
					}
				}
			}
			finally
			{
				if (bullBrush != null) bullBrush.Dispose();
				if (bearBrush != null) bearBrush.Dispose();
			}
		}

		private void FillBarTrapezoid(ChartControl chartControl, int barLeftIdx,
			float topLeftY, float topRightY, float botLeftY, float botRightY,
			SharpDX.Direct2D1.Brush brush)
		{
			if (brush == null) return;

			float xL = chartControl.GetXByBarIndex(ChartBars, barLeftIdx);
			float xR = chartControl.GetXByBarIndex(ChartBars, barLeftIdx + 1);

			SharpDX.Direct2D1.PathGeometry geom = null;
			SharpDX.Direct2D1.GeometrySink sink = null;
			try
			{
				geom = new SharpDX.Direct2D1.PathGeometry(Core.Globals.D2DFactory);
				sink = geom.Open();
				sink.SetFillMode(SharpDX.Direct2D1.FillMode.Winding);
				sink.BeginFigure(new SharpDX.Vector2(xL, topLeftY), SharpDX.Direct2D1.FigureBegin.Filled);
				sink.AddLine(new SharpDX.Vector2(xR, topRightY));
				sink.AddLine(new SharpDX.Vector2(xR, botRightY));
				sink.AddLine(new SharpDX.Vector2(xL, botLeftY));
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

		private static bool ColorsEqual(SharpDX.Color4 a, SharpDX.Color4 b)
		{
			const float eps = 1e-4f;
			return Math.Abs(a.Red   - b.Red)   < eps
				&& Math.Abs(a.Green - b.Green) < eps
				&& Math.Abs(a.Blue  - b.Blue)  < eps
				&& Math.Abs(a.Alpha - b.Alpha) < eps;
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
		private indTradingView.InertialRsi[] cacheInertialRsi;
		public indTradingView.InertialRsi InertialRsi(int minLen, int maxLen, int smooth, int signalLen, double obLevel, double osLevel, bool showFill, bool showSignals)
		{
			return InertialRsi(Input, minLen, maxLen, smooth, signalLen, obLevel, osLevel, showFill, showSignals);
		}

		public indTradingView.InertialRsi InertialRsi(ISeries<double> input, int minLen, int maxLen, int smooth, int signalLen, double obLevel, double osLevel, bool showFill, bool showSignals)
		{
			if (cacheInertialRsi != null)
				for (int idx = 0; idx < cacheInertialRsi.Length; idx++)
					if (cacheInertialRsi[idx] != null && cacheInertialRsi[idx].MinLen == minLen && cacheInertialRsi[idx].MaxLen == maxLen && cacheInertialRsi[idx].Smooth == smooth && cacheInertialRsi[idx].SignalLen == signalLen && cacheInertialRsi[idx].ObLevel == obLevel && cacheInertialRsi[idx].OsLevel == osLevel && cacheInertialRsi[idx].ShowFill == showFill && cacheInertialRsi[idx].ShowSignals == showSignals && cacheInertialRsi[idx].EqualsInput(input))
						return cacheInertialRsi[idx];
			return CacheIndicator<indTradingView.InertialRsi>(new indTradingView.InertialRsi(){ MinLen = minLen, MaxLen = maxLen, Smooth = smooth, SignalLen = signalLen, ObLevel = obLevel, OsLevel = osLevel, ShowFill = showFill, ShowSignals = showSignals }, input, ref cacheInertialRsi);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.InertialRsi InertialRsi(int minLen, int maxLen, int smooth, int signalLen, double obLevel, double osLevel, bool showFill, bool showSignals)
		{
			return indicator.InertialRsi(Input, minLen, maxLen, smooth, signalLen, obLevel, osLevel, showFill, showSignals);
		}

		public Indicators.indTradingView.InertialRsi InertialRsi(ISeries<double> input , int minLen, int maxLen, int smooth, int signalLen, double obLevel, double osLevel, bool showFill, bool showSignals)
		{
			return indicator.InertialRsi(input, minLen, maxLen, smooth, signalLen, obLevel, osLevel, showFill, showSignals);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.InertialRsi InertialRsi(int minLen, int maxLen, int smooth, int signalLen, double obLevel, double osLevel, bool showFill, bool showSignals)
		{
			return indicator.InertialRsi(Input, minLen, maxLen, smooth, signalLen, obLevel, osLevel, showFill, showSignals);
		}

		public Indicators.indTradingView.InertialRsi InertialRsi(ISeries<double> input , int minLen, int maxLen, int smooth, int signalLen, double obLevel, double osLevel, bool showFill, bool showSignals)
		{
			return indicator.InertialRsi(input, minLen, maxLen, smooth, signalLen, obLevel, osLevel, showFill, showSignals);
		}
	}
}

#endregion
