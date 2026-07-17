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

// NT8 Version of Dynamic Stoch
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the Mozilla Public License 2.0 (MPL 2.0).
// The original Pine Script™ code is by QuantraSystems and can be found at: https://www.tradingview.com/script/wgTxuL34-Cosine-Kernel-Regressions-QuantraSystems/
// Adaptation for NinjaTrader by jack@mydailytake.com
// Write-up + downloads: https://mydailytake.com/tradingview-cosine-kernel-regressions-quantrasystems-conversion-to-ninjatrader-8/
// Source + all conversions: https://github.com/MyDailyTake/tradingview-to-ninjatrader-8
// © 2024 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the Mozilla Public License 2.0. Full license details at https://mozilla.org/MPL/2.0/
// The use of QuantraSystems' name or its adapted code in this work does not imply endorsement by the original authors.

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	public class DynamicStoch : Indicator
	{
		[NinjaScriptProperty]
	    [Display(Order = 01, GroupName = "Parameters", Name = "Length", Description = "")]
	    public int Length { get; set; }
		
		private MIN min;
		private MAX max;
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"";
				Name										= "Dynamic Stoch [QuantraSystems]";
				Calculate									= Calculate.OnBarClose;
				IsOverlay									= false;
				DisplayInDataBox							= true;
				DrawOnPricePanel							= true;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				IsSuspendedWhileInactive					= true;
				
				AddPlot(new Stroke(Brushes.DimGray, DashStyleHelper.Solid, 2f), PlotStyle.Line, "Dynamic Stoch");
				
				Length = 20;
			}
			else if (State == State.DataLoaded)
			{
				min = MIN(Low, Length);
				max = MAX(High, Length);
			}
		}

		protected override void OnBarUpdate()
		{
			if(CurrentBar < Length)
				return;
			
			double lowestLow = min[0];
            double highestHigh = max[0];
            double stochastic = 100 * (Input[0] - lowestLow) / (highestHigh - lowestLow);

            DynStoch[0] = STOCH_ReScale(stochastic);
		}
		
		private double STOCH_ReScale(double stoch)
		{
		    return (stoch - 50) * 2;
		}
		
		[Browsable(false)][XmlIgnore] public Series<double> DynStoch { get { return Values[0]; } }
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private indTradingView.DynamicStoch[] cacheDynamicStoch;
		public indTradingView.DynamicStoch DynamicStoch(int length)
		{
			return DynamicStoch(Input, length);
		}

		public indTradingView.DynamicStoch DynamicStoch(ISeries<double> input, int length)
		{
			if (cacheDynamicStoch != null)
				for (int idx = 0; idx < cacheDynamicStoch.Length; idx++)
					if (cacheDynamicStoch[idx] != null && cacheDynamicStoch[idx].Length == length && cacheDynamicStoch[idx].EqualsInput(input))
						return cacheDynamicStoch[idx];
			return CacheIndicator<indTradingView.DynamicStoch>(new indTradingView.DynamicStoch(){ Length = length }, input, ref cacheDynamicStoch);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.DynamicStoch DynamicStoch(int length)
		{
			return indicator.DynamicStoch(Input, length);
		}

		public Indicators.indTradingView.DynamicStoch DynamicStoch(ISeries<double> input , int length)
		{
			return indicator.DynamicStoch(input, length);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.DynamicStoch DynamicStoch(int length)
		{
			return indicator.DynamicStoch(Input, length);
		}

		public Indicators.indTradingView.DynamicStoch DynamicStoch(ISeries<double> input , int length)
		{
			return indicator.DynamicStoch(input, length);
		}
	}
}

#endregion
