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

// NT8 Version of Beta-Weighted Moving Average
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the the Creative Commons Attribution-ShareAlike 4.0 International License.
// The original Pine Script™ code is by alexgrover and can be found at: https://www.tradingview.com/script/mheEtfmN-A-Useful-MA-Weighting-Function-For-Controlling-Lag-Smoothness/
// Adaptation for NinjaTrader by jack@mydailytake.com
// Write-up + downloads: https://mydailytake.com/tradingview-beta-weighted-moving-average-bwma-conversion-to-ninjatrader-8/
// Source + all conversions: https://github.com/MyDailyTake/tradingview-to-ninjatrader-8
// © 2024 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the the Creative Commons Attribution-ShareAlike 4.0 International License https://creativecommons.org/licenses/by-sa/4.0/
// The use of alexgrover name or its adapted code in this work does not imply endorsement by the original authors.

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	#region Categories
	
	[Gui.CategoryOrder("Indicator Inputs", 				10100)]
	[Gui.CategoryOrder("Color Inputs", 					10200)]
	
	#endregion
	
	public class BetaWeightedMovingAverage : Indicator
	{
		#region indInfo
		
		private string indName = "Beta-Weighted Moving Average (BWMA)";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by alexgrover can be found here: https://www.tradingview.com/script/mheEtfmN-A-Useful-MA-Weighting-Function-For-Controlling-Lag-Smoothness/";
		
		#endregion
		
		#region Properties

		[Range(1, int.MaxValue)]
	    [NinjaScriptProperty]
	    [Display(Order = 01, GroupName = "Indicator Inputs", Name = "Length", Description = "Length of the moving average.")]
	    public int Length { get; set; }

	    [Range(1, 10)]
	    [NinjaScriptProperty]
	    [Display(Order = 02, GroupName = "Indicator Inputs", Name = "-Lag (Beta)", Description = "Beta parameter for controlling lag.")]
	    public double Beta { get; set; }

	    [Range(1, 10)]
	    [NinjaScriptProperty]
	    [Display(Order = 03, GroupName = "Indicator Inputs", Name = "+Lag (Alpha)", Description = "Alpha parameter for controlling lag.")]
	    public double Alpha { get; set; }
		
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
			
		private double[] weights;
		
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
				
		        Length = 50;
		        Beta = 3;
		        Alpha = 3;
				
				ColorPlot = true;
				PlotColorUp = Brushes.DodgerBlue;
				PlotColorDown = Brushes.Firebrick;
				
				AddPlot(new Stroke(Brushes.Gold, DashStyleHelper.Solid, 4f), PlotStyle.Line, "BWMA");
			}
			else if (State == State.DataLoaded)
			{
                weights = new double[Length];

                for (int i = 0; i < Length; i++)
                {
                    double x = (double)i / (Length - 1);
                    weights[i] = Math.Pow(x, Alpha - 1) * Math.Pow(1 - x, Beta - 1);
                }
			}
		}
		
		#endregion

		protected override void OnBarUpdate()
		{
			if (CurrentBar < Length)
                return;

            double sum = 0;
            double den = 0;
            for (int i = 0; i < Length; i++)
            {
                sum += Input[i] * weights[i];
                den += weights[i];
            }

            BWMA[0] = sum / den;
			
			if(ColorPlot)
			{
				if(BWMA[0] > BWMA[1]) PlotBrushes[0][0] = PlotColorUp;
				if(BWMA[0] < BWMA[1]) PlotBrushes[0][0] = PlotColorDown;
			}
		}
		
		[Browsable(false)][XmlIgnore] public Series<double> BWMA { get { return Values[0]; } }
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private indTradingView.BetaWeightedMovingAverage[] cacheBetaWeightedMovingAverage;
		public indTradingView.BetaWeightedMovingAverage BetaWeightedMovingAverage(int length, double beta, double alpha)
		{
			return BetaWeightedMovingAverage(Input, length, beta, alpha);
		}

		public indTradingView.BetaWeightedMovingAverage BetaWeightedMovingAverage(ISeries<double> input, int length, double beta, double alpha)
		{
			if (cacheBetaWeightedMovingAverage != null)
				for (int idx = 0; idx < cacheBetaWeightedMovingAverage.Length; idx++)
					if (cacheBetaWeightedMovingAverage[idx] != null && cacheBetaWeightedMovingAverage[idx].Length == length && cacheBetaWeightedMovingAverage[idx].Beta == beta && cacheBetaWeightedMovingAverage[idx].Alpha == alpha && cacheBetaWeightedMovingAverage[idx].EqualsInput(input))
						return cacheBetaWeightedMovingAverage[idx];
			return CacheIndicator<indTradingView.BetaWeightedMovingAverage>(new indTradingView.BetaWeightedMovingAverage(){ Length = length, Beta = beta, Alpha = alpha }, input, ref cacheBetaWeightedMovingAverage);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.BetaWeightedMovingAverage BetaWeightedMovingAverage(int length, double beta, double alpha)
		{
			return indicator.BetaWeightedMovingAverage(Input, length, beta, alpha);
		}

		public Indicators.indTradingView.BetaWeightedMovingAverage BetaWeightedMovingAverage(ISeries<double> input , int length, double beta, double alpha)
		{
			return indicator.BetaWeightedMovingAverage(input, length, beta, alpha);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.BetaWeightedMovingAverage BetaWeightedMovingAverage(int length, double beta, double alpha)
		{
			return indicator.BetaWeightedMovingAverage(Input, length, beta, alpha);
		}

		public Indicators.indTradingView.BetaWeightedMovingAverage BetaWeightedMovingAverage(ISeries<double> input , int length, double beta, double alpha)
		{
			return indicator.BetaWeightedMovingAverage(input, length, beta, alpha);
		}
	}
}

#endregion
