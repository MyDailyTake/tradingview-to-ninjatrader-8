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

// NT8 Version of Dynamic Cmo
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
	public class DynamicCMO : Indicator
	{
		[NinjaScriptProperty]
	    [Display(Order = 01, GroupName = "Parameters", Name = "Length", Description = "")]
	    public int Length { get; set; }
		
		private Series<double> m1, m2;
		private SUM sm1, sm2;
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"";
				Name										= "Dynamic CMO [QuantraSystems]";
				Calculate									= Calculate.OnBarClose;
				IsOverlay									= false;
				DisplayInDataBox							= true;
				DrawOnPricePanel							= true;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				IsSuspendedWhileInactive					= true;
				
				AddPlot(new Stroke(Brushes.DimGray, DashStyleHelper.Solid, 2f), PlotStyle.Line, "Dynamic CMO");
				
				Length = 20;
			}
			else if (State == State.DataLoaded)
			{
				m1 = new Series<double>(this);
				m2 = new Series<double>(this);
				sm1 = SUM(m1, Length);
				sm2 = SUM(m2, Length);
			}
		}

		protected override void OnBarUpdate()
		{
			if(CurrentBar < Length)
				return;
			
			double momm = Input[0] - Input[1];
			m1[0] = momm >= 0.0 ?  momm : 0.0;
			m2[0] = momm <  0.0 ? -momm : 0.0;
			double div = sm1[0] + sm2[0];
			double chandeMO = div != 0 ? 100 * (sm1[0] - sm2[0]) / div : 0;
			
			DynCMO[0] = CMO_ReScale(chandeMO);
		}
		
		private double CMO_ReScale(double chandeMO)
		{
		    return chandeMO * 1.15;
		}
		
		[Browsable(false)][XmlIgnore] public Series<double> DynCMO { get { return Values[0]; } }
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private indTradingView.DynamicCMO[] cacheDynamicCMO;
		public indTradingView.DynamicCMO DynamicCMO(int length)
		{
			return DynamicCMO(Input, length);
		}

		public indTradingView.DynamicCMO DynamicCMO(ISeries<double> input, int length)
		{
			if (cacheDynamicCMO != null)
				for (int idx = 0; idx < cacheDynamicCMO.Length; idx++)
					if (cacheDynamicCMO[idx] != null && cacheDynamicCMO[idx].Length == length && cacheDynamicCMO[idx].EqualsInput(input))
						return cacheDynamicCMO[idx];
			return CacheIndicator<indTradingView.DynamicCMO>(new indTradingView.DynamicCMO(){ Length = length }, input, ref cacheDynamicCMO);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.DynamicCMO DynamicCMO(int length)
		{
			return indicator.DynamicCMO(Input, length);
		}

		public Indicators.indTradingView.DynamicCMO DynamicCMO(ISeries<double> input , int length)
		{
			return indicator.DynamicCMO(input, length);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.DynamicCMO DynamicCMO(int length)
		{
			return indicator.DynamicCMO(Input, length);
		}

		public Indicators.indTradingView.DynamicCMO DynamicCMO(ISeries<double> input , int length)
		{
			return indicator.DynamicCMO(input, length);
		}
	}
}

#endregion
