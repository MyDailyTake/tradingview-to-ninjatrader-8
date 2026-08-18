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

// NT8 Version of SuperTrend Oscillator
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the Mozilla Public License 2.0 (MPL 2.0).
// The original Pine Script™ code is by ChartPrime and can be found at: https://www.tradingview.com/script/JqEFTgOE-SuperTrend-Oscillator-ChartPrime/
// Adaptation for NinjaTrader by jack@mydailytake.com
// © 2026 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the Mozilla Public License 2.0. Full license details at https://mozilla.org/MPL/2.0/
// The use of ChartPrime name or its adapted code in this work does not imply endorsement by the original authors.
//
// Notes:
//   Distance-from-SuperTrend oscillator. A standard SuperTrend (HL2 ± Multiplier × ATR with band locking)
//   tracks the trend; the difference (Close − SuperTrend) is smoothed via a chosen MA (HMA / SMA / EMA),
//   then normalized by Multiplier × ATR so the result lives roughly in ±1.7. A gradient color from bear
//   color at −1 through gray at 0 to bull color at +1 highlights momentum strength.
//
//   Reversal diamonds fire when the oscillator crosses its 3-bar prior value AND is past ±0.5 — strong
//   internal-momentum reversal flag — guarded by a 10-bar refractory window.
//
//   Documented scope decisions:
//     • SuperTrend line + fill on price panel — omitted; NT8 sub-panel indicators can't draw arbitrary
//       SharpDX geometry on the price panel. Bar-color flow via BarBrushes preserves the trend cue.
//     • Pine `barcolor` + `plotcandle(force_overlay)` collapsed to a single BarBrushes assignment.
//     • Pine 6-arg gradient fills (`fill(p, p, top, bot, c1, c2)`) implemented as solid translucent
//       trapezoids using the bottom-color tint at fixed alpha. Visual difference is minor.
//
//   Non-repainting: SuperTrend uses standard band-locking / direction-from-prior pattern. Public Series
//   outputs: Oscillator, SuperTrend, Direction (1 = uptrend, −1 = downtrend).

#region Enums SuperTrendOscillator

public enum SuperTrendOscillator_OscType
{
	HMA,
	SMA,
	EMA
}

