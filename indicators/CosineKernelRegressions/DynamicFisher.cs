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

// NT8 Version of Dynamic Fisher
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
	public class DynamicFisher : Indicator
	{
		[NinjaScriptProperty]
	    [Display(Order = 01, GroupName = "Parameters", Name = "Length", Description = "")]
	    public int Length { get; set; }
		
		private MAX max;
		private MIN min;
		private Series<double> value1, fish1;
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"";
				Name										= "Dynamic Fisher [QuantraSystems]";
				Calculate									= Calculate.OnBarClose;
				IsOverlay									= false;
				DisplayInDataBox							= true;
				DrawOnPricePanel							= true;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				IsSuspendedWhileInactive					= true;
				
				AddPlot(new Stroke(Brushes.DimGray, DashStyleHelper.Solid, 2f), PlotStyle.Line, "Dynamic Fisher");
				
				Length = 20;
			}
			else if (State == State.DataLoaded)
			{
				max = MAX(Median, Length);
				min = MIN(Median, Length);
				value1 = new Series<double>(this);
				fish1 = new Series<double>(this);
			}
		}

		protected override void OnBarUpdate()
		{
			if(CurrentBar < Length)
				return;
			
			double high = max[0];
            double low = min[0];
            value1[0] = 0.66 * ((Median[0] - low) / (high - low) - 0.5) + 0.67 * value1[1];
            double value2 = value1[0] > 0.99 ? 0.999 : value1[0] < -0.99 ? -0.999 : value1[0];
            fish1[0] = 0.5 * Math.Log((1 + value2) / (1 - value2)) + 0.5 * fish1[1];

            DynFisher[0] = FISH_ReScale(fish1[0]);
		}
		
		private double FISH_ReScale(double fish1)
		{
		    return fish1 * 30;
		}
		
		[Browsable(false)][XmlIgnore] public Series<double> DynFisher { get { return Values[0]; } }
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private indTradingView.DynamicFisher[] cacheDynamicFisher;
		public indTradingView.DynamicFisher DynamicFisher(int length)
		{
			return DynamicFisher(Input, length);
		}

		public indTradingView.DynamicFisher DynamicFisher(ISeries<double> input, int length)
		{
			if (cacheDynamicFisher != null)
				for (int idx = 0; idx < cacheDynamicFisher.Length; idx++)
					if (cacheDynamicFisher[idx] != null && cacheDynamicFisher[idx].Length == length && cacheDynamicFisher[idx].EqualsInput(input))
						return cacheDynamicFisher[idx];
			return CacheIndicator<indTradingView.DynamicFisher>(new indTradingView.DynamicFisher(){ Length = length }, input, ref cacheDynamicFisher);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.DynamicFisher DynamicFisher(int length)
		{
			return indicator.DynamicFisher(Input, length);
		}

		public Indicators.indTradingView.DynamicFisher DynamicFisher(ISeries<double> input , int length)
		{
			return indicator.DynamicFisher(input, length);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.DynamicFisher DynamicFisher(int length)
		{
			return indicator.DynamicFisher(Input, length);
		}

		public Indicators.indTradingView.DynamicFisher DynamicFisher(ISeries<double> input , int length)
		{
			return indicator.DynamicFisher(input, length);
		}
	}
}

#endregion
