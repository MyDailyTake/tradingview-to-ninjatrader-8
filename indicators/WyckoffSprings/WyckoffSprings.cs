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

// NT8 Version of Wyckoff Springs
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the Mozilla Public License 2.0 (MPL 2.0).
// The original Pine Script™ code is by QuantVue and can be found at: https://www.tradingview.com/script/cI1uqRnB-Wyckoff-Springs-QuantVue/
// Adaptation for NinjaTrader by jack@mydailytake.com
// Write-up + downloads: https://mydailytake.com/tradingview-wyckoff-springs-quantvue-conversion-to-ninjatrader-8/
// Source + all conversions: https://github.com/MyDailyTake/tradingview-to-ninjatrader-8
// © 2024 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the Mozilla Public License 2.0. Full license details at https://mozilla.org/MPL/2.0/
// The use of QuantVue name or its adapted code in this work does not imply endorsement by the original authors.

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	#region Categories
	
	[Gui.CategoryOrder("Indicator Setup", 				10100)]
	
	#endregion
	
	public class WyckoffSprings : Indicator
	{
		#region indInfo
		
		private string indName = "Wyckoff Springs [QuantVue]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by QuantVue can be found here: https://www.tradingview.com/script/cI1uqRnB-Wyckoff-Springs-QuantVue/";
		
		#endregion
		
		#region Properties

	    [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Order = 01, GroupName = "Indicator Setup", Name = "Pivot Length", Description = "Length of the pivot.")]
        public int PivotLength { get; set; }

        [NinjaScriptProperty]
        [Display(Order = 02, GroupName = "Indicator Setup", Name = "Require Volume Confirmation", Description = "Enable or disable volume confirmation.")]
        public bool RequireVolumeConfirmation { get; set; }

        [NinjaScriptProperty]
        [Range(0.0, 10.0)]
        [Display(Order = 03, GroupName = "Indicator Setup", Name = "Volume Threshold", Description = "Threshold for volume confirmation.")]
        public double VolumeThreshold { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Order = 04, GroupName = "Indicator Setup", Name = "Trading Range Period", Description = "Period for calculating the trading range.")]
        public int TradingRangePeriod { get; set; }
		
		#endregion
		
		#region PivotPoint
		
		private class PivotPoint
        {
            public double P { get; set; }
            public int B { get; set; }
            public bool Act { get; set; }
            public int C { get; set; }

            public PivotPoint(double p, int b)
            {
                P = p;
                B = b;
				Act = true;
				C = 0;
            }
        }
		
		#endregion
			
		#region Variables
		
		private Swing indSwing;
		private SMA indSmaVolume;
		private MAX indMax;
		private MIN indMin;
		
		private Series<double> swingLow;
		
		private List<PivotPoint> pivs;
		
		private int maxPeriod;
			
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
				
		        PivotLength = 6;
		        RequireVolumeConfirmation = false;
				VolumeThreshold = 1.5;
				TradingRangePeriod = 20;
				
				AddPlot(new Stroke(Brushes.Lime, DashStyleHelper.Solid, 8f), PlotStyle.TriangleUp, "Signal Up");
				AddPlot(new Stroke(Brushes.Firebrick, DashStyleHelper.Solid, 3f), PlotStyle.Line, "Range Low");
			}
			else if (State == State.DataLoaded)
			{
				indSwing = Swing(PivotLength);
				indSmaVolume = SMA(Volume, TradingRangePeriod);
				indMax = MAX(High, TradingRangePeriod);
				indMin = MIN(Low, TradingRangePeriod);
				
				swingLow = new Series<double>(this);
				
				pivs = new List<PivotPoint>();
				
				maxPeriod = Math.Max(PivotLength, TradingRangePeriod);
				if(Calculate != Calculate.OnBarClose)
					Calculate = Calculate.OnBarClose;
			}
		}
		
		#endregion

		protected override void OnBarUpdate()
		{
			#region Pre-Process
			
			if(CurrentBar < maxPeriod)
				return;
			
			swingLow[0] = indSwing.SwingLow[0];
			
			#endregion
			
			if(swingLow[0] != swingLow[1])
				pivs.Add(new PivotPoint(swingLow[0], CurrentBar));
				
			for (int i = 0; i < pivs.Count; i++)
            {
                PivotPoint p = pivs[i];
                if (p.Act)
                {
                    if (
						   Low[0] < p.P 
						&& Close[0] > p.P 
						&& p.C <= 3 
						&& Low[0] <= indMin[0] 
						&& (!RequireVolumeConfirmation || MeetsThreshold()))
                    {
                        SignalUp[0] = Low[0];
                        p.Act = false;
                    }
                    else if (Low[0] < p.P)
                    {
                        p.C += 1;
                    }
                }
			}
			
			RangeLow[0] = indMin[0];
		}
		
		private bool MeetsThreshold()
		{
			return Volume[0] >= indSmaVolume[0] * VolumeThreshold;
		}
		
		[Browsable(false)][XmlIgnore] public Series<double> SignalUp { get { return Values[0]; } }
		[Browsable(false)][XmlIgnore] public Series<double> RangeLow { get { return Values[1]; } }
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private indTradingView.WyckoffSprings[] cacheWyckoffSprings;
		public indTradingView.WyckoffSprings WyckoffSprings(int pivotLength, bool requireVolumeConfirmation, double volumeThreshold, int tradingRangePeriod)
		{
			return WyckoffSprings(Input, pivotLength, requireVolumeConfirmation, volumeThreshold, tradingRangePeriod);
		}

		public indTradingView.WyckoffSprings WyckoffSprings(ISeries<double> input, int pivotLength, bool requireVolumeConfirmation, double volumeThreshold, int tradingRangePeriod)
		{
			if (cacheWyckoffSprings != null)
				for (int idx = 0; idx < cacheWyckoffSprings.Length; idx++)
					if (cacheWyckoffSprings[idx] != null && cacheWyckoffSprings[idx].PivotLength == pivotLength && cacheWyckoffSprings[idx].RequireVolumeConfirmation == requireVolumeConfirmation && cacheWyckoffSprings[idx].VolumeThreshold == volumeThreshold && cacheWyckoffSprings[idx].TradingRangePeriod == tradingRangePeriod && cacheWyckoffSprings[idx].EqualsInput(input))
						return cacheWyckoffSprings[idx];
			return CacheIndicator<indTradingView.WyckoffSprings>(new indTradingView.WyckoffSprings(){ PivotLength = pivotLength, RequireVolumeConfirmation = requireVolumeConfirmation, VolumeThreshold = volumeThreshold, TradingRangePeriod = tradingRangePeriod }, input, ref cacheWyckoffSprings);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.WyckoffSprings WyckoffSprings(int pivotLength, bool requireVolumeConfirmation, double volumeThreshold, int tradingRangePeriod)
		{
			return indicator.WyckoffSprings(Input, pivotLength, requireVolumeConfirmation, volumeThreshold, tradingRangePeriod);
		}

		public Indicators.indTradingView.WyckoffSprings WyckoffSprings(ISeries<double> input , int pivotLength, bool requireVolumeConfirmation, double volumeThreshold, int tradingRangePeriod)
		{
			return indicator.WyckoffSprings(input, pivotLength, requireVolumeConfirmation, volumeThreshold, tradingRangePeriod);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.WyckoffSprings WyckoffSprings(int pivotLength, bool requireVolumeConfirmation, double volumeThreshold, int tradingRangePeriod)
		{
			return indicator.WyckoffSprings(Input, pivotLength, requireVolumeConfirmation, volumeThreshold, tradingRangePeriod);
		}

		public Indicators.indTradingView.WyckoffSprings WyckoffSprings(ISeries<double> input , int pivotLength, bool requireVolumeConfirmation, double volumeThreshold, int tradingRangePeriod)
		{
			return indicator.WyckoffSprings(input, pivotLength, requireVolumeConfirmation, volumeThreshold, tradingRangePeriod);
		}
	}
}

#endregion
