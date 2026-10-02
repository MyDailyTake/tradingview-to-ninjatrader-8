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

// NT8 Version of RSI With Noise Elimination Technology
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the Mozilla Public License 2.0 (MPL 2.0).
// The original Pine Script™ code is by alexgrover and can be found at: https://www.tradingview.com/script/Rso1v0W9-RSI-With-Noise-Elimination-Technology-John-Ehlers/
// Adaptation for NinjaTrader by jack@mydailytake.com
// © 2026 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the Mozilla Public License 2.0. Full license details at https://mozilla.org/MPL/2.0/
// The use of alexgrover name or its adapted code in this work does not imply endorsement by the original authors.
//
// Notes:
//   Ehlers' Noise Elimination Technology applied to a normalized momentum oscillator.
//   The NET line is a rolling Kendall rank correlation coefficient of the underlying RSI; absolute values close to 1
//   indicate a strong directional move, while values near zero indicate noise.
//   Two plots: NET and the underlying normalized RSI. Non-repainting.
//   Public Series outputs: Net, Rsi.

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	#region Categories
	[Gui.CategoryOrder("Indicator Setup", 10100)]
	#endregion

	public class RsiNoiseElimination : Indicator
	{
		#region indInfo

		private string indName        = "RSI With Noise Elimination [alexgrover]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by alexgrover can be found here: https://www.tradingview.com/script/Rso1v0W9-RSI-With-Noise-Elimination-Technology-John-Ehlers/";

		#endregion

		#region Properties

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 1, GroupName = "Indicator Setup", Name = "RSI Length",
			Description = "Lookback period for the normalized momentum oscillator (Ehlers efficiency ratio).")]
		public int RsiLength { get; set; }

		[NinjaScriptProperty]
		[Range(2, int.MaxValue)]
		[Display(Order = 2, GroupName = "Indicator Setup", Name = "NET Length",
			Description = "Window size for the Kendall rank correlation kernel applied to the normalized RSI.")]
		public int NetLength { get; set; }

		#endregion

		#region Variables

		private Series<double> myRsiSeries;

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

				RsiLength = 14;
				NetLength = 14;

				AddPlot(new Stroke(new SolidColorBrush(Color.FromRgb(0x21, 0x57, 0xf3)), DashStyleHelper.Solid, 2f), PlotStyle.Line, "NET");
				AddPlot(new Stroke(new SolidColorBrush(Color.FromRgb(0xff, 0x11, 0x00)), DashStyleHelper.Solid, 2f), PlotStyle.Line, "RSI");

				AddLine(new Stroke(Brushes.Gray, DashStyleHelper.Dash, 1f), 0.0, "Zero");
			}
			else if (State == State.DataLoaded)
			{
				// Inner loop reads myRsiSeries[i] for i up to NetLength-1 (user-configurable, can exceed 256) — needs Infinite.
				myRsiSeries = new Series<double>(this, MaximumBarsLookBack.Infinite);
			}
		}

		#endregion

		protected override void OnBarUpdate()
		{
			// Stage 1: normalized momentum oscillator (efficiency-ratio form).
			if (CurrentBar < RsiLength) return;

			double num = Input[0] - Input[RsiLength];
			double den = 0.0;
			for (int k = 0; k < RsiLength; k++)
				den += Math.Abs(Input[k] - Input[k + 1]);

			double myRsi   = den == 0.0 ? 0.0 : num / den;
			myRsiSeries[0] = myRsi;
			Values[1][0]   = myRsi;

			// Stage 2: NET requires NetLength-1 prior valid MyRSI values.
			if (CurrentBar < RsiLength + NetLength - 1) return;

			double accum = 0.0;
			for (int i = 1; i <= NetLength - 1; i++)
			{
				for (int j = 1; j <= i; j++)
				{
					double diff = myRsiSeries[i] - myRsiSeries[i - j];
					if      (diff > 0) accum -= 1.0;
					else if (diff < 0) accum += 1.0;
				}
			}

			double netDen = 0.5 * NetLength * (NetLength - 1);
			Values[0][0]  = netDen == 0.0 ? 0.0 : accum / netDen;
		}

		[Browsable(false)][XmlIgnore] public Series<double> Net { get { return Values[0]; } }
		[Browsable(false)][XmlIgnore] public Series<double> Rsi { get { return Values[1]; } }
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private indTradingView.RsiNoiseElimination[] cacheRsiNoiseElimination;
		public indTradingView.RsiNoiseElimination RsiNoiseElimination(int rsiLength, int netLength)
		{
			return RsiNoiseElimination(Input, rsiLength, netLength);
		}

		public indTradingView.RsiNoiseElimination RsiNoiseElimination(ISeries<double> input, int rsiLength, int netLength)
		{
			if (cacheRsiNoiseElimination != null)
				for (int idx = 0; idx < cacheRsiNoiseElimination.Length; idx++)
					if (cacheRsiNoiseElimination[idx] != null && cacheRsiNoiseElimination[idx].RsiLength == rsiLength && cacheRsiNoiseElimination[idx].NetLength == netLength && cacheRsiNoiseElimination[idx].EqualsInput(input))
						return cacheRsiNoiseElimination[idx];
			return CacheIndicator<indTradingView.RsiNoiseElimination>(new indTradingView.RsiNoiseElimination(){ RsiLength = rsiLength, NetLength = netLength }, input, ref cacheRsiNoiseElimination);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.RsiNoiseElimination RsiNoiseElimination(int rsiLength, int netLength)
		{
			return indicator.RsiNoiseElimination(Input, rsiLength, netLength);
		}

		public Indicators.indTradingView.RsiNoiseElimination RsiNoiseElimination(ISeries<double> input , int rsiLength, int netLength)
		{
			return indicator.RsiNoiseElimination(input, rsiLength, netLength);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.RsiNoiseElimination RsiNoiseElimination(int rsiLength, int netLength)
		{
			return indicator.RsiNoiseElimination(Input, rsiLength, netLength);
		}

		public Indicators.indTradingView.RsiNoiseElimination RsiNoiseElimination(ISeries<double> input , int rsiLength, int netLength)
		{
			return indicator.RsiNoiseElimination(input, rsiLength, netLength);
		}
	}
}

#endregion
