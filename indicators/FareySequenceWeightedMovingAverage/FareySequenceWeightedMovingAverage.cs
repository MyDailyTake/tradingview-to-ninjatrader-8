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

// NT8 Version of Farey Sequence Weighted Moving Average
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the the GPL-3.0 license.
// The original Pine Script™ code is by everget and can be found at: https://www.tradingview.com/script/UQ48Qh3y-Farey-Sequence-Weighted-Moving-Average/
// Adaptation for NinjaTrader by jack@mydailytake.com
// Write-up + downloads: https://mydailytake.com/tradingview-farey-sequence-weighted-moving-average-conversion-to-ninjatrader-8/
// Source + all conversions: https://github.com/MyDailyTake/tradingview-to-ninjatrader-8
// © 2024 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the the GPL-3.0 license details at https://www.gnu.org/licenses/gpl-3.0.en.html
// The use of everget name or its adapted code in this work does not imply endorsement by the original authors.

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	#region Categories
	
	[Gui.CategoryOrder("Indicator Inputs", 				10100)]
	[Gui.CategoryOrder("Color Inputs", 					10200)]
	
	#endregion
	
	public class FareySequenceWeightedMovingAverage : Indicator
	{
		#region indInfo
		
		private string indName = "Farey Sequence Weighted Moving Average";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by everget can be found here: https://www.tradingview.com/script/UQ48Qh3y-Farey-Sequence-Weighted-Moving-Average/";
		
		#endregion
		
		#region Properties

		[Range(2, int.MaxValue)]
	    [NinjaScriptProperty]
	    [Display(Order = 02, GroupName = "Indicator Inputs", Name = "Order", Description = "")]
	    public int SequenceOrder { get; set; }
		
	    [Display(Order = 01, GroupName = "Color Inputs", Name = "Color Plot", Description = "")]
	    public bool ColorPlot { get; set; }
		
		[XmlIgnore()]
	    [Display(Order = 02, GroupName = "Color Inputs", Name = "Up", Description = "")]
	    public Brush PlotColorUp { get; set; }
			[Browsable(false)]
			public string PlotColorUpSerialize
			{
			    get { return Serialize.BrushToString(PlotColorUp); }
			    set { PlotColorUp = Serialize.StringToBrush(value); }
			}
			
		[XmlIgnore()]
	    [Display(Order = 03, GroupName = "Color Inputs", Name = "Down", Description = "")]
	    public Brush PlotColorDown { get; set; }
			[Browsable(false)]
			public string PlotColorDownSerialize
			{
			    get { return Serialize.BrushToString(PlotColorDown); }
			    set { PlotColorDown = Serialize.StringToBrush(value); }
			}
		
		#endregion
			
		#region Variables
			
		
			
		#endregion
		
		#region OnStateChange
			
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= indDescription;
				Name										= indName;
				Calculate									= Calculate.OnBarClose;
				IsOverlay									= true;
				DisplayInDataBox							= true;
				DrawOnPricePanel							= false;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				IsSuspendedWhileInactive					= true;
				
		        SequenceOrder = 5;
				
				ColorPlot = true;
				PlotColorUp = Brushes.DodgerBlue;
				PlotColorDown = Brushes.Firebrick;
				
				AddPlot(new Stroke(Brushes.Gold, DashStyleHelper.Solid, 4f), PlotStyle.Line, "FSWMA");
			}
		}
		
		#endregion

		protected override void OnBarUpdate()
		{
			if (CurrentBar < SequenceOrder)
                return;
			
			double sum = Input[0];
            double divisor = 1.0;

            for (int i = 1; i < SequenceOrder; i++)
            {
                double weight = 1.0 / (i + 1.0);
                sum += weight * Input[i];
                divisor += weight;
            }

            FSWMA[0] = sum / divisor;
			
			if(ColorPlot)
			{
				if(FSWMA[0] > FSWMA[1]) PlotBrushes[0][0] = PlotColorUp;
				if(FSWMA[0] < FSWMA[1]) PlotBrushes[0][0] = PlotColorDown;
			}
		}
		
		[Browsable(false)][XmlIgnore] public Series<double> FSWMA { get { return Values[0]; } }
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private indTradingView.FareySequenceWeightedMovingAverage[] cacheFareySequenceWeightedMovingAverage;
		public indTradingView.FareySequenceWeightedMovingAverage FareySequenceWeightedMovingAverage(int sequenceOrder)
		{
			return FareySequenceWeightedMovingAverage(Input, sequenceOrder);
		}

		public indTradingView.FareySequenceWeightedMovingAverage FareySequenceWeightedMovingAverage(ISeries<double> input, int sequenceOrder)
		{
			if (cacheFareySequenceWeightedMovingAverage != null)
				for (int idx = 0; idx < cacheFareySequenceWeightedMovingAverage.Length; idx++)
					if (cacheFareySequenceWeightedMovingAverage[idx] != null && cacheFareySequenceWeightedMovingAverage[idx].SequenceOrder == sequenceOrder && cacheFareySequenceWeightedMovingAverage[idx].EqualsInput(input))
						return cacheFareySequenceWeightedMovingAverage[idx];
			return CacheIndicator<indTradingView.FareySequenceWeightedMovingAverage>(new indTradingView.FareySequenceWeightedMovingAverage(){ SequenceOrder = sequenceOrder }, input, ref cacheFareySequenceWeightedMovingAverage);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.FareySequenceWeightedMovingAverage FareySequenceWeightedMovingAverage(int sequenceOrder)
		{
			return indicator.FareySequenceWeightedMovingAverage(Input, sequenceOrder);
		}

		public Indicators.indTradingView.FareySequenceWeightedMovingAverage FareySequenceWeightedMovingAverage(ISeries<double> input , int sequenceOrder)
		{
			return indicator.FareySequenceWeightedMovingAverage(input, sequenceOrder);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.FareySequenceWeightedMovingAverage FareySequenceWeightedMovingAverage(int sequenceOrder)
		{
			return indicator.FareySequenceWeightedMovingAverage(Input, sequenceOrder);
		}

		public Indicators.indTradingView.FareySequenceWeightedMovingAverage FareySequenceWeightedMovingAverage(ISeries<double> input , int sequenceOrder)
		{
			return indicator.FareySequenceWeightedMovingAverage(input, sequenceOrder);
		}
	}
}

#endregion
