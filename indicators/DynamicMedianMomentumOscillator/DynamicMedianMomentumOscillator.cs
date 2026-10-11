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

// NT8 Version of Dynamic Median Momentum Oscillator
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the Mozilla Public License 2.0 (MPL 2.0).
// The original Pine Script™ code is by AlgoAlpha and can be found at: https://www.tradingview.com/script/EDgeowAw-Dynamic-Median-Momentum-Oscillator-AlgoAlpha/
// Adaptation for NinjaTrader by jack@mydailytake.com
// © 2026 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the Mozilla Public License 2.0. Full license details at https://mozilla.org/MPL/2.0/
// The use of AlgoAlpha name or its adapted code in this work does not imply endorsement by the original authors.
//
// Notes:
//   Median-based momentum oscillator. The basis is the rolling median of HLC3 over BasisLength bars,
//   making the center less sensitive to outliers than a moving average. The raw oscillator is
//   (close - basis), optionally standardized by the average candle range (EMA of High-Low) so that the
//   ±200 / ±250 / ±300 OB/OS bands hold across asset prices. Smooth MCD = EMA of the (standardized)
//   oscillator; Signal = EMA of Smooth MCD; histogram = Smooth MCD − Signal.
//
//   Display mode controls which components are visible:
//     • All        — slow lines (Smooth MCD + Signal) + histogram
//     • Fast       — histogram only
//     • Slow       — slow lines only
//     • Super Slow — Smooth MCD as a colored line filled to zero, with bull/bear strength shading
//
//   Documented scope decisions:
//     • 6 alertconditions stripped per QC convention.
//     • Pine `color.new(c, NN)` translucency reproduced via SharpDX SolidColorBrush alpha.
//
//   Non-repainting: all calcs are EMA / median over closed-bar history.

#region Enums DynamicMedianMomentumOscillator

public enum DynamicMedianMomentumOscillator_Mode
{
	All,
	Fast,
	Slow,
	SuperSlow
}

