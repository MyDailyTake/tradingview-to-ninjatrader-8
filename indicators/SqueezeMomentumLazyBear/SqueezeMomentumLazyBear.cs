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

// NT8 Version of Squeeze Momentum Indicator
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the Mozilla Public License 2.0 (MPL 2.0).
// The original Pine Script™ code is by LazyBear and can be found at: https://www.tradingview.com/script/nqQ1DT5a-Squeeze-Momentum-Indicator-LazyBear/
// Adaptation for NinjaTrader by jack@mydailytake.com
// Write-up + downloads: https://mydailytake.com/squeeze-momentum-lazybear-ninjatrader-8/
// Source + all conversions: https://github.com/MyDailyTake/tradingview-to-ninjatrader-8
// © 2026 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the Mozilla Public License 2.0. Full license details at https://mozilla.org/MPL/2.0/
// The use of LazyBear name or its adapted code in this work does not imply endorsement by the original authors.
//
// Notes:
//   Derivative of John Carter's TTM Squeeze. Detects Bollinger-Band-inside-Keltner-Channel
//   compression (squeeze on), expansion past those bands (squeeze off / firing), and a
//   linear-regression momentum histogram colored by direction and rate-of-change.
//   Bollinger bands use the KC multiplier — preserved upstream behavior.
//   Non-repainting. Public Series outputs: Momentum, IsSqueezeOn, IsSqueezeOff.

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	#region Categories
	[Gui.CategoryOrder("Indicator Setup", 10100)]
	[Gui.CategoryOrder("Display",         10200)]
	#endregion

	public class SqueezeMomentumLazyBear : Indicator
	{
		#region indInfo

		private string indName        = "Squeeze Momentum [LazyBear]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by LazyBear can be found here: https://www.tradingview.com/script/nqQ1DT5a-Squeeze-Momentum-Indicator-LazyBear/";

		#endregion

		#region Properties

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 1, GroupName = "Indicator Setup", Name = "BB Length",
			Description = "Bollinger band period.")]
		public int BbLength { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 2, GroupName = "Indicator Setup", Name = "KC Length",
			Description = "Keltner channel period.")]
		public int KcLength { get; set; }

		[NinjaScriptProperty]
		[Range(0.1, 10.0)]
		[Display(Order = 3, GroupName = "Indicator Setup", Name = "KC Mult",
			Description = "Keltner channel multiplier (also drives the Bollinger band width — preserved upstream behavior).")]
		public double KcMult { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 4, GroupName = "Indicator Setup", Name = "Use True Range (KC)",
			Description = "Use True Range instead of High − Low for the Keltner channel width.")]
		public bool UseTrueRange { get; set; }

		[XmlIgnore]
		[Display(Order = 1, GroupName = "Display", Name = "Momentum Up Rising",
			Description = "Histogram color when momentum is positive and rising.")]
		public Brush MomUpRisingColor { get; set; }
		[Browsable(false)]
		public string MomUpRisingColorSerialize
		{
			get { return Serialize.BrushToString(MomUpRisingColor); }
			set { MomUpRisingColor = EnsureFrozen(Serialize.StringToBrush(value)); }
		}

		[XmlIgnore]
		[Display(Order = 2, GroupName = "Display", Name = "Momentum Up Falling",
			Description = "Histogram color when momentum is positive and falling.")]
		public Brush MomUpFallingColor { get; set; }
		[Browsable(false)]
		public string MomUpFallingColorSerialize
		{
			get { return Serialize.BrushToString(MomUpFallingColor); }
			set { MomUpFallingColor = EnsureFrozen(Serialize.StringToBrush(value)); }
		}

		[XmlIgnore]
		[Display(Order = 3, GroupName = "Display", Name = "Momentum Down Falling",
			Description = "Histogram color when momentum is negative and falling.")]
		public Brush MomDnFallingColor { get; set; }
		[Browsable(false)]
		public string MomDnFallingColorSerialize
		{
			get { return Serialize.BrushToString(MomDnFallingColor); }
			set { MomDnFallingColor = EnsureFrozen(Serialize.StringToBrush(value)); }
		}

		[XmlIgnore]
		[Display(Order = 4, GroupName = "Display", Name = "Momentum Down Rising",
			Description = "Histogram color when momentum is negative but recovering.")]
		public Brush MomDnRisingColor { get; set; }
		[Browsable(false)]
		public string MomDnRisingColorSerialize
		{
			get { return Serialize.BrushToString(MomDnRisingColor); }
			set { MomDnRisingColor = EnsureFrozen(Serialize.StringToBrush(value)); }
		}

		[XmlIgnore]
		[Display(Order = 5, GroupName = "Display", Name = "No Squeeze",
			Description = "Zero-line marker color when neither squeeze on nor squeeze off.")]
		public Brush NoSqueezeColor { get; set; }
		[Browsable(false)]
		public string NoSqueezeColorSerialize
		{
			get { return Serialize.BrushToString(NoSqueezeColor); }
			set { NoSqueezeColor = EnsureFrozen(Serialize.StringToBrush(value)); }
		}

		[XmlIgnore]
		[Display(Order = 6, GroupName = "Display", Name = "Squeeze On",
			Description = "Zero-line marker color when Bollinger bands are inside Keltner channels (compression).")]
		public Brush SqueezeOnColor { get; set; }
		[Browsable(false)]
		public string SqueezeOnColorSerialize
		{
			get { return Serialize.BrushToString(SqueezeOnColor); }
			set { SqueezeOnColor = EnsureFrozen(Serialize.StringToBrush(value)); }
		}

		[XmlIgnore]
		[Display(Order = 7, GroupName = "Display", Name = "Squeeze Off",
			Description = "Zero-line marker color when Bollinger bands are outside Keltner channels (released).")]
		public Brush SqueezeOffColor { get; set; }
		[Browsable(false)]
		public string SqueezeOffColorSerialize
		{
			get { return Serialize.BrushToString(SqueezeOffColor); }
			set { SqueezeOffColor = EnsureFrozen(Serialize.StringToBrush(value)); }
		}

		#endregion

		#region Variables

		private Series<double>	linRegInput;
		private Series<double>	rangeSeries;
		private Series<bool>	sIsSqueezeOn;
		private Series<bool>	sIsSqueezeOff;

		private MAX				maxHighKc;
		private MIN				minLowKc;
		private SMA				smaCloseKc;
		private LinReg			linRegMom;
		private SMA				smaCloseBb;
		private StdDev			stdDevCloseBb;
		private SMA				smaRangeKc;

		#endregion

		#region OnStateChange

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description                  = indDescription;
				Name                         = indName;
				Calculate                    = Calculate.OnBarClose;
				IsOverlay                    = false;
				DisplayInDataBox             = true;
				DrawOnPricePanel             = false;
				DrawHorizontalGridLines      = true;
				DrawVerticalGridLines        = true;
				PaintPriceMarkers            = true;
				ScaleJustification           = NinjaTrader.Gui.Chart.ScaleJustification.Right;
				IsSuspendedWhileInactive     = true;

				BbLength      = 20;
				KcLength      = 20;
				KcMult        = 1.5;
				UseTrueRange  = true;

				MomUpRisingColor  = Brushes.Lime;
				MomUpFallingColor = Brushes.Green;
				MomDnFallingColor = Brushes.Red;
				MomDnRisingColor  = Brushes.Maroon;
				NoSqueezeColor    = Brushes.DodgerBlue;
				SqueezeOnColor    = Brushes.Black;
				SqueezeOffColor   = Brushes.Gray;

				AddPlot(new Stroke(Brushes.Lime,       DashStyleHelper.Solid, 4f), PlotStyle.Bar,   "Momentum");
				AddPlot(new Stroke(Brushes.DodgerBlue, DashStyleHelper.Solid, 2f), PlotStyle.Cross, "Squeeze State");
			}
			else if (State == State.DataLoaded)
			{
				// LinReg / SMA wrap these with KcLength (user-configurable, can exceed 256) — needs Infinite.
				linRegInput		= new Series<double>(this, MaximumBarsLookBack.Infinite);
				rangeSeries		= new Series<double>(this, MaximumBarsLookBack.Infinite);
				sIsSqueezeOn	= new Series<bool>(this);
				sIsSqueezeOff	= new Series<bool>(this);

				maxHighKc		= MAX(High, KcLength);
				minLowKc		= MIN(Low,  KcLength);
				smaCloseKc		= SMA(Close, KcLength);
				linRegMom		= LinReg(linRegInput, KcLength);
				smaCloseBb		= SMA(Close, BbLength);
				stdDevCloseBb	= StdDev(Close, BbLength);
				smaRangeKc		= SMA(rangeSeries, KcLength);
			}
		}

		#endregion

		protected override void OnBarUpdate()
		{
			int warmup = Math.Max(BbLength, KcLength);
			if (CurrentBar < warmup) return;

			// Linear-regression source = Close − ((HH+LL)/2 + SMA(close)) / 2
			double midpointA = (maxHighKc[0] + minLowKc[0]) / 2.0;
			double midpointB = smaCloseKc[0];
			double midpoint  = (midpointA + midpointB) / 2.0;
			linRegInput[0]   = Close[0] - midpoint;

			double val   = linRegMom[0];
			Values[0][0] = val;

			// Histogram color — 4 states based on val sign and direction.
			double prevVal = 0.0;
			if (CurrentBar > warmup)
			{
				double pv = Values[0][1];
				if (!double.IsNaN(pv)) prevVal = pv;
			}

			Brush histColor;
			if (val > 0)
				histColor = val > prevVal ? MomUpRisingColor : MomUpFallingColor;
			else
				histColor = val < prevVal ? MomDnFallingColor : MomDnRisingColor;
			PlotBrushes[0][0] = histColor;

			// Bollinger / Keltner squeeze state. BB width uses the KC multiplier (preserved source quirk).
			double basis     = smaCloseBb[0];
			double dev       = KcMult * stdDevCloseBb[0];
			double upperBB   = basis + dev;
			double lowerBB   = basis - dev;

			double tr;
			if (CurrentBar == 0)
				tr = High[0] - Low[0];
			else
			{
				double a = High[0] - Low[0];
				double b = Math.Abs(High[0] - Close[1]);
				double c = Math.Abs(Low[0]  - Close[1]);
				tr = Math.Max(a, Math.Max(b, c));
			}
			rangeSeries[0] = UseTrueRange ? tr : (High[0] - Low[0]);

			double ma       = smaCloseKc[0];
			double rangema  = smaRangeKc[0];
			double upperKC  = ma + rangema * KcMult;
			double lowerKC  = ma - rangema * KcMult;

			bool sqzOn      = lowerBB > lowerKC && upperBB < upperKC;
			bool sqzOff     = lowerBB < lowerKC && upperBB > upperKC;

			sIsSqueezeOn[0]  = sqzOn;
			sIsSqueezeOff[0] = sqzOff;

			Values[1][0]      = 0.0;
			PlotBrushes[1][0] = !sqzOn && !sqzOff ? NoSqueezeColor : sqzOn ? SqueezeOnColor : SqueezeOffColor;
		}

		[Browsable(false)][XmlIgnore] public Series<double> Momentum      { get { return Values[0]; } }
		[Browsable(false)][XmlIgnore] public Series<bool>   IsSqueezeOn   { get { Update(); return sIsSqueezeOn;  } }
		[Browsable(false)][XmlIgnore] public Series<bool>   IsSqueezeOff  { get { Update(); return sIsSqueezeOff; } }

		private static Brush EnsureFrozen(Brush b)
		{
			if (b != null && b.CanFreeze && !b.IsFrozen) b.Freeze();
			return b;
		}
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private indTradingView.SqueezeMomentumLazyBear[] cacheSqueezeMomentumLazyBear;
		public indTradingView.SqueezeMomentumLazyBear SqueezeMomentumLazyBear(int bbLength, int kcLength, double kcMult, bool useTrueRange)
		{
			return SqueezeMomentumLazyBear(Input, bbLength, kcLength, kcMult, useTrueRange);
		}

		public indTradingView.SqueezeMomentumLazyBear SqueezeMomentumLazyBear(ISeries<double> input, int bbLength, int kcLength, double kcMult, bool useTrueRange)
		{
			if (cacheSqueezeMomentumLazyBear != null)
				for (int idx = 0; idx < cacheSqueezeMomentumLazyBear.Length; idx++)
					if (cacheSqueezeMomentumLazyBear[idx] != null && cacheSqueezeMomentumLazyBear[idx].BbLength == bbLength && cacheSqueezeMomentumLazyBear[idx].KcLength == kcLength && cacheSqueezeMomentumLazyBear[idx].KcMult == kcMult && cacheSqueezeMomentumLazyBear[idx].UseTrueRange == useTrueRange && cacheSqueezeMomentumLazyBear[idx].EqualsInput(input))
						return cacheSqueezeMomentumLazyBear[idx];
			return CacheIndicator<indTradingView.SqueezeMomentumLazyBear>(new indTradingView.SqueezeMomentumLazyBear(){ BbLength = bbLength, KcLength = kcLength, KcMult = kcMult, UseTrueRange = useTrueRange }, input, ref cacheSqueezeMomentumLazyBear);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.SqueezeMomentumLazyBear SqueezeMomentumLazyBear(int bbLength, int kcLength, double kcMult, bool useTrueRange)
		{
			return indicator.SqueezeMomentumLazyBear(Input, bbLength, kcLength, kcMult, useTrueRange);
		}

		public Indicators.indTradingView.SqueezeMomentumLazyBear SqueezeMomentumLazyBear(ISeries<double> input , int bbLength, int kcLength, double kcMult, bool useTrueRange)
		{
			return indicator.SqueezeMomentumLazyBear(input, bbLength, kcLength, kcMult, useTrueRange);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.SqueezeMomentumLazyBear SqueezeMomentumLazyBear(int bbLength, int kcLength, double kcMult, bool useTrueRange)
		{
			return indicator.SqueezeMomentumLazyBear(Input, bbLength, kcLength, kcMult, useTrueRange);
		}

		public Indicators.indTradingView.SqueezeMomentumLazyBear SqueezeMomentumLazyBear(ISeries<double> input , int bbLength, int kcLength, double kcMult, bool useTrueRange)
		{
			return indicator.SqueezeMomentumLazyBear(input, bbLength, kcLength, kcMult, useTrueRange);
		}
	}
}

#endregion
