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

// NT8 Version of Dynamic Bbpct
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
	public class DynamicBBPCT : Indicator
	{
		[NinjaScriptProperty]
	    [Display(Order = 01, GroupName = "Parameters", Name = "Length", Description = "")]
	    public int Length { get; set; }
		
		[NinjaScriptProperty]
	    [Display(Order = 02, GroupName = "Parameters", Name = "Multiplier ", Description = "")]
	    public double Multiplier  { get; set; }
		
		private DynamicSMA sma;
		private DynamicStDev stdDev;
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"";
				Name										= "Dynamic BBPCT [QuantraSystems]";
				Calculate									= Calculate.OnBarClose;
				IsOverlay									= false;
				DisplayInDataBox							= true;
				DrawOnPricePanel							= true;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				IsSuspendedWhileInactive					= true;
				
				AddPlot(new Stroke(Brushes.DimGray, DashStyleHelper.Solid, 2f), PlotStyle.Line, "Dynamic BBPCT");
				
				Length = 20;
				Multiplier = 2.0;
			}
			else if (State == State.DataLoaded)
			{
				sma = DynamicSMA(Length);
				stdDev = DynamicStDev(Length);
			}
		}

		protected override void OnBarUpdate()
		{
			if(CurrentBar < Length)
				return;
			
            double upperBand = sma[0] + Multiplier * stdDev[0];
            double lowerBand = sma[0] - Multiplier * stdDev[0];
            double bbpct = (Close[0] - lowerBand) / (upperBand - lowerBand);

            DynBBPCT[0] = BBPCT_ReScale(bbpct);
		}
		
		private double BBPCT_ReScale(double bbpct)
		{
		    return (bbpct - 0.5) * 120;
		}
		
		[Browsable(false)][XmlIgnore] public Series<double> DynBBPCT { get { return Values[0]; } }
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private indTradingView.DynamicBBPCT[] cacheDynamicBBPCT;
		public indTradingView.DynamicBBPCT DynamicBBPCT(int length, double multiplier)
		{
			return DynamicBBPCT(Input, length, multiplier);
		}

		public indTradingView.DynamicBBPCT DynamicBBPCT(ISeries<double> input, int length, double multiplier)
		{
			if (cacheDynamicBBPCT != null)
				for (int idx = 0; idx < cacheDynamicBBPCT.Length; idx++)
					if (cacheDynamicBBPCT[idx] != null && cacheDynamicBBPCT[idx].Length == length && cacheDynamicBBPCT[idx].Multiplier == multiplier && cacheDynamicBBPCT[idx].EqualsInput(input))
						return cacheDynamicBBPCT[idx];
			return CacheIndicator<indTradingView.DynamicBBPCT>(new indTradingView.DynamicBBPCT(){ Length = length, Multiplier = multiplier }, input, ref cacheDynamicBBPCT);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.DynamicBBPCT DynamicBBPCT(int length, double multiplier)
		{
			return indicator.DynamicBBPCT(Input, length, multiplier);
		}

		public Indicators.indTradingView.DynamicBBPCT DynamicBBPCT(ISeries<double> input , int length, double multiplier)
		{
			return indicator.DynamicBBPCT(input, length, multiplier);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.DynamicBBPCT DynamicBBPCT(int length, double multiplier)
		{
			return indicator.DynamicBBPCT(Input, length, multiplier);
		}

		public Indicators.indTradingView.DynamicBBPCT DynamicBBPCT(ISeries<double> input , int length, double multiplier)
		{
			return indicator.DynamicBBPCT(input, length, multiplier);
		}
	}
}

#endregion
