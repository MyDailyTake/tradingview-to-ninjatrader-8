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

// NT8 Version of Nadaraya-Watson: Rational Quadratic Kernel (Non-Repainting) [jdehorty]
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the Mozilla Public License 2.0 (MPL 2.0).
// The original Pine Script™ code is by jdehorty and can be found at: https://www.tradingview.com/script/AWNvbPRM-Nadaraya-Watson-Rational-Quadratic-Kernel-Non-Repainting/
// Adaptation for NinjaTrader by jack@mydailytake.com
// Write-up + downloads: https://mydailytake.com/nadaraya-watson-rq-kernel-jdehorty-ninjatrader-8/
// Source + all conversions: https://github.com/MyDailyTake/tradingview-to-ninjatrader-8
// © 2026 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the Mozilla Public License 2.0. Full license details at https://mozilla.org/MPL/2.0/
// The use of jdehorty name or its adapted code in this work does not imply endorsement by the original authors.
//
// Single-line non-repainting Nadaraya-Watson kernel-regression estimator using the Rational Quadratic
// kernel. Two color modes:
//   • Rate of change — line tints by yhat1 slope (default).
//   • Smooth Colors  — runs a second kernel at (Lookback − Lag) and tints by the yhat2 ↔ yhat1
//     crossover, which produces fewer transitions on noisy data.
//
// Public Series outputs: NwLine, AlertStream (1 = bullish color change, −1 = bearish, 0 otherwise).
// 2 alertconditions stripped per house QC.

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	#region Categories
	[Gui.CategoryOrder("Kernel Settings",	10100)]
	[Gui.CategoryOrder("Colors",			10200)]
	#endregion

	public class NadarayaWatsonRqKernel : Indicator
	{
		#region indInfo

		private string indName        = "Nadaraya-Watson: Rational Quadratic Kernel (Non-Repainting) [jdehorty]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by jdehorty can be found here: https://www.tradingview.com/script/AWNvbPRM-Nadaraya-Watson-Rational-Quadratic-Kernel-Non-Repainting/";

		#endregion

		#region Properties

		[NinjaScriptProperty]
		[Range(3.0, 50.0)]
		[Display(Order = 1, GroupName = "Kernel Settings", Name = "Lookback Window (h)",
			Description = "Bandwidth of the Rational Quadratic kernel. Higher = smoother, lower = more reactive. Recommended 3-50.")]
		public double Lookback { get; set; }

		[NinjaScriptProperty]
		[Range(0.25, 25.0)]
		[Display(Order = 2, GroupName = "Kernel Settings", Name = "Relative Weighting (alpha)",
			Description = "Mix between long and short timeframes. Lower = longer timeframes dominate; higher = behaves like a Gaussian kernel. Recommended 0.25-25.")]
		public double Alpha { get; set; }

		[NinjaScriptProperty]
		[Range(0, 200)]
		[Display(Order = 3, GroupName = "Kernel Settings", Name = "Start Regression at Bar",
			Description = "Skip the first N bars when fitting. Recommended 5-25.")]
		public int StartBar { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 1, GroupName = "Colors", Name = "Smooth Colors",
			Description = "Use a crossover-based color mechanism for fewer transitions, instead of slope-based.")]
		public bool SmoothColors { get; set; }

		[NinjaScriptProperty]
		[Range(1, 10)]
		[Display(Order = 2, GroupName = "Colors", Name = "Lag",
			Description = "Lookback delta for the crossover detection kernel. Lower = earlier crossovers. Recommended 1-2.")]
		public int Lag { get; set; }

		[XmlIgnore]
		[Display(Order = 3, GroupName = "Colors", Name = "Bullish Color")]
		public Brush BullishColor { get; set; }
			[Browsable(false)]
			public string BullishColorSerialize
			{
				get { return Serialize.BrushToString(BullishColor); }
				set { BullishColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore]
		[Display(Order = 4, GroupName = "Colors", Name = "Bearish Color")]
		public Brush BearishColor { get; set; }
			[Browsable(false)]
			public string BearishColorSerialize
			{
				get { return Serialize.BrushToString(BearishColor); }
				set { BearishColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		#endregion

		#region Public outputs

		[Browsable(false)][XmlIgnore] public Series<double> NwLine      { get { return Values[0]; } }
		[Browsable(false)][XmlIgnore] public Series<double> NwLineLag   { get { Update(); return sLag; } }
		[Browsable(false)][XmlIgnore] public Series<double> AlertStream { get { Update(); return sAlert; } }

		#endregion

		#region Variables

		private double[] kernelWeightsMain;	// at Lookback
		private double[] kernelWeightsLag;	// at Lookback - Lag
		private int      kernelWidth;

		private Series<double> sLag;
		private Series<double> sAlert;

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

				Lookback	 = 8.0;
				Alpha		 = 8.0;
				StartBar	 = 25;
				SmoothColors = false;
				Lag			 = 2;
				BullishColor = new SolidColorBrush(Color.FromRgb(0x3A, 0xFF, 0x17));
				BearishColor = new SolidColorBrush(Color.FromRgb(0xFD, 0x17, 0x07));
				EnsureFrozen(BullishColor);
				EnsureFrozen(BearishColor);

				AddPlot(new Stroke(Brushes.LimeGreen, DashStyleHelper.Solid, 2f), PlotStyle.Line, "RQ Kernel");
			}
			else if (State == State.DataLoaded)
			{
				BuildKernelWeights();
				sLag   = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sAlert = new Series<double>(this, MaximumBarsLookBack.Infinite);
			}
		}

		#endregion

		#region OnBarUpdate

		protected override void OnBarUpdate()
		{
			if (CurrentBar < StartBar + 3) return;

			double yhat1 = NadarayaWatson(Input, kernelWeightsMain);
			double yhat2 = NadarayaWatson(Input, kernelWeightsLag);

			Values[0][0] = yhat1;
			sLag[0]      = yhat2;

			// Color flow.
			Brush col;
			if (SmoothColors)
			{
				bool bullSmooth = yhat2 > yhat1;
				col = bullSmooth ? BullishColor : BearishColor;
			}
			else
			{
				bool isBullish = CurrentBar > 0 && Values[0][1] < yhat1;
				col = isBullish ? BullishColor : BearishColor;
			}
			PlotBrushes[0][0] = col;

			// Alert stream — 1 on bull color change, -1 on bear color change, else 0.
			double alert = 0;
			if (CurrentBar > 1)
			{
				if (SmoothColors)
				{
					bool isBullishCross = yhat2 > yhat1 && sLag[1] <= Values[0][1];
					bool isBearishCross = yhat2 < yhat1 && sLag[1] >= Values[0][1];
					if (isBearishCross) alert = -1;
					else if (isBullishCross) alert = 1;
				}
				else
				{
					bool wasBearish = Values[0][2] > Values[0][1];
					bool wasBullish = Values[0][2] < Values[0][1];
					bool isBearish  = Values[0][1] > yhat1;
					bool isBullish  = Values[0][1] < yhat1;
					if (isBearish && wasBullish) alert = -1;
					else if (isBullish && wasBearish) alert = 1;
				}
			}
			sAlert[0] = alert;
		}

		#endregion

		#region Kernel

		private void BuildKernelWeights()
		{
			kernelWidth = Math.Max(50, Math.Min(500, (int)(Lookback * 12)));
			kernelWeightsMain = BuildWeights(Lookback);
			double lagH = Math.Max(1.0, Lookback - Lag);
			kernelWeightsLag = BuildWeights(lagH);
		}

		private double[] BuildWeights(double h)
		{
			double[] w = new double[kernelWidth];
			double denom = 2.0 * Alpha * h * h;
			for (int i = 0; i < kernelWidth; i++)
				w[i] = Math.Pow(1.0 + (i * (double)i) / denom, -Alpha);
			return w;
		}

		private double NadarayaWatson(ISeries<double> src, double[] weights)
		{
			int n = Math.Min(kernelWidth, CurrentBar - StartBar + 1);
			if (n <= 0) return src[0];
			double weighted = 0, cum = 0;
			for (int i = 0; i < n; i++)
			{
				double w = weights[i];
				weighted += src[i] * w;
				cum      += w;
			}
			return cum > 0 ? weighted / cum : src[0];
		}

		#endregion

		#region Helpers

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
		private indTradingView.NadarayaWatsonRqKernel[] cacheNadarayaWatsonRqKernel;
		public indTradingView.NadarayaWatsonRqKernel NadarayaWatsonRqKernel(double lookback, double alpha, int startBar, bool smoothColors, int lag)
		{
			return NadarayaWatsonRqKernel(Input, lookback, alpha, startBar, smoothColors, lag);
		}

		public indTradingView.NadarayaWatsonRqKernel NadarayaWatsonRqKernel(ISeries<double> input, double lookback, double alpha, int startBar, bool smoothColors, int lag)
		{
			if (cacheNadarayaWatsonRqKernel != null)
				for (int idx = 0; idx < cacheNadarayaWatsonRqKernel.Length; idx++)
					if (cacheNadarayaWatsonRqKernel[idx] != null && cacheNadarayaWatsonRqKernel[idx].Lookback == lookback && cacheNadarayaWatsonRqKernel[idx].Alpha == alpha && cacheNadarayaWatsonRqKernel[idx].StartBar == startBar && cacheNadarayaWatsonRqKernel[idx].SmoothColors == smoothColors && cacheNadarayaWatsonRqKernel[idx].Lag == lag && cacheNadarayaWatsonRqKernel[idx].EqualsInput(input))
						return cacheNadarayaWatsonRqKernel[idx];
			return CacheIndicator<indTradingView.NadarayaWatsonRqKernel>(new indTradingView.NadarayaWatsonRqKernel(){ Lookback = lookback, Alpha = alpha, StartBar = startBar, SmoothColors = smoothColors, Lag = lag }, input, ref cacheNadarayaWatsonRqKernel);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.NadarayaWatsonRqKernel NadarayaWatsonRqKernel(double lookback, double alpha, int startBar, bool smoothColors, int lag)
		{
			return indicator.NadarayaWatsonRqKernel(Input, lookback, alpha, startBar, smoothColors, lag);
		}

		public Indicators.indTradingView.NadarayaWatsonRqKernel NadarayaWatsonRqKernel(ISeries<double> input , double lookback, double alpha, int startBar, bool smoothColors, int lag)
		{
			return indicator.NadarayaWatsonRqKernel(input, lookback, alpha, startBar, smoothColors, lag);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.NadarayaWatsonRqKernel NadarayaWatsonRqKernel(double lookback, double alpha, int startBar, bool smoothColors, int lag)
		{
			return indicator.NadarayaWatsonRqKernel(Input, lookback, alpha, startBar, smoothColors, lag);
		}

		public Indicators.indTradingView.NadarayaWatsonRqKernel NadarayaWatsonRqKernel(ISeries<double> input , double lookback, double alpha, int startBar, bool smoothColors, int lag)
		{
			return indicator.NadarayaWatsonRqKernel(input, lookback, alpha, startBar, smoothColors, lag);
		}
	}
}

#endregion
