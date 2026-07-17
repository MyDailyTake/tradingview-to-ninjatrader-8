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

// NT8 Version of Dynamic StDev
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
	public class DynamicStDev : Indicator
	{
		[NinjaScriptProperty]
	    [Display(Order = 01, GroupName = "Parameters", Name = "Length", Description = "")]
	    public int Length { get; set; }
		
		private DynamicSMA mean;
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"";
				Name										= "Dynamic Stdev [QuantraSystems]";
				Calculate									= Calculate.OnBarClose;
				IsOverlay									= false;
				DisplayInDataBox							= true;
				DrawOnPricePanel							= true;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				IsSuspendedWhileInactive					= true;
				
				AddPlot(new Stroke(Brushes.DimGray, DashStyleHelper.Solid, 2f), PlotStyle.Line, "Dynamic Standard Deviation");
				
				Length = 20;
			}
			else if (State == State.DataLoaded)
			{
				mean = DynamicSMA(Length);
			}
		}

		protected override void OnBarUpdate()
		{
			if(CurrentBar < Length)
				return;
			
			double sumSquares = 0.0;
            double meanValue = mean[0];
            for(int i = 0; i < Length; i++)
            {
                double diff = Input[i] - meanValue;
                sumSquares += Math.Pow(diff, 2);
            }

            DynStdDev[0] = Math.Sqrt(sumSquares / Length);
		}
		
		[Browsable(false)][XmlIgnore] public Series<double> DynStdDev { get { return Values[0]; } }
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private indTradingView.DynamicStDev[] cacheDynamicStDev;
		public indTradingView.DynamicStDev DynamicStDev(int length)
		{
			return DynamicStDev(Input, length);
		}

		public indTradingView.DynamicStDev DynamicStDev(ISeries<double> input, int length)
		{
			if (cacheDynamicStDev != null)
				for (int idx = 0; idx < cacheDynamicStDev.Length; idx++)
					if (cacheDynamicStDev[idx] != null && cacheDynamicStDev[idx].Length == length && cacheDynamicStDev[idx].EqualsInput(input))
						return cacheDynamicStDev[idx];
			return CacheIndicator<indTradingView.DynamicStDev>(new indTradingView.DynamicStDev(){ Length = length }, input, ref cacheDynamicStDev);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.DynamicStDev DynamicStDev(int length)
		{
			return indicator.DynamicStDev(Input, length);
		}

		public Indicators.indTradingView.DynamicStDev DynamicStDev(ISeries<double> input , int length)
		{
			return indicator.DynamicStDev(input, length);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.DynamicStDev DynamicStDev(int length)
		{
			return indicator.DynamicStDev(Input, length);
		}

		public Indicators.indTradingView.DynamicStDev DynamicStDev(ISeries<double> input , int length)
		{
			return indicator.DynamicStDev(input, length);
		}
	}
}

#endregion
