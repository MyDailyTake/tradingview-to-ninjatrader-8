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

// NT8 Version of Andean Oscillator
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under a Attribution-NonCommercial-ShareAlike 4.0 International.
// The original Pine Script™ code is by alexgrover and can be found at: https://www.tradingview.com/script/x9qYvBYN-Andean-Oscillator/
// Adaptation for NinjaTrader by jack@mydailytake.com
// Write-up + downloads: https://mydailytake.com/andean-oscillator-alexgrover-ninjatrader-8/
// Source + all conversions: https://github.com/MyDailyTake/tradingview-to-ninjatrader-8
// © 2026 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the Attribution-NonCommercial-ShareAlike 4.0 International (CC BY-NC-SA 4.0) https://creativecommons.org/licenses/by-nc-sa/4.0/
// The use of alexgrover name or its adapted code in this work does not imply endorsement by the original authors.
//
// Notes:
//   Online-algorithm exponential envelopes on close/open and their squares.
//   Bullish component = √(dn2 − dn1²), Bearish component = √(up2 − up1²).
//   Signal line is an EMA of the larger component — when bull or bear crosses above signal, momentum is dominant.
//   Non-repainting. Public Series outputs: Bull, Bear, Signal.

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	#region Categories
	[Gui.CategoryOrder("Indicator Setup", 10100)]
	#endregion

	public class AndeanOscillator : Indicator
	{
		#region indInfo

		private string indName        = "Andean Oscillator [alexgrover]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by alexgrover can be found here: https://www.tradingview.com/script/x9qYvBYN-Andean-Oscillator/";

		#endregion

		#region Properties

		[NinjaScriptProperty]
		[Range(2, int.MaxValue)]
		[Display(Order = 1, GroupName = "Indicator Setup", Name = "Length",
			Description = "Length controlling the smoothing alpha (alpha = 2 / (length + 1)).")]
		public int Length { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 2, GroupName = "Indicator Setup", Name = "Signal Length",
			Description = "EMA length applied to the larger of the bullish/bearish components.")]
		public int SigLength { get; set; }

		#endregion

		#region Variables

		private double alpha;
		private double up1, up2, dn1, dn2;
		private Series<double> maxComponent;
		private EMA            emaSig;

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

				Length    = 50;
				SigLength = 9;

				AddPlot(new Stroke(new SolidColorBrush(Color.FromRgb(0x08, 0x99, 0x81)), DashStyleHelper.Solid, 2f), PlotStyle.Line, "Bull");
				AddPlot(new Stroke(new SolidColorBrush(Color.FromRgb(0xf2, 0x36, 0x45)), DashStyleHelper.Solid, 2f), PlotStyle.Line, "Bear");
				AddPlot(new Stroke(new SolidColorBrush(Color.FromRgb(0xff, 0x98, 0x00)), DashStyleHelper.Solid, 2f), PlotStyle.Line, "Signal");
			}
			else if (State == State.DataLoaded)
			{
				alpha        = 2.0 / (Length + 1);
				// EMA wraps with SigLength (user-configurable, can exceed 256) — needs Infinite.
				maxComponent = new Series<double>(this, MaximumBarsLookBack.Infinite);
				emaSig       = EMA(maxComponent, SigLength);
			}
		}

		#endregion

		protected override void OnBarUpdate()
		{
			double C  = Close[0];
			double O  = Open[0];
			double C2 = C * C;
			double O2 = O * O;

			// Default-zero fields seed the recursion uniformly — bar 0 reads prev=0 and the formula gives the correct first-bar value.
			up1 = Math.Max(Math.Max(C,  O),  up1 - (up1 - C)  * alpha);
			up2 = Math.Max(Math.Max(C2, O2), up2 - (up2 - C2) * alpha);
			dn1 = Math.Min(Math.Min(C,  O),  dn1 + (C  - dn1) * alpha);
			dn2 = Math.Min(Math.Min(C2, O2), dn2 + (C2 - dn2) * alpha);

			double bull = Math.Sqrt(Math.Max(0.0, dn2 - dn1 * dn1));
			double bear = Math.Sqrt(Math.Max(0.0, up2 - up1 * up1));

			Values[0][0] = bull;
			Values[1][0] = bear;

			maxComponent[0] = Math.Max(bull, bear);
			Values[2][0]    = emaSig[0];
		}

		[Browsable(false)][XmlIgnore] public Series<double> Bull   { get { return Values[0]; } }
		[Browsable(false)][XmlIgnore] public Series<double> Bear   { get { return Values[1]; } }
		[Browsable(false)][XmlIgnore] public Series<double> Signal { get { return Values[2]; } }
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private indTradingView.AndeanOscillator[] cacheAndeanOscillator;
		public indTradingView.AndeanOscillator AndeanOscillator(int length, int sigLength)
		{
			return AndeanOscillator(Input, length, sigLength);
		}

		public indTradingView.AndeanOscillator AndeanOscillator(ISeries<double> input, int length, int sigLength)
		{
			if (cacheAndeanOscillator != null)
				for (int idx = 0; idx < cacheAndeanOscillator.Length; idx++)
					if (cacheAndeanOscillator[idx] != null && cacheAndeanOscillator[idx].Length == length && cacheAndeanOscillator[idx].SigLength == sigLength && cacheAndeanOscillator[idx].EqualsInput(input))
						return cacheAndeanOscillator[idx];
			return CacheIndicator<indTradingView.AndeanOscillator>(new indTradingView.AndeanOscillator(){ Length = length, SigLength = sigLength }, input, ref cacheAndeanOscillator);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.AndeanOscillator AndeanOscillator(int length, int sigLength)
		{
			return indicator.AndeanOscillator(Input, length, sigLength);
		}

		public Indicators.indTradingView.AndeanOscillator AndeanOscillator(ISeries<double> input , int length, int sigLength)
		{
			return indicator.AndeanOscillator(input, length, sigLength);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.AndeanOscillator AndeanOscillator(int length, int sigLength)
		{
			return indicator.AndeanOscillator(Input, length, sigLength);
		}

		public Indicators.indTradingView.AndeanOscillator AndeanOscillator(ISeries<double> input , int length, int sigLength)
		{
			return indicator.AndeanOscillator(input, length, sigLength);
		}
	}
}

#endregion