#endregion

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	#region Categories
	[Gui.CategoryOrder("Parameters",	10100)]
	[Gui.CategoryOrder("Display",		10200)]
	[Gui.CategoryOrder("Appearance",	10300)]
	#endregion

	public class DynamicMedianMomentumOscillator : Indicator
	{
		#region indInfo

		private string indName        = "Dynamic Median Momentum Oscillator [AlgoAlpha]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by AlgoAlpha can be found here: https://www.tradingview.com/script/EDgeowAw-Dynamic-Median-Momentum-Oscillator-AlgoAlpha/";

		#endregion

		#region Properties

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 1, GroupName = "Parameters", Name = "Basis Length",
			Description = "Lookback used to calculate the rolling median basis (HLC3).")]
		public int BasisLength { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 2, GroupName = "Parameters", Name = "Smoothing Length",
			Description = "EMA length applied to the raw oscillator to produce the Smooth MCD line.")]
		public int SmoothLength { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 3, GroupName = "Parameters", Name = "Signal Length",
			Description = "EMA length used for the Signal line.")]
		public int SignalLength { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 4, GroupName = "Parameters", Name = "Standardize",
			Description = "Normalize the oscillator by average candle range so values can use fixed OB/OS zones.")]
		public bool Standardize { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 1, GroupName = "Display", Name = "Display Mode",
			Description = "Chooses which oscillator components are visible.")]
		public DynamicMedianMomentumOscillator_Mode Mode { get; set; }

		[XmlIgnore]
		[Display(Order = 1, GroupName = "Appearance", Name = "Bullish Color",
			Description = "Color used for bullish lines, fills, and positive histogram bars.")]
		public Brush BullColor { get; set; }
			[Browsable(false)]
			public string BullColorSerialize
			{
				get { return Serialize.BrushToString(BullColor); }
				set { BullColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore]
		[Display(Order = 2, GroupName = "Appearance", Name = "Bearish Color",
			Description = "Color used for bearish lines, fills, and negative histogram bars.")]
		public Brush BearColor { get; set; }
			[Browsable(false)]
			public string BearColorSerialize
			{
				get { return Serialize.BrushToString(BearColor); }
				set { BearColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		#endregion

		#region Public outputs

		[Browsable(false)][XmlIgnore] public Series<double> SmoothMcd       { get { return Values[0]; } }
		[Browsable(false)][XmlIgnore] public Series<double> Signal          { get { return Values[1]; } }
		[Browsable(false)][XmlIgnore] public Series<double> Histogram       { get { return Values[2]; } }
		[Browsable(false)][XmlIgnore] public Series<double> SuperSlow       { get { return Values[3]; } }

		#endregion

		#region Variables

		// HLC3 input + median + range — Infinite for OnRender consumption.
		private Series<double>	sHlc3;
		private Series<double>	sBasis;
		private Series<double>	sRangeBasis;
		private Series<double>	sMcd;
		private Series<double>	sSmoothMcd;
		private Series<double>	sSig;
		private Series<double>	sHist;

		// Reusable buffer for the rolling-median sort (avoids per-bar allocation).
		private double[]		medianBuffer;

		// SharpDX brushes — built once per render-target lifecycle.
		private SharpDX.Direct2D1.SolidColorBrush dxObExtreme, dxObStrong;
		private SharpDX.Direct2D1.SolidColorBrush dxOsExtreme, dxOsStrong;
		private SharpDX.Direct2D1.SolidColorBrush dxSlowFillBull, dxSlowFillBear;
		private SharpDX.Direct2D1.SolidColorBrush dxSuperBullStrong, dxSuperBullWeak;
		private SharpDX.Direct2D1.SolidColorBrush dxSuperBearStrong, dxSuperBearWeak;

		// Cached alpha-127 variants of the palette colours (constant), used per bar for the histogram
		// + super-slow plot brushes. Built once in DataLoaded so no SolidColorBrush is minted per bar.
		private Brush bullDim, bearDim;

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

				BasisLength		= 21;
				SmoothLength	= 21;
				SignalLength	= 14;
				Standardize		= true;
				Mode			= DynamicMedianMomentumOscillator_Mode.All;

				// Pine source defaults: green = #00FFBB, red = #FF1100.
				BullColor = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0xBB));
				BearColor = new SolidColorBrush(Color.FromRgb(0xFF, 0x11, 0x00));
				EnsureFrozen(BullColor);
				EnsureFrozen(BearColor);

				// Lines mirror Pine plot colors: white smooth_mcd, orange signal.
				AddPlot(new Stroke(Brushes.WhiteSmoke,	DashStyleHelper.Solid, 1f), PlotStyle.Line,    "Smooth MCD");
				AddPlot(new Stroke(Brushes.Orange,		DashStyleHelper.Solid, 1f), PlotStyle.Line,    "Signal");
				AddPlot(new Stroke(Brushes.DimGray,		DashStyleHelper.Solid, 1f), PlotStyle.Bar,     "Histogram");
				AddPlot(new Stroke(Brushes.DimGray,		DashStyleHelper.Solid, 2f), PlotStyle.Line,    "Super Slow");

				// Pine hlines — only render visible when Standardize is on; HLines stay drawn for reference.
				AddLine(new Stroke(new SolidColorBrush(Color.FromArgb(96, 192, 192, 192)), DashStyleHelper.Dot, 1f),  300, "Extreme OB");
				AddLine(new Stroke(new SolidColorBrush(Color.FromArgb(96, 192, 192, 192)), DashStyleHelper.Dot, 1f),  250, "Strong OB");
				AddLine(new Stroke(new SolidColorBrush(Color.FromArgb(96, 192, 192, 192)), DashStyleHelper.Dot, 1f),  200, "Overbought");
				AddLine(new Stroke(new SolidColorBrush(Color.FromArgb(96, 192, 192, 192)), DashStyleHelper.Dot, 1f),    0, "Zero");
				AddLine(new Stroke(new SolidColorBrush(Color.FromArgb(96, 192, 192, 192)), DashStyleHelper.Dot, 1f), -200, "Oversold");
				AddLine(new Stroke(new SolidColorBrush(Color.FromArgb(96, 192, 192, 192)), DashStyleHelper.Dot, 1f), -250, "Strong OS");
				AddLine(new Stroke(new SolidColorBrush(Color.FromArgb(96, 192, 192, 192)), DashStyleHelper.Dot, 1f), -300, "Extreme OS");
			}
			else if (State == State.DataLoaded)
			{
				sHlc3       = new Series<double>(this);
				sRangeBasis = new Series<double>(this);
				sMcd        = new Series<double>(this);
				// OnRender consumes Smooth MCD / Sig / SuperSlow across the visible window — Infinite.
				sBasis      = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sSmoothMcd  = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sSig        = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sHist       = new Series<double>(this);

				medianBuffer = new double[Math.Max(BasisLength, 1)];

				// Cache constant translucent brushes once (colours come from frozen properties).
				bullDim = WithAlpha(BullColor, 127);
				bearDim = WithAlpha(BearColor, 127);
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
			if (CurrentBar < 1) { ResetAll(); return; }

			// HLC3 source.
			sHlc3[0] = (High[0] + Low[0] + Close[0]) / 3.0;

			// Rolling median of HLC3 over BasisLength bars.
			sBasis[0] = RollingMedian(sHlc3, BasisLength);

			// EMA of (high-low) over BasisLength as the standardization scale.
			double range = High[0] - Low[0];
			sRangeBasis[0] = CurrentBar == 0 ? range : EmaStep(sRangeBasis[1], range, BasisLength);

			// Raw or standardized oscillator.
			double rawMcd = Close[0] - sBasis[0];
			double mcd    = Standardize && sRangeBasis[0] > 0.0 ? rawMcd / sRangeBasis[0] * 100.0 : rawMcd;
			sMcd[0]       = mcd;

			// Smooth MCD = EMA(mcd, smoothLen). Signal = EMA(smoothMcd, sigLen).
			sSmoothMcd[0] = CurrentBar == 0 ? mcd : EmaStep(sSmoothMcd[1], mcd, SmoothLength);
			sSig[0]       = CurrentBar == 0 ? sSmoothMcd[0] : EmaStep(sSig[1], sSmoothMcd[0], SignalLength);
			sHist[0]      = sSmoothMcd[0] - sSig[0];

			bool showFast      = Mode == DynamicMedianMomentumOscillator_Mode.All  || Mode == DynamicMedianMomentumOscillator_Mode.Fast;
			bool showSlow      = Mode == DynamicMedianMomentumOscillator_Mode.All  || Mode == DynamicMedianMomentumOscillator_Mode.Slow;
			bool showSuperSlow = Mode == DynamicMedianMomentumOscillator_Mode.SuperSlow;

			// Plot values driven by mode — plot.style_linebr equivalent via Reset on the inactive side.
			if (showSlow) { Values[0][0] = sSmoothMcd[0]; Values[1][0] = sSig[0]; }
			else          { Values[0].Reset(); Values[1].Reset(); }

			if (showFast) Values[2][0] = sHist[0]; else Values[2].Reset();

			if (showSuperSlow) Values[3][0] = sSmoothMcd[0]; else Values[3].Reset();

			// Histogram per-bar color: green if hist>0 (lighter when falling), red if hist<0 (lighter when rising).
			double prevHist = CurrentBar > 0 ? sHist[1] : sHist[0];
			if (showFast)
			{
				if (sHist[0] > 0)
					PlotBrushes[2][0] = sHist[0] < prevHist ? bullDim : BullColor;
				else
					PlotBrushes[2][0] = sHist[0] > prevHist ? bearDim : BearColor;
			}

			// Slow lines keep their default white / orange — handled by Stroke color.

			// Super Slow line color: bull/bear depending on sign of smooth_mcd, with weaker alpha when "wrong-side of sig".
			if (showSuperSlow)
			{
				bool strong = sSmoothMcd[0] >= 0 ? sSmoothMcd[0] > sSig[0] : sSmoothMcd[0] < sSig[0];
				Brush baseB = sSmoothMcd[0] >= 0 ? BullColor : BearColor;
				PlotBrushes[3][0] = strong ? baseB : (sSmoothMcd[0] >= 0 ? bullDim : bearDim);
			}

			// Reversion markers — only when Standardize is on AND mode is not Fast.
			bool showReversionMarks = Standardize && Mode != DynamicMedianMomentumOscillator_Mode.Fast;
			if (showReversionMarks && CurrentBar >= 1)
			{
				double prevMcd = sSmoothMcd[1];
				double curMcd  = sSmoothMcd[0];

				if (prevMcd >= 250 && curMcd <  250)  Draw.TriangleDown(this, "dmmObStrong"  + CurrentBar, false, 0,  320, BearColor);
				if (prevMcd <= -250 && curMcd > -250) Draw.TriangleUp  (this, "dmmOsStrong"  + CurrentBar, false, 0, -320, BullColor);
				if (prevMcd >= 300 && curMcd <  300)  Draw.TriangleDown(this, "dmmObReversion" + CurrentBar, false, 0,  320, BearColor);
				if (prevMcd <= -300 && curMcd > -300) Draw.TriangleUp  (this, "dmmOsReversion" + CurrentBar, false, 0, -320, BullColor);
			}

			// Slow bull / bear cross dot markers — at sig[1] price level per Pine.
			if (showSlow && CurrentBar >= 2)
			{
				double prevDelta = sSmoothMcd[1] - sSig[1];
				double curDelta  = sSmoothMcd[0] - sSig[0];
				double sigPrev   = sSig[1];

				if (prevDelta <= 0 && curDelta > 0) Draw.Dot(this, "dmmBullX" + CurrentBar, false, 0, sigPrev, Brushes.WhiteSmoke);
				if (prevDelta >= 0 && curDelta < 0) Draw.Dot(this, "dmmBearX" + CurrentBar, false, 0, sigPrev, Brushes.Orange);
			}
		}

		private void ResetAll()
		{
			Values[0].Reset();
			Values[1].Reset();
			Values[2].Reset();
			Values[3].Reset();
		}

		#endregion

		#region Math helpers

		// EMA recurrence: out = (in - prev) * (2/(len+1)) + prev.
		private static double EmaStep(double prev, double cur, int len)
		{
			double k = 2.0 / (len + 1.0);
			return (cur - prev) * k + prev;
		}

		// Rolling median of `source` over `len` bars (or fewer when warming up).
		private double RollingMedian(Series<double> source, int len)
		{
			int n = Math.Min(len, CurrentBar + 1);
			if (medianBuffer == null || medianBuffer.Length < n)
				medianBuffer = new double[Math.Max(n, len)];
			for (int i = 0; i < n; i++) medianBuffer[i] = source[i];
			Array.Sort(medianBuffer, 0, n);
			return n % 2 == 0 ? (medianBuffer[n / 2 - 1] + medianBuffer[n / 2]) / 2.0 : medianBuffer[n / 2];
		}

		#endregion

		#region SharpDX area fills

		public override void OnRenderTargetChanged()
		{
			try
			{
				ReleaseRenderResources();
				if (RenderTarget == null) return;

				// OB/OS bands — Pine `color.new(c, 50/80)` → alpha 0.5 / 0.2.
				dxObExtreme = MakeDXBrush(BearColor, 0.50f);
				dxObStrong  = MakeDXBrush(BearColor, 0.20f);
				dxOsExtreme = MakeDXBrush(BullColor, 0.50f);
				dxOsStrong  = MakeDXBrush(BullColor, 0.20f);

				// Slow trend fill — Pine `color.new(white/orange, 95)` → alpha 0.05 (very subtle).
				dxSlowFillBull = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color4(1f, 1f, 1f, 0.06f));
				dxSlowFillBear = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color4(1f, 0.65f, 0f, 0.06f));

				// Super Slow fill — Pine `color.new(c, 85/92)` → alpha 0.15 / 0.08.
				dxSuperBullStrong = MakeDXBrush(BullColor, 0.15f);
				dxSuperBullWeak   = MakeDXBrush(BullColor, 0.08f);
				dxSuperBearStrong = MakeDXBrush(BearColor, 0.15f);
				dxSuperBearWeak   = MakeDXBrush(BearColor, 0.08f);
			}
			catch
			{
				// Suppress during chart teardown.
			}
		}

		private void ReleaseRenderResources()
		{
			void D(ref SharpDX.Direct2D1.SolidColorBrush b) { if (b != null) { b.Dispose(); b = null; } }
			D(ref dxObExtreme); D(ref dxObStrong);
			D(ref dxOsExtreme); D(ref dxOsStrong);
			D(ref dxSlowFillBull); D(ref dxSlowFillBear);
			D(ref dxSuperBullStrong); D(ref dxSuperBullWeak);
			D(ref dxSuperBearStrong); D(ref dxSuperBearWeak);
		}

		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			base.OnRender(chartControl, chartScale);

			if (RenderTarget == null)	return;
			if (ChartBars == null)		return;
			if (!IsVisible)				return;
			if (IsInHitTest)			return;

			int fromIdx = Math.Max(ChartBars.FromIndex, 0);
			int toIdx   = Math.Min(ChartBars.ToIndex,   CurrentBar);
			if (toIdx <= fromIdx) return;

			float panelLeft  = ChartPanel.X;
			float panelRight = ChartPanel.X + ChartPanel.W;

			bool showSlow      = Mode == DynamicMedianMomentumOscillator_Mode.All || Mode == DynamicMedianMomentumOscillator_Mode.Slow;
			bool showSuperSlow = Mode == DynamicMedianMomentumOscillator_Mode.SuperSlow;

			// OB/OS bands — full-width horizontal rectangles, only when Standardize is enabled.
			if (Standardize)
			{
				DrawHBand(chartScale, panelLeft, panelRight,  250,  300, dxObExtreme);
				DrawHBand(chartScale, panelLeft, panelRight,  200,  250, dxObStrong);
				DrawHBand(chartScale, panelLeft, panelRight, -250, -200, dxOsStrong);
				DrawHBand(chartScale, panelLeft, panelRight, -300, -250, dxOsExtreme);
			}

			// Slow trend fill — translucent band between Smooth MCD and Signal, colored by which is on top.
			if (showSlow)
			{
				for (int j = fromIdx; j < toIdx; j++)
				{
					if (!sSmoothMcd.IsValidDataPointAt(j) || !sSmoothMcd.IsValidDataPointAt(j + 1)) continue;
					if (!sSig.IsValidDataPointAt(j)       || !sSig.IsValidDataPointAt(j + 1))       continue;

					double mJ  = sSmoothMcd.GetValueAt(j);
					double mJ1 = sSmoothMcd.GetValueAt(j + 1);
					double sJ  = sSig.GetValueAt(j);
					double sJ1 = sSig.GetValueAt(j + 1);

					var brush = (mJ + mJ1) > (sJ + sJ1) ? dxSlowFillBull : dxSlowFillBear;
					DrawTrapezoid(chartControl, chartScale, j,
						Math.Max(mJ, sJ), Math.Max(mJ1, sJ1),
						Math.Min(mJ, sJ), Math.Min(mJ1, sJ1), brush);
				}
			}

			// Super Slow fill — between Smooth MCD and zero, with strong/weak shading.
			if (showSuperSlow)
			{
				for (int j = fromIdx; j < toIdx; j++)
				{
					if (!sSmoothMcd.IsValidDataPointAt(j) || !sSmoothMcd.IsValidDataPointAt(j + 1)) continue;
					if (!sSig.IsValidDataPointAt(j)       || !sSig.IsValidDataPointAt(j + 1))       continue;

					double mJ  = sSmoothMcd.GetValueAt(j);
					double mJ1 = sSmoothMcd.GetValueAt(j + 1);

					// Per-bar strong/weak — picked from the right-hand bar (j+1) state.
					double sJ1 = sSig.GetValueAt(j + 1);
					bool strong = mJ1 >= 0 ? mJ1 > sJ1 : mJ1 < sJ1;

					SharpDX.Direct2D1.SolidColorBrush brush;
					if (mJ1 >= 0) brush = strong ? dxSuperBullStrong : dxSuperBullWeak;
					else          brush = strong ? dxSuperBearStrong : dxSuperBearWeak;

					// Both ends same side of zero — single trapezoid.
					if (mJ >= 0 && mJ1 >= 0)
						DrawTrapezoid(chartControl, chartScale, j, mJ, mJ1, 0.0, 0.0, brush);
					else if (mJ <= 0 && mJ1 <= 0)
						DrawTrapezoid(chartControl, chartScale, j, 0.0, 0.0, mJ, mJ1, brush);
					else
					{
						// Crossing zero — split using the bar's own sign for each half.
						double clampTop = Math.Max(mJ, 0); double clampTopJ1 = Math.Max(mJ1, 0);
						double clampBot = Math.Min(mJ, 0); double clampBotJ1 = Math.Min(mJ1, 0);
						SharpDX.Direct2D1.SolidColorBrush bullB = strong ? dxSuperBullStrong : dxSuperBullWeak;
						SharpDX.Direct2D1.SolidColorBrush bearB = strong ? dxSuperBearStrong : dxSuperBearWeak;
						DrawTrapezoid(chartControl, chartScale, j, clampTop, clampTopJ1, 0.0, 0.0, bullB);
						DrawTrapezoid(chartControl, chartScale, j, 0.0, 0.0, clampBot, clampBotJ1, bearB);
					}
				}
			}

		}

		private void DrawHBand(ChartScale chartScale, float panelLeft, float panelRight,
			double yLow, double yHigh, SharpDX.Direct2D1.SolidColorBrush brush)
		{
			if (brush == null) return;
			float yA = (float)chartScale.GetYByValue(yHigh);
			float yB = (float)chartScale.GetYByValue(yLow);
			RenderTarget.FillRectangle(
				new SharpDX.RectangleF(panelLeft, Math.Min(yA, yB), panelRight - panelLeft, Math.Abs(yA - yB)),
				brush);
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

			if (Math.Abs(yTopL - yBotL) < 0.5f && Math.Abs(yTopR - yBotR) < 0.5f) return;

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

		#endregion

		#region Helpers

		private static Brush WithAlpha(Brush src, byte alpha)
		{
			var scb = src as SolidColorBrush;
			Color c = scb != null ? scb.Color : Colors.Gray;
			var b = new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
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
		private indTradingView.DynamicMedianMomentumOscillator[] cacheDynamicMedianMomentumOscillator;
		public indTradingView.DynamicMedianMomentumOscillator DynamicMedianMomentumOscillator(int basisLength, int smoothLength, int signalLength, bool standardize, DynamicMedianMomentumOscillator_Mode mode)
		{
			return DynamicMedianMomentumOscillator(Input, basisLength, smoothLength, signalLength, standardize, mode);
		}

		public indTradingView.DynamicMedianMomentumOscillator DynamicMedianMomentumOscillator(ISeries<double> input, int basisLength, int smoothLength, int signalLength, bool standardize, DynamicMedianMomentumOscillator_Mode mode)
		{
			if (cacheDynamicMedianMomentumOscillator != null)
				for (int idx = 0; idx < cacheDynamicMedianMomentumOscillator.Length; idx++)
					if (cacheDynamicMedianMomentumOscillator[idx] != null && cacheDynamicMedianMomentumOscillator[idx].BasisLength == basisLength && cacheDynamicMedianMomentumOscillator[idx].SmoothLength == smoothLength && cacheDynamicMedianMomentumOscillator[idx].SignalLength == signalLength && cacheDynamicMedianMomentumOscillator[idx].Standardize == standardize && cacheDynamicMedianMomentumOscillator[idx].Mode == mode && cacheDynamicMedianMomentumOscillator[idx].EqualsInput(input))
						return cacheDynamicMedianMomentumOscillator[idx];
			return CacheIndicator<indTradingView.DynamicMedianMomentumOscillator>(new indTradingView.DynamicMedianMomentumOscillator(){ BasisLength = basisLength, SmoothLength = smoothLength, SignalLength = signalLength, Standardize = standardize, Mode = mode }, input, ref cacheDynamicMedianMomentumOscillator);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.DynamicMedianMomentumOscillator DynamicMedianMomentumOscillator(int basisLength, int smoothLength, int signalLength, bool standardize, DynamicMedianMomentumOscillator_Mode mode)
		{
			return indicator.DynamicMedianMomentumOscillator(Input, basisLength, smoothLength, signalLength, standardize, mode);
		}

		public Indicators.indTradingView.DynamicMedianMomentumOscillator DynamicMedianMomentumOscillator(ISeries<double> input , int basisLength, int smoothLength, int signalLength, bool standardize, DynamicMedianMomentumOscillator_Mode mode)
		{
			return indicator.DynamicMedianMomentumOscillator(input, basisLength, smoothLength, signalLength, standardize, mode);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.DynamicMedianMomentumOscillator DynamicMedianMomentumOscillator(int basisLength, int smoothLength, int signalLength, bool standardize, DynamicMedianMomentumOscillator_Mode mode)
		{
			return indicator.DynamicMedianMomentumOscillator(Input, basisLength, smoothLength, signalLength, standardize, mode);
		}

		public Indicators.indTradingView.DynamicMedianMomentumOscillator DynamicMedianMomentumOscillator(ISeries<double> input , int basisLength, int smoothLength, int signalLength, bool standardize, DynamicMedianMomentumOscillator_Mode mode)
		{
			return indicator.DynamicMedianMomentumOscillator(input, basisLength, smoothLength, signalLength, standardize, mode);
		}
	}
}

#endregion
