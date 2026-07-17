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

// NT8 Version of Dynamic VZO
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
	public class DynamicVZO : Indicator
	{
		[NinjaScriptProperty]
	    [Display(Order = 01, GroupName = "Parameters", Name = "Length", Description = "")]
	    public int Length { get; set; }
		
		private Series<double> r;
		private DynamicEMA emaR, emaV;
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"";
				Name										= "Dynamic VZO [QuantraSystems]";
				Calculate									= Calculate.OnBarClose;
				IsOverlay									= false;
				DisplayInDataBox							= true;
				DrawOnPricePanel							= true;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				IsSuspendedWhileInactive					= true;
				
				AddPlot(new Stroke(Brushes.DimGray, DashStyleHelper.Solid, 2f), PlotStyle.Line, "Dynamic VZO");
				
				Length = 20;
			}
			else if (State == State.DataLoaded)
			{
				r = new Series<double>(this);
				emaR = DynamicEMA(r, Length / 3);
				emaV = DynamicEMA(Volume, Length / 3);
			}
		}

		protected override void OnBarUpdate()
		{
			if(CurrentBar < Length)
				return;
			
			r[0] = (Close[0] > Close[1] ? Volume[0] : -Volume[0]);
			double vp = emaR[0];
            double tv = emaV[0];

            DynVZO[0] = VZO_ReScale(vp, tv);
		}
		
		private double VZO_ReScale(double VP, double TV)
		{
		    return (VP / TV) * 110;
		}
		
		[Browsable(false)][XmlIgnore] public Series<double> DynVZO { get { return Values[0]; } }
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private indTradingView.DynamicVZO[] cacheDynamicVZO;
		public indTradingView.DynamicVZO DynamicVZO(int length)
		{
			return DynamicVZO(Input, length);
		}

		public indTradingView.DynamicVZO DynamicVZO(ISeries<double> input, int length)
		{
			if (cacheDynamicVZO != null)
				for (int idx = 0; idx < cacheDynamicVZO.Length; idx++)
					if (cacheDynamicVZO[idx] != null && cacheDynamicVZO[idx].Length == length && cacheDynamicVZO[idx].EqualsInput(input))
						return cacheDynamicVZO[idx];
			return CacheIndicator<indTradingView.DynamicVZO>(new indTradingView.DynamicVZO(){ Length = length }, input, ref cacheDynamicVZO);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.DynamicVZO DynamicVZO(int length)
		{
			return indicator.DynamicVZO(Input, length);
		}

		public Indicators.indTradingView.DynamicVZO DynamicVZO(ISeries<double> input , int length)
		{
			return indicator.DynamicVZO(input, length);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.DynamicVZO DynamicVZO(int length)
		{
			return indicator.DynamicVZO(Input, length);
		}

		public Indicators.indTradingView.DynamicVZO DynamicVZO(ISeries<double> input , int length)
		{
			return indicator.DynamicVZO(input, length);
		}
	}
}

#endregion
