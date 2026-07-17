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

// NT8 Version of RSI Trail
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the Attribution-NonCommercial-ShareAlike 4.0 International (CC BY-NC-SA 4.0) https://creativecommons.org/licenses/by-nc-sa/4.0/.
// The original Pine Script™ code is by UAlgo and can be found at: https://www.tradingview.com/script/PUGvtsEu-RSI-Trail-UAlgo/
// Adaptation for NinjaTrader by jack@mydailytake.com
// Write-up + downloads: https://mydailytake.com/tradingview-rsi-trail-ualgo-conversion-to-ninjatrader-8/
// Source + all conversions: https://github.com/MyDailyTake/tradingview-to-ninjatrader-8
// © 2024 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the Attribution-NonCommercial-ShareAlike 4.0 International (CC BY-NC-SA 4.0) https://creativecommons.org/licenses/by-nc-sa/4.0/
// The use of UAlgo name or its adapted code in this work does not imply endorsement by the original authors.

#region RsiTrail_MovingAverages

public enum RsiTrail_MovingAverages
{
	DEMA,
	EMA,
	HMA,
	SMA,
	TMA,
	TEMA,
	VWMA,
	WMA,
	ZLEMA,
}

#endregion

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	#region Categories
	
	[Gui.CategoryOrder("Indicator Inputs", 				10100)]
	[Gui.CategoryOrder("Signal Inputs", 				10200)]
	
	#endregion
	
	public class RsiTrail : Indicator
	{
		#region indInfo
		
		private string indName = "RSI Trail [UAlgo]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by UAlgo can be found here: https://www.tradingview.com/script/PUGvtsEu-RSI-Trail-UAlgo/";
		
		#endregion
		
		#region Properties

		// Indicator Inputs
		[NinjaScriptProperty]
        [Display(Order = 01, GroupName = "Indicator Inputs", Name = "Moving Average Type", Description = "")]
        public RsiTrail_MovingAverages MaType { get; set; }

		[NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Order = 02, GroupName = "Indicator Inputs", Name = "RSI Upper Bound", Description = "")]
        public double RsiUpper { get; set; }
		
        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Order = 03, GroupName = "Indicator Inputs", Name = "RSI Lower Bound", Description = "")]
        public double RsiLower { get; set; }
		
		[NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Order = 04, GroupName = "Indicator Inputs", Name = "MA / ATR Period", Description = "")]
        public int MaPeriod { get; set; }
		
		[NinjaScriptProperty]
        [Display(Order = 05, GroupName = "Indicator Inputs", Name = "Enforce Directional Movement", Description = "")]
        public bool EnforceMovement { get; set; }
		
		// Signal Inputs
		[Range(0, int.MaxValue)]
        [Display(Order = 00, GroupName = "Signal Inputs", Name = "Plot Tick Offset", Description = "")]
        public int TickOffset { get; set; }
		
	    [Display(Order = 01, GroupName = "Signal Inputs", Name = "Color Candles", Description = "")]
        public bool ColorCandles { get; set; }
		
		[XmlIgnore()]
	    [Display(Order = 02, GroupName = "Signal Inputs", Name = "Trend Color Up", Description = "")]
	    public Brush TrendUpColor { get; set; }
			[Browsable(false)]
			public string TrendUpColorSerialize
			{
			    get { return Serialize.BrushToString(TrendUpColor); }
			    set { TrendUpColor = Serialize.StringToBrush(value); }
			}
			
		[XmlIgnore()]
	    [Display(Order = 03, GroupName = "Signal Inputs", Name = "Trend Color Down", Description = "")]
	    public Brush TrendDownColor { get; set; }
			[Browsable(false)]
			public string TrendDownColorSerialize
			{
			    get { return Serialize.BrushToString(TrendDownColor); }
			    set { TrendDownColor = Serialize.StringToBrush(value); }
			}
		
		#endregion
			
		#region Variables
		
		private ISeries<double> movingAerage;
		private ATR indAtr;
		private Series<double> upperBound, lowerBound;
		private Series<int> Trend;
			
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
				
				MaType = RsiTrail_MovingAverages.EMA;
				RsiUpper = 60;
				RsiLower = 40;
				MaPeriod = 27;
				
				TickOffset = 4;
		        ColorCandles = false;
				TrendUpColor = Brushes.DodgerBlue;
				TrendDownColor = Brushes.Firebrick;
				
				AddPlot(new Stroke(Brushes.DodgerBlue, DashStyleHelper.Solid, 8f), PlotStyle.TriangleUp, "Trend Up");
				AddPlot(new Stroke(Brushes.Firebrick, DashStyleHelper.Solid, 8f), PlotStyle.TriangleDown, "Trend Down");
				AddPlot(new Stroke(Brushes.DimGray, DashStyleHelper.Solid, 3f), PlotStyle.Line, "RSI Trend Line");
				AddPlot(new Stroke(Brushes.Transparent, DashStyleHelper.Solid, 3f), PlotStyle.Line, "Moving Average");
			}
			else if (State == State.DataLoaded)
			{
				movingAerage = GetMovingAverage(MaType, Weighted, MaPeriod);
				indAtr = ATR(MaPeriod);
				
				upperBound = new Series<double>(this);
				lowerBound = new Series<double>(this);
				Trend = new Series<int>(this);
			}
		}
		
		#endregion

		protected override void OnBarUpdate()
		{
			if(CurrentBar <= MaPeriod)
				return;
			
			Trend[0] = Trend[1];
			
			upperBound[0] = movingAerage[0] + (RsiUpper - 50.0) / 10.0 * indAtr[0];
    		lowerBound[0] = movingAerage[0] - (50.0 - RsiLower) / 10.0 * indAtr[0];
			
			if(CrossAbove(Weighted, upperBound, 1))
				Trend[0] = 1;
    		if(CrossBelow(Weighted, lowerBound, 1)) 
				Trend[0] = -1;
			
			if(Trend[0] > 0)
			{
				RsiTrendLine[0] = EnforceMovement && Trend[1] > 0 ? Math.Max(lowerBound[0], RsiTrendLine[1]) : lowerBound[0];
				if(Trend[0] != Trend[1])
					TrendUp[0] = Math.Min(Low[0], RsiTrendLine[0]) - TickOffset * TickSize;
				
				DoColoring(TrendUpColor);
			}
			if(Trend[0] < 0)
			{
				RsiTrendLine[0] = EnforceMovement && Trend[1] < 0 ? Math.Min(upperBound[0], RsiTrendLine[1]) : upperBound[0];
				if(Trend[0] != Trend[1])
					TrendDown[0] = Math.Max(High[0], RsiTrendLine[0]) + TickOffset * TickSize;
					
				DoColoring(TrendDownColor);
			}
			
			MovingAverage[0] = movingAerage[0];
		}
		
		private void DoColoring(Brush brush)
		{
			PlotBrushes[2][0] = brush;
			
			if(ColorCandles)
				BarBrushes[0] = CandleOutlineBrushes[0] = brush;
		}
		
		#region GetMovingAverage
		
		private ISeries<double> GetMovingAverage(RsiTrail_MovingAverages maType, ISeries<double> input, int period)
		{
		    switch(maType)
		    {
		        case RsiTrail_MovingAverages.DEMA:
		            return DEMA(input, period);
		        case RsiTrail_MovingAverages.EMA:
		            return EMA(input, period);
		        case RsiTrail_MovingAverages.HMA:
		            return HMA(input, period);
		        case RsiTrail_MovingAverages.SMA:
		            return SMA(input, period);
		        case RsiTrail_MovingAverages.TMA:
		            return TMA(input, period);
		        case RsiTrail_MovingAverages.TEMA:
		            return TEMA(input, period);
				case RsiTrail_MovingAverages.VWMA:
		            return VWMA(input, period);
		        case RsiTrail_MovingAverages.WMA:
		            return WMA(input, period);
				case RsiTrail_MovingAverages.ZLEMA:
		            return ZLEMA(input, period);
		        default:
		            return SMA(input, period);
		    }
		}
		
		#endregion
		
		#region Plots / Public
		
		[Browsable(false)][XmlIgnore] public Series<double> TrendUp   	 { get { return Values[0]; } }
		[Browsable(false)][XmlIgnore] public Series<double> TrendDown 	 { get { return Values[1]; } }
		[Browsable(false)][XmlIgnore] public Series<double> RsiTrendLine { get { return Values[2]; } }
		[Browsable(false)][XmlIgnore] public Series<double> MovingAverage{ get { return Values[3]; } }
		
		[Browsable(false)][XmlIgnore] public Series<int> PublicTrend  	 { get { Update(); return Trend; } }
		
		#endregion
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private indTradingView.RsiTrail[] cacheRsiTrail;
		public indTradingView.RsiTrail RsiTrail(RsiTrail_MovingAverages maType, double rsiUpper, double rsiLower, int maPeriod, bool enforceMovement)
		{
			return RsiTrail(Input, maType, rsiUpper, rsiLower, maPeriod, enforceMovement);
		}

		public indTradingView.RsiTrail RsiTrail(ISeries<double> input, RsiTrail_MovingAverages maType, double rsiUpper, double rsiLower, int maPeriod, bool enforceMovement)
		{
			if (cacheRsiTrail != null)
				for (int idx = 0; idx < cacheRsiTrail.Length; idx++)
					if (cacheRsiTrail[idx] != null && cacheRsiTrail[idx].MaType == maType && cacheRsiTrail[idx].RsiUpper == rsiUpper && cacheRsiTrail[idx].RsiLower == rsiLower && cacheRsiTrail[idx].MaPeriod == maPeriod && cacheRsiTrail[idx].EnforceMovement == enforceMovement && cacheRsiTrail[idx].EqualsInput(input))
						return cacheRsiTrail[idx];
			return CacheIndicator<indTradingView.RsiTrail>(new indTradingView.RsiTrail(){ MaType = maType, RsiUpper = rsiUpper, RsiLower = rsiLower, MaPeriod = maPeriod, EnforceMovement = enforceMovement }, input, ref cacheRsiTrail);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.RsiTrail RsiTrail(RsiTrail_MovingAverages maType, double rsiUpper, double rsiLower, int maPeriod, bool enforceMovement)
		{
			return indicator.RsiTrail(Input, maType, rsiUpper, rsiLower, maPeriod, enforceMovement);
		}

		public Indicators.indTradingView.RsiTrail RsiTrail(ISeries<double> input , RsiTrail_MovingAverages maType, double rsiUpper, double rsiLower, int maPeriod, bool enforceMovement)
		{
			return indicator.RsiTrail(input, maType, rsiUpper, rsiLower, maPeriod, enforceMovement);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.RsiTrail RsiTrail(RsiTrail_MovingAverages maType, double rsiUpper, double rsiLower, int maPeriod, bool enforceMovement)
		{
			return indicator.RsiTrail(Input, maType, rsiUpper, rsiLower, maPeriod, enforceMovement);
		}

		public Indicators.indTradingView.RsiTrail RsiTrail(ISeries<double> input , RsiTrail_MovingAverages maType, double rsiUpper, double rsiLower, int maPeriod, bool enforceMovement)
		{
			return indicator.RsiTrail(input, maType, rsiUpper, rsiLower, maPeriod, enforceMovement);
		}
	}
}

#endregion
