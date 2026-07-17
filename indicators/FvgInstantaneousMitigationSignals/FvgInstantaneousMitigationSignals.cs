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

// NT8 Version of FVG Instantaneous Mitigation Signals
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under a Attribution-NonCommercial-ShareAlike 4.0 International.
// The original Pine Script™ code is by LuxAlgo and can be found at: https://www.tradingview.com/script/xYpl5UdE-FVG-Instantaneous-Mitigation-Signals-LuxAlgo/
// Adaptation for NinjaTrader by jack@mydailytake.com
// Write-up + downloads: https://mydailytake.com/tradingview-fvg-instantaneous-mitigation-signals-luxalgo-conversion-to-ninjatrader-8/
// Source + all conversions: https://github.com/MyDailyTake/tradingview-to-ninjatrader-8
// © 2024 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the Attribution-NonCommercial-ShareAlike 4.0 International (CC BY-NC-SA 4.0) https://creativecommons.org/licenses/by-nc-sa/4.0/
// The use of LuxAlgo name or its adapted code in this work does not imply endorsement by the original authors.

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	#region Categories
	
	[Gui.CategoryOrder("Setup Inputs", 					10100)]
	[Gui.CategoryOrder("Kernel Calibration", 			10200)]
	[Gui.CategoryOrder("Display", 						10300)]
	
	#endregion
	
	public class FvgInstantaneousMitigationSignals : Indicator
	{
		#region indInfo
		
		private string indName = "FVG Instantaneous Mitigation Signals [LuxAlgo]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by LuxAlgo can be found here: https://www.tradingview.com/script/xYpl5UdE-FVG-Instantaneous-Mitigation-Signals-LuxAlgo/";
		
		#endregion
		
		#region Properties

	    [NinjaScriptProperty]
	    [Display(Order = 01, GroupName = "Setup Inputs", Name = "Show Bull", Description = "Show bull signals")]
	    public bool ShowBull { get; set; }
		
		[NinjaScriptProperty]
	    [Display(Order = 02, GroupName = "Setup Inputs", Name = "Show Bear", Description = "Show bear signals")]
	    public bool ShowBear { get; set; }
		
		[NinjaScriptProperty]
	    [Display(Order = 03, GroupName = "Setup Inputs", Name = "Show Bull Average", Description = "Show bull signals")]
	    public bool ShowBullAvg { get; set; }
		
		[NinjaScriptProperty]
	    [Display(Order = 04, GroupName = "Setup Inputs", Name = "Show Bear Average", Description = "Show bear signals")]
	    public bool ShowBearAvg { get; set; }
		
		[NinjaScriptProperty]
		[Range(0, double.MaxValue)]
		[Display(Order = 05, GroupName = "Setup Inputs", Name = "FVG Width Filter", Description = "")]
		public double FilterWidth { get; set; }
		
		[NinjaScriptProperty]
	    [Display(Order = 06, GroupName = "Setup Inputs", Name = "Reset Every Signal", Description = "Every signal = true; Inverse = false")]
	    public bool EveryOrInverse { get; set; }
		
		[NinjaScriptProperty]
		[Range(0, double.MaxValue)]
		[Display(Order = 07, GroupName = "Setup Inputs", Name = "Trailing Multiple", Description = "")]
		public double TrailingMultiple { get; set; }
		
		[NinjaScriptProperty]
	    [Display(Order = 08, GroupName = "Setup Inputs", Name = "Stop Profit / Stop", Description = "")]
	    public bool ShowProfitStop { get; set; }
		
		[NinjaScriptProperty]
		[Range(0, double.MaxValue)]
		[Display(Order = 09, GroupName = "Setup Inputs", Name = "Profit Multiple", Description = "")]
		public double ProfitMultiple { get; set; }
		
		[NinjaScriptProperty]
		[Range(0, double.MaxValue)]
		[Display(Order = 10, GroupName = "Setup Inputs", Name = "Stop Multiple", Description = "")]
		public double StopMultiple { get; set; }
		
		#endregion
			
		#region Variables
			
		private ATR indATR;
		private Series<int> trend;
		private double TrailingStop;
		private bool TrailingStopReached;
		private bool isPriceChange;
			
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
				
		        ShowBull = true;
				ShowBear = true;
				ShowBullAvg = true;
				ShowBearAvg = true;
				FilterWidth = 0.0;
				EveryOrInverse = true;
				TrailingMultiple = 3.0;
				ShowProfitStop = false;
				ProfitMultiple = 4.0;
				StopMultiple = 2.0;
				
				AddPlot(new Stroke(Brushes.DodgerBlue, DashStyleHelper.Solid, 8f), PlotStyle.TriangleUp, "Bull Signal");
				AddPlot(new Stroke(Brushes.Firebrick, DashStyleHelper.Solid, 8f), PlotStyle.TriangleDown, "Bear Signal");
				
				AddPlot(new Stroke(Brushes.DodgerBlue, DashStyleHelper.Dash, 3f), PlotStyle.Hash, "Bull Line");
				AddPlot(new Stroke(Brushes.Firebrick, DashStyleHelper.Dash, 3f), PlotStyle.Hash, "Bear Line");
				
				AddPlot(new Stroke(Brushes.DodgerBlue, DashStyleHelper.Solid, 3f), PlotStyle.Line, "Bull Trailing");
				AddPlot(new Stroke(Brushes.Firebrick, DashStyleHelper.Solid, 3f), PlotStyle.Line, "Bear Trailing");
			}
			else if (State == State.DataLoaded)
			{
				if(Calculate == Calculate.OnEachTick)
					Calculate = Calculate.OnPriceChange;
				
				if(Calculate == Calculate.OnPriceChange)
					isPriceChange = true;
				
				indATR = ATR(200);
				trend = new Series<int>(this);
			}
		}
		
		#endregion

		protected override void OnBarUpdate()
		{
			#region Pre-Process
			
			if(CurrentBar < 200)
				return;
			
			if(isPriceChange)
			{
				for(int i = 0; i <= Values.Length-1; i++)
					Values[i].Reset();
				
				RemoveDrawObject("Stop / Target " + CurrentBar.ToString());
			}
			
			#endregion
			
			trend[0] = trend[1];
			bool isBullSignal = ShowBull && Low[3] > High[1] && Close[2] < Low[3]  && Close[0] > Low[3]  && Filter(Low[3], High[1]);
			bool isBearSignal = ShowBear && Low[1] > High[3] && Close[2] > High[3] && Close[0] < High[3] && Filter(Low[1], High[3]);
			
			if(isBullSignal)
			{
				trend[0] = 1;
				double entry = (Low[3] + High[1]) / 2;
				
				BullSignal[0] = Low[0] - 2 * TickSize;
				if(ShowBullAvg) BullLine[0] = entry;
				
				if(ShowProfitStop)
				{
					double target = entry + indATR[0] * ProfitMultiple;
					double stop = entry - indATR[0] * StopMultiple;
					double ratio = Math.Abs(entry - target) / Math.Abs(entry - stop);
					Draw.RiskReward(this, "Stop / Target " + CurrentBar.ToString(), false, 0, entry, 0, stop, ratio, true);
				}
			}
			else
			{
				if(ShowBullAvg)
				{
					if(BullLine.IsValidDataPoint(1) && Close[0] >= BullLine[1])
						BullLine[0] = BullLine[1];
				}
			}
			
			if(isBearSignal)
			{
				trend[0] = -1;
				double entry = (Low[1] + High[3]) / 2;
				
				BearSignal[0] = High[0] + 2 * TickSize;
			    if(ShowBearAvg) BearLine[0] = entry;
				
				if(ShowProfitStop)
				{
					double target = entry - indATR[0] * ProfitMultiple;
					double stop = entry + indATR[0] * StopMultiple;
					double ratio = Math.Abs(entry - target) / Math.Abs(entry - stop);
					Draw.RiskReward(this, "Stop / Target " + CurrentBar.ToString(), false, 0, entry, 0, stop, ratio, true);
				}
			}
			else
			{
				if(ShowBearAvg)
				{
					if(BearLine.IsValidDataPoint(1) && Close[0] <= BearLine[1])
						BearLine[0] = BearLine[1];
				}
			}
			
			// Trailing
			bool trigger = EveryOrInverse ? (isBullSignal || isBearSignal) : trend[0] != trend[1];

		    if(trigger)
			{
		        if(trend[0] > 0) TrailingStop = Close[0] - indATR[0] * TrailingMultiple;
				if(trend[0] < 0) TrailingStop = Close[0] + indATR[0] * TrailingMultiple;
				TrailingStopReached = false;
			}
		    else
			{
				if(trend[0] > 0) TrailingStop = Close[0] - TrailingStop > indATR[0] * TrailingMultiple ? Close[0] - indATR[0] * TrailingMultiple : TrailingStop;
				if(trend[0] < 0) TrailingStop = TrailingStop - Close[0] > indATR[0] * TrailingMultiple ? Close[0] + indATR[0] * TrailingMultiple : TrailingStop;

		        if(Close[0] < TrailingStop && trend[0] == 1)
		            TrailingStopReached = true;
		        else if(Close[0] > TrailingStop && trend[0] == -1)
		            TrailingStopReached = true;
			}
			
			if(!TrailingStopReached)
			{
				if(trend[0] > 0) BullTrailing[0] = TrailingStop;
				if(trend[0] < 0) BearTrailing[0] = TrailingStop;
			}
		}
		
		private bool Filter(double low, double high)
		{
			return low - high > indATR[0] * FilterWidth;
		}
		
		[Browsable(false)][XmlIgnore] public Series<double> BullSignal 		{ get { return Values[0]; } }
		[Browsable(false)][XmlIgnore] public Series<double> BearSignal 		{ get { return Values[1]; } }
		[Browsable(false)][XmlIgnore] public Series<double> BullLine   		{ get { return Values[2]; } }
		[Browsable(false)][XmlIgnore] public Series<double> BearLine   		{ get { return Values[3]; } }
		[Browsable(false)][XmlIgnore] public Series<double> BullTrailing   	{ get { return Values[4]; } }
		[Browsable(false)][XmlIgnore] public Series<double> BearTrailing	{ get { return Values[5]; } }
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private indTradingView.FvgInstantaneousMitigationSignals[] cacheFvgInstantaneousMitigationSignals;
		public indTradingView.FvgInstantaneousMitigationSignals FvgInstantaneousMitigationSignals(bool showBull, bool showBear, bool showBullAvg, bool showBearAvg, double filterWidth, bool everyOrInverse, double trailingMultiple, bool showProfitStop, double profitMultiple, double stopMultiple)
		{
			return FvgInstantaneousMitigationSignals(Input, showBull, showBear, showBullAvg, showBearAvg, filterWidth, everyOrInverse, trailingMultiple, showProfitStop, profitMultiple, stopMultiple);
		}

		public indTradingView.FvgInstantaneousMitigationSignals FvgInstantaneousMitigationSignals(ISeries<double> input, bool showBull, bool showBear, bool showBullAvg, bool showBearAvg, double filterWidth, bool everyOrInverse, double trailingMultiple, bool showProfitStop, double profitMultiple, double stopMultiple)
		{
			if (cacheFvgInstantaneousMitigationSignals != null)
				for (int idx = 0; idx < cacheFvgInstantaneousMitigationSignals.Length; idx++)
					if (cacheFvgInstantaneousMitigationSignals[idx] != null && cacheFvgInstantaneousMitigationSignals[idx].ShowBull == showBull && cacheFvgInstantaneousMitigationSignals[idx].ShowBear == showBear && cacheFvgInstantaneousMitigationSignals[idx].ShowBullAvg == showBullAvg && cacheFvgInstantaneousMitigationSignals[idx].ShowBearAvg == showBearAvg && cacheFvgInstantaneousMitigationSignals[idx].FilterWidth == filterWidth && cacheFvgInstantaneousMitigationSignals[idx].EveryOrInverse == everyOrInverse && cacheFvgInstantaneousMitigationSignals[idx].TrailingMultiple == trailingMultiple && cacheFvgInstantaneousMitigationSignals[idx].ShowProfitStop == showProfitStop && cacheFvgInstantaneousMitigationSignals[idx].ProfitMultiple == profitMultiple && cacheFvgInstantaneousMitigationSignals[idx].StopMultiple == stopMultiple && cacheFvgInstantaneousMitigationSignals[idx].EqualsInput(input))
						return cacheFvgInstantaneousMitigationSignals[idx];
			return CacheIndicator<indTradingView.FvgInstantaneousMitigationSignals>(new indTradingView.FvgInstantaneousMitigationSignals(){ ShowBull = showBull, ShowBear = showBear, ShowBullAvg = showBullAvg, ShowBearAvg = showBearAvg, FilterWidth = filterWidth, EveryOrInverse = everyOrInverse, TrailingMultiple = trailingMultiple, ShowProfitStop = showProfitStop, ProfitMultiple = profitMultiple, StopMultiple = stopMultiple }, input, ref cacheFvgInstantaneousMitigationSignals);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.FvgInstantaneousMitigationSignals FvgInstantaneousMitigationSignals(bool showBull, bool showBear, bool showBullAvg, bool showBearAvg, double filterWidth, bool everyOrInverse, double trailingMultiple, bool showProfitStop, double profitMultiple, double stopMultiple)
		{
			return indicator.FvgInstantaneousMitigationSignals(Input, showBull, showBear, showBullAvg, showBearAvg, filterWidth, everyOrInverse, trailingMultiple, showProfitStop, profitMultiple, stopMultiple);
		}

		public Indicators.indTradingView.FvgInstantaneousMitigationSignals FvgInstantaneousMitigationSignals(ISeries<double> input , bool showBull, bool showBear, bool showBullAvg, bool showBearAvg, double filterWidth, bool everyOrInverse, double trailingMultiple, bool showProfitStop, double profitMultiple, double stopMultiple)
		{
			return indicator.FvgInstantaneousMitigationSignals(input, showBull, showBear, showBullAvg, showBearAvg, filterWidth, everyOrInverse, trailingMultiple, showProfitStop, profitMultiple, stopMultiple);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.FvgInstantaneousMitigationSignals FvgInstantaneousMitigationSignals(bool showBull, bool showBear, bool showBullAvg, bool showBearAvg, double filterWidth, bool everyOrInverse, double trailingMultiple, bool showProfitStop, double profitMultiple, double stopMultiple)
		{
			return indicator.FvgInstantaneousMitigationSignals(Input, showBull, showBear, showBullAvg, showBearAvg, filterWidth, everyOrInverse, trailingMultiple, showProfitStop, profitMultiple, stopMultiple);
		}

		public Indicators.indTradingView.FvgInstantaneousMitigationSignals FvgInstantaneousMitigationSignals(ISeries<double> input , bool showBull, bool showBear, bool showBullAvg, bool showBearAvg, double filterWidth, bool everyOrInverse, double trailingMultiple, bool showProfitStop, double profitMultiple, double stopMultiple)
		{
			return indicator.FvgInstantaneousMitigationSignals(input, showBull, showBear, showBullAvg, showBearAvg, filterWidth, everyOrInverse, trailingMultiple, showProfitStop, profitMultiple, stopMultiple);
		}
	}
}

#endregion
