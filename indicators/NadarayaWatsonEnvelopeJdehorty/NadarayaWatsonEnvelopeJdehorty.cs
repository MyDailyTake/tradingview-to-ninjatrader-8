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

// NT8 Version of Nadaraya-Watson: Envelope (Non-Repainting) [jdehorty]
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the Mozilla Public License 2.0 (MPL 2.0).
// The original Pine Script™ code is by jdehorty and can be found at: https://www.tradingview.com/script/WeLssFxl-Nadaraya-Watson-Envelope-Non-Repainting/
// Adaptation for NinjaTrader by jack@mydailytake.com
// Write-up + downloads: https://mydailytake.com/nadaraya-watson-envelope-jdehorty-ninjatrader-8/
// Source + all conversions: https://github.com/MyDailyTake/tradingview-to-ninjatrader-8
// © 2026 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the Mozilla Public License 2.0. Full license details at https://mozilla.org/MPL/2.0/
// The use of jdehorty name or its adapted code in this work does not imply endorsement by the original authors.
//
// Non-repainting Nadaraya-Watson kernel-regression estimator with an ATR-scaled envelope. The Rational
// Quadratic kernel weights nearby bars more heavily and is applied independently to close, high, and low
// to derive a smoothed price (yhat) and a kernel-true-range used for ATR. NearFactor / FarFactor define
// two pairs of upper / lower boundaries, producing a graduated envelope around the estimator. The
// estimator line is tinted bullish when its slope is rising and bearish when falling.
//
// Public Series outputs: NwEstimator, UpperFar, UpperNear, LowerNear, LowerFar.

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	#region Categories
	[Gui.CategoryOrder("Kernel Settings",	10100)]
	[Gui.CategoryOrder("Envelope",			10200)]
	[Gui.CategoryOrder("Colors",			10300)]
	#endregion

	public class NadarayaWatsonEnvelopeJdehorty : Indicator
	{
		#region indInfo

		private string indName        = "Nadaraya-Watson: Envelope (Non-Repainting) [jdehorty]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by jdehorty can be found here: https://www.tradingview.com/script/WeLssFxl-Nadaraya-Watson-Envelope-Non-Repainting/";

		#endregion

		#region Properties

		[NinjaScriptProperty]
		[Range(2, 100)]
		[Display(Order = 1, GroupName = "Kernel Settings", Name = "Lookback Window (h)",
			Description = "Bandwidth of the Rational Quadratic kernel. Higher = smoother, lower = more reactive. Recommended 3-50.")]
		public int Lookback { get; set; }

		[NinjaScriptProperty]
		[Range(0.25, 25.0)]
		[Display(Order = 2, GroupName = "Kernel Settings", Name = "Relative Weighting (alpha)",
			Description = "Mix between long and short timeframes. Lower = longer timeframes dominate; higher = behaves like a Gaussian kernel. Recommended 0.25-25.")]
		public double Alpha { get; set; }

		[NinjaScriptProperty]
		[Range(0, 200)]
		[Display(Order = 3, GroupName = "Kernel Settings", Name = "Start Regression at Bar",
			Description = "Skip the first N bars when fitting. The earliest bars on a chart are noisy; omitting them often improves the fit. Recommended 5-25.")]
		public int StartBar { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 1, GroupName = "Envelope", Name = "ATR Length",
			Description = "Period for the kernel-true-range RMA used to scale the envelope.")]
		public int AtrLength { get; set; }

		[NinjaScriptProperty]
		[Range(0.5, 5.0)]
		[Display(Order = 2, GroupName = "Envelope", Name = "Near ATR Factor",
			Description = "Multiplier on the kernel-ATR for the near (inner) envelope boundary. Recommended 0.5-2.0.")]
		public double NearFactor { get; set; }

		[NinjaScriptProperty]
		[Range(1.0, 20.0)]
		[Display(Order = 3, GroupName = "Envelope", Name = "Far ATR Factor",
			Description = "Multiplier on the kernel-ATR for the far (outer) envelope boundary. Recommended 6.0-8.0.")]
		public double FarFactor { get; set; }

		[XmlIgnore]
		[Display(Order = 1, GroupName = "Colors", Name = "Bullish Estimator Color",
			Description = "Color of the Nadaraya-Watson estimator line when its slope is rising.")]
		public Brush BullishColor { get; set; }
			[Browsable(false)]
			public string BullishColorSerialize
			{
				get { return Serialize.BrushToString(BullishColor); }
				set { BullishColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore]
		[Display(Order = 2, GroupName = "Colors", Name = "Bearish Estimator Color",
			Description = "Color of the Nadaraya-Watson estimator line when its slope is falling.")]
		public Brush BearishColor { get; set; }
			[Browsable(false)]
			public string BearishColorSerialize
			{
				get { return Serialize.BrushToString(BearishColor); }
				set { BearishColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore]
		[Display(Order = 3, GroupName = "Colors", Name = "Upper Boundary Color",
			Description = "Color used for the upper envelope boundaries and shaded region.")]
		public Brush UpperColor { get; set; }
			[Browsable(false)]
			public string UpperColorSerialize
			{
				get { return Serialize.BrushToString(UpperColor); }
				set { UpperColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore]
		[Display(Order = 4, GroupName = "Colors", Name = "Lower Boundary Color",
			Description = "Color used for the lower envelope boundaries and shaded region.")]
		public Brush LowerColor { get; set; }
			[Browsable(false)]
			public string LowerColorSerialize
			{
				get { return Serialize.BrushToString(LowerColor); }
				set { LowerColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		#endregion

		#region Public outputs

		[Browsable(false)][XmlIgnore] public Series<double> NwEstimator { get { return Values[0]; } }
		[Browsable(false)][XmlIgnore] public Series<double> UpperFar    { get { return Values[1]; } }
		[Browsable(false)][XmlIgnore] public Series<double> UpperAvg    { get { return Values[2]; } }
		[Browsable(false)][XmlIgnore] public Series<double> UpperNear   { get { return Values[3]; } }
		[Browsable(false)][XmlIgnore] public Series<double> LowerNear   { get { return Values[4]; } }
		[Browsable(false)][XmlIgnore] public Series<double> LowerAvg    { get { return Values[5]; } }
		[Browsable(false)][XmlIgnore] public Series<double> LowerFar    { get { return Values[6]; } }

		#endregion

		#region Variables

		// Pre-computed Rational-Quadratic weights — the kernel decays fast, so a fixed window is enough.
		private double[] kernelWeights;
		private int      kernelWidth;

		// Manual RMA on the kernel-derived true range.
		private double rmaKtr = double.NaN;

		// 4 fill brushes — matches Pine's split (avg → far at 40% alpha, avg → near at 20% alpha)
		private SharpDX.Direct2D1.SolidColorBrush dxUpperFarFill;
		private SharpDX.Direct2D1.SolidColorBrush dxUpperNearFill;
		private SharpDX.Direct2D1.SolidColorBrush dxLowerNearFill;
		private SharpDX.Direct2D1.SolidColorBrush dxLowerFarFill;

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

				Lookback	= 8;
				Alpha		= 8.0;
				StartBar	= 25;
				AtrLength	= 60;
				NearFactor	= 1.5;
				FarFactor	= 8.0;
				BullishColor= new SolidColorBrush(Color.FromArgb(128, 0x00, 0x80, 0x00));
				BearishColor= new SolidColorBrush(Color.FromArgb(128, 0xFF, 0x00, 0x00));
				UpperColor	= new SolidColorBrush(Color.FromArgb(102, 0xFF, 0x00, 0x00));
				LowerColor	= new SolidColorBrush(Color.FromArgb(102, 0x00, 0x80, 0x00));
				EnsureFrozen(BullishColor); EnsureFrozen(BearishColor);
				EnsureFrozen(UpperColor); EnsureFrozen(LowerColor);

				AddPlot(new Stroke(Brushes.DodgerBlue, DashStyleHelper.Solid, 2f), PlotStyle.Line, "NW Estimator");
				AddPlot(new Stroke(Brushes.IndianRed,  DashStyleHelper.Solid, 1f), PlotStyle.Line, "Upper Far");
				AddPlot(new Stroke(Brushes.IndianRed,  DashStyleHelper.Solid, 1f), PlotStyle.Line, "Upper Avg");
				AddPlot(new Stroke(Brushes.IndianRed,  DashStyleHelper.Solid, 1f), PlotStyle.Line, "Upper Near");
				AddPlot(new Stroke(Brushes.SeaGreen,   DashStyleHelper.Solid, 1f), PlotStyle.Line, "Lower Near");
				AddPlot(new Stroke(Brushes.SeaGreen,   DashStyleHelper.Solid, 1f), PlotStyle.Line, "Lower Avg");
				AddPlot(new Stroke(Brushes.SeaGreen,   DashStyleHelper.Solid, 1f), PlotStyle.Line, "Lower Far");
			}
			else if (State == State.DataLoaded)
			{
				BuildKernelWeights();
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
			if (CurrentBar < StartBar + 1) return;

			double yhatClose = NadarayaWatson(Close);
			double yhatHigh  = NadarayaWatson(High);
			double yhatLow   = NadarayaWatson(Low);

			// Kernel-true-range — TR computed from smoothed yhat values, then RMA-smoothed.
			double trCur;
			if (CurrentBar == StartBar + 1)
			{
				trCur = yhatHigh - yhatLow;
			}
			else
			{
				double prevClose = Values[0][1];
				trCur = Math.Max(yhatHigh - yhatLow,
						 Math.Max(Math.Abs(yhatHigh - prevClose), Math.Abs(yhatLow - prevClose)));
			}
			double a = 1.0 / Math.Max(1, AtrLength);
			rmaKtr = double.IsNaN(rmaKtr) ? trCur : (1.0 - a) * rmaKtr + a * trCur;

			double ktr      = rmaKtr;
			double upperNear= yhatClose + NearFactor * ktr;
			double upperFar = yhatClose + FarFactor  * ktr;
			double lowerNear= yhatClose - NearFactor * ktr;
			double lowerFar = yhatClose - FarFactor  * ktr;
			double upperAvg = (upperFar + upperNear) * 0.5;
			double lowerAvg = (lowerFar + lowerNear) * 0.5;

			Values[0][0] = yhatClose;
			Values[1][0] = upperFar;
			Values[2][0] = upperAvg;
			Values[3][0] = upperNear;
			Values[4][0] = lowerNear;
			Values[5][0] = lowerAvg;
			Values[6][0] = lowerFar;

			bool rising = CurrentBar > 0 && yhatClose > Values[0][1];
			PlotBrushes[0][0] = rising ? BullishColor : BearishColor;
		}

		#endregion

		#region Kernel

		private void BuildKernelWeights()
		{
			kernelWidth = Math.Max(50, Math.Min(500, Lookback * 12));
			kernelWeights = new double[kernelWidth];
			double denom = 2.0 * Alpha * Lookback * Lookback;
			for (int i = 0; i < kernelWidth; i++)
				kernelWeights[i] = Math.Pow(1.0 + (i * (double)i) / denom, -Alpha);
		}

		private double NadarayaWatson(ISeries<double> src)
		{
			if (kernelWeights == null) BuildKernelWeights();
			int n = Math.Min(kernelWidth, CurrentBar - StartBar + 1);
			if (n <= 0) return src[0];

			double weighted = 0;
			double cum = 0;
			for (int i = 0; i < n; i++)
			{
				double w = kernelWeights[i];
				weighted += src[i] * w;
				cum      += w;
			}
			return cum > 0 ? weighted / cum : src[0];
		}

		#endregion

		#region SharpDX envelope shading

		public override void OnRenderTargetChanged()
		{
			try
			{
				ReleaseRenderResources();
				if (RenderTarget == null) return;

				Color upC = (UpperColor as SolidColorBrush)?.Color ?? Colors.Red;
				Color loC = (LowerColor as SolidColorBrush)?.Color ?? Colors.Green;

				// Pine: red_far = color.new(red, 60) → 40% opaque; red_near = color.new(red, 80) → 20% opaque (mirror for green)
				dxUpperFarFill  = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, ToColor4(upC, 0.40f));
				dxUpperNearFill = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, ToColor4(upC, 0.20f));
				dxLowerNearFill = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, ToColor4(loC, 0.20f));
				dxLowerFarFill  = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, ToColor4(loC, 0.40f));
			}
			catch { /* render target torn down mid-rebuild */ }
		}

		private void ReleaseRenderResources()
		{
			void D(ref SharpDX.Direct2D1.SolidColorBrush bx) { if (bx != null) { bx.Dispose(); bx = null; } }
			D(ref dxUpperFarFill); D(ref dxUpperNearFill); D(ref dxLowerNearFill); D(ref dxLowerFarFill);
		}

		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			base.OnRender(chartControl, chartScale);
			if (RenderTarget == null) return;
			if (!IsVisible || IsInHitTest) return;
			if (ChartBars == null) return;
			if (dxUpperFarFill == null) return;

			int primaryLast = CurrentBars[0];
			int chartLast   = ChartBars.ToIndex;
			int fromIdx     = Math.Max(ChartBars.FromIndex, 0);
			int toIdx       = Math.Min(chartLast, primaryLast);

			for (int j = fromIdx; j < toIdx; j++)
			{
				if (!Values[1].IsValidDataPointAt(j) || !Values[1].IsValidDataPointAt(j + 1)) continue;
				if (!Values[2].IsValidDataPointAt(j) || !Values[3].IsValidDataPointAt(j)) continue;
				if (!Values[4].IsValidDataPointAt(j) || !Values[5].IsValidDataPointAt(j) || !Values[6].IsValidDataPointAt(j)) continue;

				double upFarL  = Values[1].GetValueAt(j);     double upFarR  = Values[1].GetValueAt(j + 1);
				double upAvgL  = Values[2].GetValueAt(j);     double upAvgR  = Values[2].GetValueAt(j + 1);
				double upNearL = Values[3].GetValueAt(j);     double upNearR = Values[3].GetValueAt(j + 1);
				double loNearL = Values[4].GetValueAt(j);     double loNearR = Values[4].GetValueAt(j + 1);
				double loAvgL  = Values[5].GetValueAt(j);     double loAvgR  = Values[5].GetValueAt(j + 1);
				double loFarL  = Values[6].GetValueAt(j);     double loFarR  = Values[6].GetValueAt(j + 1);

				// Upper: 2 stacked bands separated by avg — outer (avg→far) is darker, inner (near→avg) is lighter
				DrawTrapezoid(chartControl, chartScale, j, upFarL,  upFarR,  upAvgL,  upAvgR,  dxUpperFarFill);
				DrawTrapezoid(chartControl, chartScale, j, upAvgL,  upAvgR,  upNearL, upNearR, dxUpperNearFill);
				// Lower: mirrored — inner (avg→near) lighter, outer (far→avg) darker
				DrawTrapezoid(chartControl, chartScale, j, loNearL, loNearR, loAvgL,  loAvgR,  dxLowerNearFill);
				DrawTrapezoid(chartControl, chartScale, j, loAvgL,  loAvgR,  loFarL,  loFarR,  dxLowerFarFill);
			}
		}

		private void DrawTrapezoid(ChartControl cc, ChartScale cs, int barLeftIdx,
			double topL, double topR, double botL, double botR, SharpDX.Direct2D1.Brush brush)
		{
			float xL = cc.GetXByBarIndex(ChartBars, barLeftIdx);
			float xR = cc.GetXByBarIndex(ChartBars, barLeftIdx + 1);
			if (xR <= xL) return;
			float yTL = (float)cs.GetYByValue(topL);
			float yTR = (float)cs.GetYByValue(topR);
			float yBL = (float)cs.GetYByValue(botL);
			float yBR = (float)cs.GetYByValue(botR);

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

		private static SharpDX.Color4 ToColor4(Color c, float alpha)
		{
			return new SharpDX.Color4(c.R / 255f, c.G / 255f, c.B / 255f, alpha);
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
		private indTradingView.NadarayaWatsonEnvelopeJdehorty[] cacheNadarayaWatsonEnvelopeJdehorty;
		public indTradingView.NadarayaWatsonEnvelopeJdehorty NadarayaWatsonEnvelopeJdehorty(int lookback, double alpha, int startBar, int atrLength, double nearFactor, double farFactor)
		{
			return NadarayaWatsonEnvelopeJdehorty(Input, lookback, alpha, startBar, atrLength, nearFactor, farFactor);
		}

		public indTradingView.NadarayaWatsonEnvelopeJdehorty NadarayaWatsonEnvelopeJdehorty(ISeries<double> input, int lookback, double alpha, int startBar, int atrLength, double nearFactor, double farFactor)
		{
			if (cacheNadarayaWatsonEnvelopeJdehorty != null)
				for (int idx = 0; idx < cacheNadarayaWatsonEnvelopeJdehorty.Length; idx++)
					if (cacheNadarayaWatsonEnvelopeJdehorty[idx] != null && cacheNadarayaWatsonEnvelopeJdehorty[idx].Lookback == lookback && cacheNadarayaWatsonEnvelopeJdehorty[idx].Alpha == alpha && cacheNadarayaWatsonEnvelopeJdehorty[idx].StartBar == startBar && cacheNadarayaWatsonEnvelopeJdehorty[idx].AtrLength == atrLength && cacheNadarayaWatsonEnvelopeJdehorty[idx].NearFactor == nearFactor && cacheNadarayaWatsonEnvelopeJdehorty[idx].FarFactor == farFactor && cacheNadarayaWatsonEnvelopeJdehorty[idx].EqualsInput(input))
						return cacheNadarayaWatsonEnvelopeJdehorty[idx];
			return CacheIndicator<indTradingView.NadarayaWatsonEnvelopeJdehorty>(new indTradingView.NadarayaWatsonEnvelopeJdehorty(){ Lookback = lookback, Alpha = alpha, StartBar = startBar, AtrLength = atrLength, NearFactor = nearFactor, FarFactor = farFactor }, input, ref cacheNadarayaWatsonEnvelopeJdehorty);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.NadarayaWatsonEnvelopeJdehorty NadarayaWatsonEnvelopeJdehorty(int lookback, double alpha, int startBar, int atrLength, double nearFactor, double farFactor)
		{
			return indicator.NadarayaWatsonEnvelopeJdehorty(Input, lookback, alpha, startBar, atrLength, nearFactor, farFactor);
		}

		public Indicators.indTradingView.NadarayaWatsonEnvelopeJdehorty NadarayaWatsonEnvelopeJdehorty(ISeries<double> input , int lookback, double alpha, int startBar, int atrLength, double nearFactor, double farFactor)
		{
			return indicator.NadarayaWatsonEnvelopeJdehorty(input, lookback, alpha, startBar, atrLength, nearFactor, farFactor);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.NadarayaWatsonEnvelopeJdehorty NadarayaWatsonEnvelopeJdehorty(int lookback, double alpha, int startBar, int atrLength, double nearFactor, double farFactor)
		{
			return indicator.NadarayaWatsonEnvelopeJdehorty(Input, lookback, alpha, startBar, atrLength, nearFactor, farFactor);
		}

		public Indicators.indTradingView.NadarayaWatsonEnvelopeJdehorty NadarayaWatsonEnvelopeJdehorty(ISeries<double> input , int lookback, double alpha, int startBar, int atrLength, double nearFactor, double farFactor)
		{
			return indicator.NadarayaWatsonEnvelopeJdehorty(input, lookback, alpha, startBar, atrLength, nearFactor, farFactor);
		}
	}
}

#endregion
