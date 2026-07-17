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

// NT8 Version of CM_Williams_Vix_Fix — Finds Market Bottoms
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the Mozilla Public License 2.0 (MPL 2.0).
// The original Pine Script™ code is by ChrisMoody and can be found at: https://www.tradingview.com/script/og7JPrRA-CM-Williams-Vix-Fix-Finds-Market-Bottoms/
// Adaptation for NinjaTrader by jack@mydailytake.com
// Write-up + downloads: https://mydailytake.com/cm-williams-vix-fix-chrismoody-ninjatrader-8/
// Source + all conversions: https://github.com/MyDailyTake/tradingview-to-ninjatrader-8
// © 2026 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the Mozilla Public License 2.0. Full license details at https://mozilla.org/MPL/2.0/
// The use of ChrisMoody name or its adapted code in this work does not imply endorsement by the original authors.
//
// Notes:
//   Larry Williams' Vix Fix — synthetic VIX-style bottom finder. Spikes when fear is elevated.
//   Histogram highlights bars where WVF crosses upperBand or rangeHigh threshold (potential bottom).
//   Optional bands: range-high/low percentile envelope, Bollinger upper-band of WVF.
//   Non-repainting. Public Series outputs: RangeHigh, RangeLow, WilliamsVixFix, UpperBand.

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	#region Categories
	[Gui.CategoryOrder("Indicator Setup", 10100)]
	[Gui.CategoryOrder("Display",         10200)]
	#endregion

	public class CmWilliamsVixFix : Indicator
	{
		#region indInfo

		private string indName        = "Williams Vix Fix [ChrisMoody]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by ChrisMoody can be found here: https://www.tradingview.com/script/og7JPrRA-CM-Williams-Vix-Fix-Finds-Market-Bottoms/";

		#endregion

		#region Properties

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 1, GroupName = "Indicator Setup", Name = "LookBack Std Dev",
			Description = "LookBack period for the highest-close component of the Williams Vix Fix.")]
		public int PdLookBack { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 2, GroupName = "Indicator Setup", Name = "Bollinger Length",
			Description = "Length used to compute the Bollinger upper band of the Williams Vix Fix.")]
		public int BbLength { get; set; }

		[NinjaScriptProperty]
		[Range(1.0, 5.0)]
		[Display(Order = 3, GroupName = "Indicator Setup", Name = "BB Multiplier",
			Description = "Standard-deviation multiplier for the upper Bollinger band.")]
		public double Mult { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 4, GroupName = "Indicator Setup", Name = "LookBack Percentile",
			Description = "LookBack period for the high/low percentile envelope.")]
		public int LbPercentile { get; set; }

		[NinjaScriptProperty]
		[Range(0.01, 1.0)]
		[Display(Order = 5, GroupName = "Indicator Setup", Name = "High Percentile",
			Description = "0.85 = 85%, 0.90 = 90%, 0.95 = 95%, 0.99 = 99%.")]
		public double PhPercentile { get; set; }

		[NinjaScriptProperty]
		[Range(1.0, 2.0)]
		[Display(Order = 6, GroupName = "Indicator Setup", Name = "Low Percentile",
			Description = "1.10 = 90%, 1.05 = 95%, 1.01 = 99%.")]
		public double PlPercentile { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 7, GroupName = "Indicator Setup", Name = "Show High Range",
			Description = "Plot the high/low percentile envelope around the WVF.")]
		public bool ShowHighRange { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 8, GroupName = "Indicator Setup", Name = "Show Std Dev Line",
			Description = "Plot the upper Bollinger band of the WVF.")]
		public bool ShowStdDev { get; set; }

		[XmlIgnore]
		[Display(Order = 1, GroupName = "Display", Name = "Highlight Color",
			Description = "Histogram color when WVF crosses the upper band or range high.")]
		public Brush HighlightColor { get; set; }
		[Browsable(false)]
		public string HighlightColorSerialize
		{
			get { return Serialize.BrushToString(HighlightColor); }
			set { HighlightColor = EnsureFrozen(Serialize.StringToBrush(value)); }
		}

		[XmlIgnore]
		[Display(Order = 2, GroupName = "Display", Name = "Normal Color",
			Description = "Histogram color when WVF is below the highlight threshold.")]
		public Brush NormalColor { get; set; }
		[Browsable(false)]
		public string NormalColorSerialize
		{
			get { return Serialize.BrushToString(NormalColor); }
			set { NormalColor = EnsureFrozen(Serialize.StringToBrush(value)); }
		}

		#endregion

		#region Variables

		private Series<double>	wvfSeries;
		private MAX				maxClosePd;
		private SMA				smaWvfBb;
		private StdDev			stdDevWvfBb;
		private MAX				maxWvfLb;
		private MIN				minWvfLb;

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

				PdLookBack    = 22;
				BbLength      = 20;
				Mult          = 2.0;
				LbPercentile  = 50;
				PhPercentile  = 0.85;
				PlPercentile  = 1.01;
				ShowHighRange = false;
				ShowStdDev    = false;

				HighlightColor = Brushes.Lime;
				NormalColor    = Brushes.Gray;

				AddPlot(new Stroke(Brushes.Orange, DashStyleHelper.Solid, 4f), PlotStyle.Line, "Range High");
				AddPlot(new Stroke(Brushes.Orange, DashStyleHelper.Solid, 4f), PlotStyle.Line, "Range Low");
				AddPlot(new Stroke(Brushes.Gray,   DashStyleHelper.Solid, 4f), PlotStyle.Bar,  "Williams Vix Fix");
				AddPlot(new Stroke(Brushes.Aqua,   DashStyleHelper.Solid, 3f), PlotStyle.Line, "Upper Band");
			}
			else if (State == State.DataLoaded)
			{
				// SMA / StdDev / MAX / MIN wrap with user-configurable periods (can exceed 256) — needs Infinite.
				wvfSeries	= new Series<double>(this, MaximumBarsLookBack.Infinite);
				maxClosePd	= MAX(Close,     PdLookBack);
				smaWvfBb	= SMA(wvfSeries, BbLength);
				stdDevWvfBb	= StdDev(wvfSeries, BbLength);
				maxWvfLb	= MAX(wvfSeries, LbPercentile);
				minWvfLb	= MIN(wvfSeries, LbPercentile);
			}
		}

		#endregion

		protected override void OnBarUpdate()
		{
			double highestClose = maxClosePd[0];
			double wvf          = highestClose == 0.0 ? 0.0 : ((highestClose - Low[0]) / highestClose) * 100.0;
			wvfSeries[0]        = wvf;

			double midLine    = smaWvfBb[0];
			double sDev       = Mult * stdDevWvfBb[0];
			double upperBand  = midLine + sDev;

			double rangeHigh  = maxWvfLb[0] * PhPercentile;
			double rangeLow   = minWvfLb[0] * PlPercentile;

			Values[2][0] = wvf;
			bool highlight = wvf >= upperBand || wvf >= rangeHigh;
			PlotBrushes[2][0] = highlight ? HighlightColor : NormalColor;

			if (ShowHighRange)
			{
				Values[0][0] = rangeHigh;
				Values[1][0] = rangeLow;
			}
			else
			{
				Values[0].Reset();
				Values[1].Reset();
			}

			if (ShowStdDev)
				Values[3][0] = upperBand;
			else
				Values[3].Reset();
		}

		[Browsable(false)][XmlIgnore] public Series<double> RangeHigh       { get { return Values[0]; } }
		[Browsable(false)][XmlIgnore] public Series<double> RangeLow        { get { return Values[1]; } }
		[Browsable(false)][XmlIgnore] public Series<double> WilliamsVixFix  { get { return Values[2]; } }
		[Browsable(false)][XmlIgnore] public Series<double> UpperBand       { get { return Values[3]; } }

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
		private indTradingView.CmWilliamsVixFix[] cacheCmWilliamsVixFix;
		public indTradingView.CmWilliamsVixFix CmWilliamsVixFix(int pdLookBack, int bbLength, double mult, int lbPercentile, double phPercentile, double plPercentile, bool showHighRange, bool showStdDev)
		{
			return CmWilliamsVixFix(Input, pdLookBack, bbLength, mult, lbPercentile, phPercentile, plPercentile, showHighRange, showStdDev);
		}

		public indTradingView.CmWilliamsVixFix CmWilliamsVixFix(ISeries<double> input, int pdLookBack, int bbLength, double mult, int lbPercentile, double phPercentile, double plPercentile, bool showHighRange, bool showStdDev)
		{
			if (cacheCmWilliamsVixFix != null)
				for (int idx = 0; idx < cacheCmWilliamsVixFix.Length; idx++)
					if (cacheCmWilliamsVixFix[idx] != null && cacheCmWilliamsVixFix[idx].PdLookBack == pdLookBack && cacheCmWilliamsVixFix[idx].BbLength == bbLength && cacheCmWilliamsVixFix[idx].Mult == mult && cacheCmWilliamsVixFix[idx].LbPercentile == lbPercentile && cacheCmWilliamsVixFix[idx].PhPercentile == phPercentile && cacheCmWilliamsVixFix[idx].PlPercentile == plPercentile && cacheCmWilliamsVixFix[idx].ShowHighRange == showHighRange && cacheCmWilliamsVixFix[idx].ShowStdDev == showStdDev && cacheCmWilliamsVixFix[idx].EqualsInput(input))
						return cacheCmWilliamsVixFix[idx];
			return CacheIndicator<indTradingView.CmWilliamsVixFix>(new indTradingView.CmWilliamsVixFix(){ PdLookBack = pdLookBack, BbLength = bbLength, Mult = mult, LbPercentile = lbPercentile, PhPercentile = phPercentile, PlPercentile = plPercentile, ShowHighRange = showHighRange, ShowStdDev = showStdDev }, input, ref cacheCmWilliamsVixFix);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.CmWilliamsVixFix CmWilliamsVixFix(int pdLookBack, int bbLength, double mult, int lbPercentile, double phPercentile, double plPercentile, bool showHighRange, bool showStdDev)
		{
			return indicator.CmWilliamsVixFix(Input, pdLookBack, bbLength, mult, lbPercentile, phPercentile, plPercentile, showHighRange, showStdDev);
		}

		public Indicators.indTradingView.CmWilliamsVixFix CmWilliamsVixFix(ISeries<double> input , int pdLookBack, int bbLength, double mult, int lbPercentile, double phPercentile, double plPercentile, bool showHighRange, bool showStdDev)
		{
			return indicator.CmWilliamsVixFix(input, pdLookBack, bbLength, mult, lbPercentile, phPercentile, plPercentile, showHighRange, showStdDev);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.CmWilliamsVixFix CmWilliamsVixFix(int pdLookBack, int bbLength, double mult, int lbPercentile, double phPercentile, double plPercentile, bool showHighRange, bool showStdDev)
		{
			return indicator.CmWilliamsVixFix(Input, pdLookBack, bbLength, mult, lbPercentile, phPercentile, plPercentile, showHighRange, showStdDev);
		}

		public Indicators.indTradingView.CmWilliamsVixFix CmWilliamsVixFix(ISeries<double> input , int pdLookBack, int bbLength, double mult, int lbPercentile, double phPercentile, double plPercentile, bool showHighRange, bool showStdDev)
		{
			return indicator.CmWilliamsVixFix(input, pdLookBack, bbLength, mult, lbPercentile, phPercentile, plPercentile, showHighRange, showStdDev);
		}
	}
}

#endregion
