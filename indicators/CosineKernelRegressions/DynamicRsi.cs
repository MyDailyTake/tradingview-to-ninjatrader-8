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

// NT8 Version of Dynamic Rsi
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
	public class DynamicRSI : Indicator
	{
		[NinjaScriptProperty]
	    [Display(Order = 01, GroupName = "Parameters", Name = "Length", Description = "")]
	    public int Length { get; set; }
		
		private DynamicRMA rmaGain, rmaLoss;
		private Series<double> gain, loss;
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"";
				Name										= "Dynamic RSI [QuantraSystems]";
				Calculate									= Calculate.OnBarClose;
				IsOverlay									= false;
				DisplayInDataBox							= true;
				DrawOnPricePanel							= true;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				IsSuspendedWhileInactive					= true;
				
				AddPlot(new Stroke(Brushes.DimGray, DashStyleHelper.Solid, 2f), PlotStyle.Line, "Dynamic RSI");
				
				Length = 20;
			}
			else if (State == State.DataLoaded)
			{
				gain = new Series<double>(this);
				loss = new Series<double>(this);
				rmaGain = DynamicRMA(gain, Length);
				rmaLoss = DynamicRMA(loss, Length);
			}
		}

		protected override void OnBarUpdate()
		{
			if(CurrentBar < Length)
				return;
			
			gain[0] = Math.Max(Input[0] - Input[1], 0);
            loss[0] = Math.Max(Input[1] - Input[0], 0);

            double rs = rmaGain[0] / rmaLoss[0];
            double rsi = 100 - (100 / (1 + rs));

            DynRSI[0] = RSI_ReScale(rsi);
		}
		
		private double RSI_ReScale(double res)
		{
		    return (res - 50) * 2.8;
		}
		
		[Browsable(false)][XmlIgnore] public Series<double> DynRSI { get { return Values[0]; } }
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private indTradingView.DynamicRSI[] cacheDynamicRSI;
		public indTradingView.DynamicRSI DynamicRSI(int length)
		{
			return DynamicRSI(Input, length);
		}

		public indTradingView.DynamicRSI DynamicRSI(ISeries<double> input, int length)
		{
			if (cacheDynamicRSI != null)
				for (int idx = 0; idx < cacheDynamicRSI.Length; idx++)
					if (cacheDynamicRSI[idx] != null && cacheDynamicRSI[idx].Length == length && cacheDynamicRSI[idx].EqualsInput(input))
						return cacheDynamicRSI[idx];
			return CacheIndicator<indTradingView.DynamicRSI>(new indTradingView.DynamicRSI(){ Length = length }, input, ref cacheDynamicRSI);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.DynamicRSI DynamicRSI(int length)
		{
			return indicator.DynamicRSI(Input, length);
		}

		public Indicators.indTradingView.DynamicRSI DynamicRSI(ISeries<double> input , int length)
		{
			return indicator.DynamicRSI(input, length);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.DynamicRSI DynamicRSI(int length)
		{
			return indicator.DynamicRSI(Input, length);
		}

		public Indicators.indTradingView.DynamicRSI DynamicRSI(ISeries<double> input , int length)
		{
			return indicator.DynamicRSI(input, length);
		}
	}
}

#endregion