#endregion

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	#region Categories
	[Gui.CategoryOrder("SuperTrend",	10100)]
	[Gui.CategoryOrder("Oscillator",	10200)]
	[Gui.CategoryOrder("Appearance",	10300)]
	#endregion

	public class SuperTrendOscillator : Indicator
	{
		#region indInfo

		private string indName        = "SuperTrend Oscillator [ChartPrime]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by ChartPrime can be found here: https://www.tradingview.com/script/JqEFTgOE-SuperTrend-Oscillator-ChartPrime/";

		#endregion

		#region Properties

		[NinjaScriptProperty]
		[Range(0.1, 50.0)]
		[Display(Order = 1, GroupName = "SuperTrend", Name = "ATR Multiplier",
			Description = "Multiplier applied to ATR to size the SuperTrend bands.")]
		public double Multiplier { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 2, GroupName = "SuperTrend", Name = "ATR Length",
			Description = "ATR period used for both the SuperTrend and the oscillator's normalization.")]
		public int AtrLength { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 1, GroupName = "Oscillator", Name = "Oscillator Type",
			Description = "MA type used to smooth (Close − SuperTrend).")]
		public SuperTrendOscillator_OscType OscillatorType { get; set; }

		[NinjaScriptProperty]
		[Range(2, int.MaxValue)]
		[Display(Order = 2, GroupName = "Oscillator", Name = "Oscillator Smoothing",
			Description = "Lookback for the oscillator's smoothing MA.")]
		public int OscillatorSmooth { get; set; }

		[XmlIgnore]
		[Display(Order = 1, GroupName = "Appearance", Name = "Bullish Color",
			Description = "Color for the upper-zone gradient and bullish bar/oscillator coloring.")]
		public Brush BullColor { get; set; }
			[Browsable(false)]
			public string BullColorSerialize
			{
				get { return Serialize.BrushToString(BullColor); }
				set { BullColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore]
		[Display(Order = 2, GroupName = "Appearance", Name = "Bearish Color",
			Description = "Color for the lower-zone gradient and bearish bar/oscillator coloring.")]
		public Brush BearColor { get; set; }
			[Browsable(false)]
			public string BearColorSerialize
			{
				get { return Serialize.BrushToString(BearColor); }
				set { BearColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore]
		[Display(Order = 3, GroupName = "Appearance", Name = "Neutral Color",
			Description = "Mid-gradient color when the oscillator is near zero.")]
		public Brush NeutralColor { get; set; }
			[Browsable(false)]
			public string NeutralColorSerialize
			{
				get { return Serialize.BrushToString(NeutralColor); }
				set { NeutralColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		#endregion

		#region Public outputs

		[Browsable(false)][XmlIgnore] public Series<double> Oscillator   { get { return Values[0]; } }
		[Browsable(false)][XmlIgnore] public Series<double> SuperTrend   { get { Update(); return sSuperTrend; } }
		[Browsable(false)][XmlIgnore] public Series<int>    Direction    { get { Update(); return sDirection; } }

		#endregion

		#region Variables

		private ATR			atrInd;

		// Source-MA cache (for HMA we'll need WMA ladder — implemented inline as scalar fields).
		private Series<double>	sCloseMinusST;	// (Close − SuperTrend) input to the smoother
		private Series<double>	sOsc;			// final normalized oscillator — Infinite for OnRender

		// SuperTrend recursive state.
		private Series<double>	sUpper;
		private Series<double>	sLower;
		private Series<double>	sSuperTrend;
		private Series<int>		sDirection;

		// Custom HMA helper backing series.
		private Series<double>	sWmaHalf;
		private Series<double>	sWmaFull;
		private Series<double>	sWmaDiff;

		// SharpDX gradient stops — built once per render-target lifecycle, rebuilt on color change.
		// Bodies are full-opacity at the saturated end; zones are 30%-opacity (Pine `color.new(c, 70)`).
		private SharpDX.Direct2D1.GradientStopCollection gsBullBody;	// peach at y=1 → transparent at y=0
		private SharpDX.Direct2D1.GradientStopCollection gsBearBody;	// transparent at y=0 → red at y=-1
		private SharpDX.Direct2D1.GradientStopCollection gsUpperZone;	// red 30% at y=1.7 → transparent at y=0
		private SharpDX.Direct2D1.GradientStopCollection gsLowerZone;	// transparent at y=0 → peach 30% at y=-1.7
		private SharpDX.Color4 lastBullC4;
		private SharpDX.Color4 lastBearC4;

		// Reversal-diamond refractory.
		private int lastReversalBar = int.MinValue;

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

				Multiplier			= 4.0;
				AtrLength			= 100;
				OscillatorType		= SuperTrendOscillator_OscType.HMA;
				OscillatorSmooth	= 25;

				// Pine source colors: colMax = #FFDFB9 (peach), colMin = #E24D3F (red).
				BullColor    = new SolidColorBrush(Color.FromRgb(0xFF, 0xDF, 0xB9));
				BearColor    = new SolidColorBrush(Color.FromRgb(0xE2, 0x4D, 0x3F));
				NeutralColor = new SolidColorBrush(Color.FromArgb(255, 128, 128, 128));
				EnsureFrozen(BullColor);
				EnsureFrozen(BearColor);
				EnsureFrozen(NeutralColor);

				AddPlot(new Stroke(Brushes.DimGray, DashStyleHelper.Solid, 2f), PlotStyle.Line, "Oscillator");

				AddLine(new Stroke(new SolidColorBrush(Color.FromArgb(96, 192, 192, 192)), DashStyleHelper.Solid, 1f),  0,    "Zero");
				AddLine(new Stroke(new SolidColorBrush(Color.FromArgb(96, 192, 192, 192)), DashStyleHelper.Dot,   1f),  1.7,  "Upper");
				AddLine(new Stroke(new SolidColorBrush(Color.FromArgb(96, 192, 192, 192)), DashStyleHelper.Dot,   1f), -1.7,  "Lower");
			}
			else if (State == State.DataLoaded)
			{
				atrInd = ATR(AtrLength);

				// OscillatorSmooth can exceed 256 default lookback for WMA history reads — Infinite.
				sCloseMinusST = new Series<double>(this, MaximumBarsLookBack.Infinite);
				// OnRender consumes the oscillator across the visible window — Infinite.
				sOsc          = new Series<double>(this, MaximumBarsLookBack.Infinite);

				sUpper       = new Series<double>(this);
				sLower       = new Series<double>(this);
				sSuperTrend  = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sDirection   = new Series<int>(this,    MaximumBarsLookBack.Infinite);

				sWmaHalf = new Series<double>(this);
				sWmaFull = new Series<double>(this);
				sWmaDiff = new Series<double>(this);
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
			int needed = Math.Max(AtrLength, OscillatorSmooth) + 5;
			if (CurrentBar < needed)
			{
				sUpper[0] = High[0];
				sLower[0] = Low[0];
				sSuperTrend[0] = (High[0] + Low[0]) / 2.0;
				sDirection[0] = 1;
				sCloseMinusST[0] = 0;
				sOsc[0] = 0;
				Values[0].Reset();
				return;
			}

			// SuperTrend with band-locking and direction flip.
			double hl2  = (High[0] + Low[0]) / 2.0;
			double atrM = atrInd[0] * Multiplier;
			double upRaw = hl2 + atrM;
			double dnRaw = hl2 - atrM;

			double prevUp = sUpper[1];
			double prevDn = sLower[1];
			double prevClose = Close[1];

			double upper = upRaw < prevUp || prevClose > prevUp ? upRaw : prevUp;
			double lower = dnRaw > prevDn || prevClose < prevDn ? dnRaw : prevDn;

			int prevDir = sDirection[1];
			int dir = prevDir;
			if (prevDir == -1)
			{
				// Prior uptrend (Pine convention: direction == -1 = up). Flip to down when close < lower.
				if (Close[0] < lower) dir = 1;
			}
			else
			{
				// Prior downtrend (direction == 1). Flip to up when close > upper.
				if (Close[0] > upper) dir = -1;
			}

			double st = dir == -1 ? lower : upper;

			sUpper[0]      = upper;
			sLower[0]      = lower;
			sSuperTrend[0] = st;
			sDirection[0]  = dir;

			sCloseMinusST[0] = Close[0] - st;

			// Smoother — HMA / SMA / EMA of (Close − SuperTrend).
			double smoothed = 0;
			switch (OscillatorType)
			{
				case SuperTrendOscillator_OscType.SMA: smoothed = SMA(sCloseMinusST, OscillatorSmooth)[0]; break;
				case SuperTrendOscillator_OscType.EMA: smoothed = EMA(sCloseMinusST, OscillatorSmooth)[0]; break;
				case SuperTrendOscillator_OscType.HMA: smoothed = HmaStep(sCloseMinusST, OscillatorSmooth); break;
			}

			double osc = atrM > 0 ? smoothed / atrM : 0;
			sOsc[0]    = osc;
			Values[0][0] = osc;

			// Per-bar color flow — gradient between bear (osc=−1), neutral (osc=0), bull (osc=+1).
			Brush oscBrush = osc < 0 ? GradientBrush(osc, -1.0, 0.0, BearColor, NeutralColor)
			                          : GradientBrush(osc,  0.0, 1.0, NeutralColor, BullColor);
			PlotBrushes[0][0] = oscBrush;
			BarBrushes[0]    = oscBrush;
			CandleOutlineBrushes[0] = oscBrush;

			// Reversal diamonds — crossover/crossunder of osc vs osc[3] beyond ±0.5, with 10-bar refractory.
			if (CurrentBar >= 4)
			{
				double prevOsc  = sOsc[1];
				double curOsc   = sOsc[0];
				double osc3prev = sOsc[4];	// osc_[3] one bar ago
				double osc3now  = sOsc[3];	// osc_[3] this bar

				bool revDown = prevOsc >= osc3prev && curOsc < osc3now && curOsc > 0.5;
				bool revUp   = prevOsc <= osc3prev && curOsc > osc3now && curOsc < -0.5;

				bool refractoryOk = (CurrentBar - lastReversalBar) > 10;
				if (revDown && refractoryOk)
				{
					Draw.Diamond(this, "stoRevD" + CurrentBar, false, 1, sOsc[1], BearColor);
					DrawOnPricePanel = true;
					Draw.Diamond(this, "stoRevDPx" + CurrentBar, true, 1, High[1] + TickSize * 4, BearColor);
					DrawOnPricePanel = false;
					lastReversalBar = CurrentBar;
				}
				if (revUp && refractoryOk)
				{
					Draw.Diamond(this, "stoRevU" + CurrentBar, false, 1, sOsc[1], BullColor);
					DrawOnPricePanel = true;
					Draw.Diamond(this, "stoRevUPx" + CurrentBar, true, 1, Low[1] - TickSize * 4, BullColor);
					DrawOnPricePanel = false;
					lastReversalBar = CurrentBar;
				}
			}
		}

		// HMA(n) = WMA(n/2) of input − WMA(n) of input → WMA(sqrt(n)) of that diff.
		private double HmaStep(Series<double> src, int len)
		{
			int half = Math.Max(1, len / 2);
			int sqrt = Math.Max(1, (int)Math.Round(Math.Sqrt(len)));
			sWmaHalf[0] = WmaFromSeries(src, half);
			sWmaFull[0] = WmaFromSeries(src, len);
			sWmaDiff[0] = 2.0 * sWmaHalf[0] - sWmaFull[0];
			return WmaFromSeries(sWmaDiff, sqrt);
		}

		private double WmaFromSeries(Series<double> src, int len)
		{
			if (len <= 0) return src[0];
			int n = Math.Min(len, CurrentBar + 1);
			double weightedSum = 0, weightSum = 0;
			for (int i = 0; i < n; i++)
			{
				int w = n - i;
				weightedSum += src[i] * w;
				weightSum   += w;
			}
			return weightSum > 0 ? weightedSum / weightSum : 0;
		}

		#endregion

		#region SharpDX area fills

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
			SharpDX.Color4 bearSolid = ToColor4(BearColor, 1.0f);
			SharpDX.Color4 bullClear = new SharpDX.Color4(bullSolid.Red, bullSolid.Green, bullSolid.Blue, 0f);
			SharpDX.Color4 bearClear = new SharpDX.Color4(bearSolid.Red, bearSolid.Green, bearSolid.Blue, 0f);
			SharpDX.Color4 bullZone  = new SharpDX.Color4(bullSolid.Red, bullSolid.Green, bullSolid.Blue, 0.30f);
			SharpDX.Color4 bearZone  = new SharpDX.Color4(bearSolid.Red, bearSolid.Green, bearSolid.Blue, 0.30f);

			// Bull body — peach at y=1, transparent at y=0. StartPoint→EndPoint will be (yAt1)→(yAt0).
			gsBullBody = new SharpDX.Direct2D1.GradientStopCollection(RenderTarget, new[]
			{
				new SharpDX.Direct2D1.GradientStop { Position = 0f, Color = bullSolid },
				new SharpDX.Direct2D1.GradientStop { Position = 1f, Color = bullClear }
			});

			// Bear body — transparent at y=0, red at y=-1. StartPoint→EndPoint will be (yAt0)→(yAtNeg1).
			gsBearBody = new SharpDX.Direct2D1.GradientStopCollection(RenderTarget, new[]
			{
				new SharpDX.Direct2D1.GradientStop { Position = 0f, Color = bearClear },
				new SharpDX.Direct2D1.GradientStop { Position = 1f, Color = bearSolid }
			});

			// Upper zone — bear-tinted (30% opacity) at y=1.7, transparent at y=0.
			gsUpperZone = new SharpDX.Direct2D1.GradientStopCollection(RenderTarget, new[]
			{
				new SharpDX.Direct2D1.GradientStop { Position = 0f, Color = bearZone },
				new SharpDX.Direct2D1.GradientStop { Position = 1f, Color = bearClear }
			});

			// Lower zone — transparent at y=0, bull-tinted (30% opacity) at y=-1.7.
			gsLowerZone = new SharpDX.Direct2D1.GradientStopCollection(RenderTarget, new[]
			{
				new SharpDX.Direct2D1.GradientStop { Position = 0f, Color = bullClear },
				new SharpDX.Direct2D1.GradientStop { Position = 1f, Color = bullZone }
			});

			lastBullC4 = bullSolid;
			lastBearC4 = bearSolid;
		}

		private void EnsureGradientStops()
		{
			if (gsBullBody == null || gsBearBody == null || gsUpperZone == null || gsLowerZone == null)
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
			if (gsBullBody  != null) { gsBullBody.Dispose();  gsBullBody  = null; }
			if (gsBearBody  != null) { gsBearBody.Dispose();  gsBearBody  = null; }
			if (gsUpperZone != null) { gsUpperZone.Dispose(); gsUpperZone = null; }
			if (gsLowerZone != null) { gsLowerZone.Dispose(); gsLowerZone = null; }
		}

		private void ReleaseRenderResources()
		{
			DisposeGradientStops();
		}

		private static SharpDX.Color4 ToColor4(Brush wpf, float alpha)
		{
			var scb = wpf as SolidColorBrush;
			System.Windows.Media.Color c = scb != null ? scb.Color : Colors.Gray;
			float wpfA = scb != null ? (float)scb.Opacity : 1f;
			return new SharpDX.Color4(c.R / 255f, c.G / 255f, c.B / 255f, Math.Max(0f, Math.Min(1f, alpha * wpfA)));
		}

		private static bool ColorsEqual(SharpDX.Color4 a, SharpDX.Color4 b)
		{
			return a.Red == b.Red && a.Green == b.Green && a.Blue == b.Blue && a.Alpha == b.Alpha;
		}

		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			base.OnRender(chartControl, chartScale);

			if (RenderTarget == null)	return;
			if (ChartBars == null)		return;
			if (!IsVisible)				return;
			if (IsInHitTest)			return;

			EnsureGradientStops();
			if (gsBullBody == null || gsBearBody == null || gsUpperZone == null || gsLowerZone == null) return;

			int fromIdx = Math.Max(ChartBars.FromIndex, 0);
			int toIdx   = Math.Min(ChartBars.ToIndex,   CurrentBar);
			if (toIdx <= fromIdx) return;

			// Gradient anchors in screen-Y at the fixed value-space stops. Per the canonical anti-seam
			// rule: brushes anchored to GLOBAL extents (not per-bar edges) so the gradient is a single
			// vertical wash across the panel — no seams between bars.
			float yAtPos17  = (float)chartScale.GetYByValue( 1.7);
			float yAtPos1   = (float)chartScale.GetYByValue( 1.0);
			float yAt0      = (float)chartScale.GetYByValue( 0.0);
			float yAtNeg1   = (float)chartScale.GetYByValue(-1.0);
			float yAtNeg17  = (float)chartScale.GetYByValue(-1.7);

			SharpDX.Direct2D1.LinearGradientBrush bullBodyBrush  = null;
			SharpDX.Direct2D1.LinearGradientBrush bearBodyBrush  = null;
			SharpDX.Direct2D1.LinearGradientBrush upperZoneBrush = null;
			SharpDX.Direct2D1.LinearGradientBrush lowerZoneBrush = null;
			try
			{
				bullBodyBrush = new SharpDX.Direct2D1.LinearGradientBrush(RenderTarget,
					new SharpDX.Direct2D1.LinearGradientBrushProperties
					{
						StartPoint = new SharpDX.Vector2(0f, yAtPos1),	// peach saturates at y=1, extends upward
						EndPoint   = new SharpDX.Vector2(0f, yAt0)
					},
					gsBullBody);

				bearBodyBrush = new SharpDX.Direct2D1.LinearGradientBrush(RenderTarget,
					new SharpDX.Direct2D1.LinearGradientBrushProperties
					{
						StartPoint = new SharpDX.Vector2(0f, yAt0),
						EndPoint   = new SharpDX.Vector2(0f, yAtNeg1)	// red saturates at y=-1, extends downward
					},
					gsBearBody);

				upperZoneBrush = new SharpDX.Direct2D1.LinearGradientBrush(RenderTarget,
					new SharpDX.Direct2D1.LinearGradientBrushProperties
					{
						StartPoint = new SharpDX.Vector2(0f, yAtPos17),
						EndPoint   = new SharpDX.Vector2(0f, yAt0)
					},
					gsUpperZone);

				lowerZoneBrush = new SharpDX.Direct2D1.LinearGradientBrush(RenderTarget,
					new SharpDX.Direct2D1.LinearGradientBrushProperties
					{
						StartPoint = new SharpDX.Vector2(0f, yAt0),
						EndPoint   = new SharpDX.Vector2(0f, yAtNeg17)
					},
					gsLowerZone);

				for (int j = fromIdx; j < toIdx; j++)
				{
					if (!sOsc.IsValidDataPointAt(j) || !sOsc.IsValidDataPointAt(j + 1)) continue;

					double oJ  = sOsc.GetValueAt(j);
					double oJ1 = sOsc.GetValueAt(j + 1);

					// Bull body — fill the entire 0 → osc region when osc>0. Gradient saturates at y=1.
					if (oJ > 0 || oJ1 > 0)
					{
						double topJ  = Math.Max(oJ,  0);
						double topJ1 = Math.Max(oJ1, 0);
						DrawTrapezoid(chartControl, chartScale, j, topJ, topJ1, 0, 0, bullBodyBrush);
					}

					// Bear body — fill the entire osc → 0 region when osc<0. Gradient saturates at y=-1.
					if (oJ < 0 || oJ1 < 0)
					{
						double botJ  = Math.Min(oJ,  0);
						double botJ1 = Math.Min(oJ1, 0);
						DrawTrapezoid(chartControl, chartScale, j, 0, 0, botJ, botJ1, bearBodyBrush);
					}

					// Upper zone — always drawn, bear-tint fade between 1.7 and max(osc, 0).
					double upBotJ  = oJ  > 0 ? oJ  : 0.0;
					double upBotJ1 = oJ1 > 0 ? oJ1 : 0.0;
					DrawTrapezoid(chartControl, chartScale, j, 1.7, 1.7, upBotJ, upBotJ1, upperZoneBrush);

					// Lower zone — always drawn, bull-tint fade between min(osc, 0) and -1.7.
					double dnTopJ  = oJ  < 0 ? oJ  : 0.0;
					double dnTopJ1 = oJ1 < 0 ? oJ1 : 0.0;
					DrawTrapezoid(chartControl, chartScale, j, dnTopJ, dnTopJ1, -1.7, -1.7, lowerZoneBrush);
				}
			}
			finally
			{
				if (bullBodyBrush  != null) bullBodyBrush.Dispose();
				if (bearBodyBrush  != null) bearBodyBrush.Dispose();
				if (upperZoneBrush != null) upperZoneBrush.Dispose();
				if (lowerZoneBrush != null) lowerZoneBrush.Dispose();
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

		#endregion

		#region Helpers

		// Linearly interpolate between two color brushes by clamped (val − lo)/(hi − lo).
		private static Brush GradientBrush(double val, double lo, double hi, Brush ca, Brush cb)
		{
			double t = (val - lo) / (hi - lo);
			if (t < 0) t = 0; else if (t > 1) t = 1;

			var sa = ca as SolidColorBrush; var sb = cb as SolidColorBrush;
			Color a = sa != null ? sa.Color : Colors.Gray;
			Color b = sb != null ? sb.Color : Colors.Gray;
			byte A = (byte)(a.A + (b.A - a.A) * t);
			byte R = (byte)(a.R + (b.R - a.R) * t);
			byte G = (byte)(a.G + (b.G - a.G) * t);
			byte B = (byte)(a.B + (b.B - a.B) * t);
			var brush = new SolidColorBrush(Color.FromArgb(A, R, G, B));
			brush.Freeze();
			return brush;
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
		private indTradingView.SuperTrendOscillator[] cacheSuperTrendOscillator;
		public indTradingView.SuperTrendOscillator SuperTrendOscillator(double multiplier, int atrLength, SuperTrendOscillator_OscType oscillatorType, int oscillatorSmooth)
		{
			return SuperTrendOscillator(Input, multiplier, atrLength, oscillatorType, oscillatorSmooth);
		}

		public indTradingView.SuperTrendOscillator SuperTrendOscillator(ISeries<double> input, double multiplier, int atrLength, SuperTrendOscillator_OscType oscillatorType, int oscillatorSmooth)
		{
			if (cacheSuperTrendOscillator != null)
				for (int idx = 0; idx < cacheSuperTrendOscillator.Length; idx++)
					if (cacheSuperTrendOscillator[idx] != null && cacheSuperTrendOscillator[idx].Multiplier == multiplier && cacheSuperTrendOscillator[idx].AtrLength == atrLength && cacheSuperTrendOscillator[idx].OscillatorType == oscillatorType && cacheSuperTrendOscillator[idx].OscillatorSmooth == oscillatorSmooth && cacheSuperTrendOscillator[idx].EqualsInput(input))
						return cacheSuperTrendOscillator[idx];
			return CacheIndicator<indTradingView.SuperTrendOscillator>(new indTradingView.SuperTrendOscillator(){ Multiplier = multiplier, AtrLength = atrLength, OscillatorType = oscillatorType, OscillatorSmooth = oscillatorSmooth }, input, ref cacheSuperTrendOscillator);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.SuperTrendOscillator SuperTrendOscillator(double multiplier, int atrLength, SuperTrendOscillator_OscType oscillatorType, int oscillatorSmooth)
		{
			return indicator.SuperTrendOscillator(Input, multiplier, atrLength, oscillatorType, oscillatorSmooth);
		}

		public Indicators.indTradingView.SuperTrendOscillator SuperTrendOscillator(ISeries<double> input , double multiplier, int atrLength, SuperTrendOscillator_OscType oscillatorType, int oscillatorSmooth)
		{
			return indicator.SuperTrendOscillator(input, multiplier, atrLength, oscillatorType, oscillatorSmooth);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.SuperTrendOscillator SuperTrendOscillator(double multiplier, int atrLength, SuperTrendOscillator_OscType oscillatorType, int oscillatorSmooth)
		{
			return indicator.SuperTrendOscillator(Input, multiplier, atrLength, oscillatorType, oscillatorSmooth);
		}

		public Indicators.indTradingView.SuperTrendOscillator SuperTrendOscillator(ISeries<double> input , double multiplier, int atrLength, SuperTrendOscillator_OscType oscillatorType, int oscillatorSmooth)
		{
			return indicator.SuperTrendOscillator(input, multiplier, atrLength, oscillatorType, oscillatorSmooth);
		}
	}
}

#endregion
