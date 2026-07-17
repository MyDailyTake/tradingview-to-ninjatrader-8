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

// NT8 Version of Median Proximity Percentile
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the Mozilla Public License 2.0 (MPL 2.0).
// The original Pine Script™ code is by AlgoAlpha and can be found at: https://www.tradingview.com/script/YEu4VVBj-Median-Proximity-Percentile-AlgoAlpha/
// Adaptation for NinjaTrader by jack@mydailytake.com
// Write-up + downloads: https://mydailytake.com/tradingview-median-proximity-percentile-algoalpha-conversion-to-ninjatrader-8/
// Source + all conversions: https://github.com/MyDailyTake/tradingview-to-ninjatrader-8
// © 2024 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the Mozilla Public License 2.0. Full license details at https://mozilla.org/MPL/2.0/
// The use of AlgoAlpha's name or its adapted code in this work does not imply endorsement by the original authors.

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	public class MedianProximityPercentile : Indicator
	{
		#region indInfo
		
		private string indName = "Median Proximity Percentile [AlgoAlpha]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by AlgoAlpha can be found here: https://www.tradingview.com/script/YEu4VVBj-Median-Proximity-Percentile-AlgoAlpha/";
		
		#endregion
		
		#region Properties
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 01, GroupName = "Parameters", Name = "Lookback Length", Description = "Set the lookback length for calculating the median.")]
		public int LookbackLength { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 02, GroupName = "Parameters", Name = "HMA Lookback Length", Description = "Set the lookback length for calculating the Hull Moving Average.")]
		public int EmaLookbackLength { get; set; }
		
		[NinjaScriptProperty]
		[Range(0.01, double.MaxValue)]
		[Display(Order = 03, GroupName = "Parameters", Name = "Standard Deviation Multiplier", Description = "Set the multiplier for the standard deviation calculation.")]
		public double StdDevMultiplier { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 04, GroupName = "Parameters", Name = "Noise Scatterplot", Description = "Enable or disable noise scatterplot.")]
		public bool ShowNoise { get; set; }
		
		[XmlIgnore()]
	    [Display(Order = 05, GroupName = "Parameters", Name = "Up Color", Description = "Color for upward movements.")]
	    public Brush ColorUp { get; set; }
			[Browsable(false)]
			public string ColorUpSerialize
			{
			    get { return Serialize.BrushToString(ColorUp); }
			    set { ColorUp = Serialize.StringToBrush(value); }
			}
			
		[XmlIgnore()]
	    [Display(Order = 06, GroupName = "Parameters", Name = "Down Color", Description = "Color for downward movements.")]
	    public Brush ColorDown { get; set; }
			[Browsable(false)]
			public string ColorDownSerialize
			{
			    get { return Serialize.BrushToString(ColorDown); }
			    set { ColorDown = Serialize.StringToBrush(value); }
			}
		
		#endregion
			
		#region Variables
			
		private int maxPeriod;
		private StdDev stdDevPriceDeviation;
		private StdDev stdDevPositiveValues;
		private StdDev stdDevNegativeValues;
		private EMA emaPositiveValues;
		private EMA emaNegativeValues;
		private HMA hmaPercentileValue;
		private Series<double> priceDeviation;
		private Series<double> normalizedValue;
		private Series<double> positiveValues;
        private Series<double> negativeValues;
		private Series<double> percentileValue;
		private Series<int> cloudTrend;
		private DateTime startFillTime;
			
		#endregion
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= indDescription;
				Name										= indName;
				Calculate									= Calculate.OnBarClose;
				IsOverlay									= false;
				DisplayInDataBox							= true;
				DrawOnPricePanel							= false;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				IsSuspendedWhileInactive					= true;
				
				AddPlot(new Stroke(Brushes.DimGray, DashStyleHelper.Solid, 2f), PlotStyle.Line, "HmaPlot");
				AddPlot(new Stroke(Brushes.DimGray, DashStyleHelper.Solid, 2f), PlotStyle.Line, "HmaPlot1");
				AddPlot(new Stroke(Brushes.DimGray, DashStyleHelper.Dot, 2f), PlotStyle.Dot, "Noise");
				
				AddLine(new Stroke(Brushes.Transparent, DashStyleHelper.Solid, 2f), 150.0, "UpperLine");
				AddLine(new Stroke(Brushes.Transparent, DashStyleHelper.Solid, 2f), 90.0, "UpperMidLine");
				AddLine(new Stroke(Brushes.DimGray, 	DashStyleHelper.Solid, 2f), 0.0, "MidLine");
				AddLine(new Stroke(Brushes.Transparent, DashStyleHelper.Solid, 2f), -90.0, "LowerMidLine");
				AddLine(new Stroke(Brushes.Transparent, DashStyleHelper.Solid, 2f), -150.0, "LowerLine");
				
				LookbackLength = 21;
        		EmaLookbackLength = 20;
				StdDevMultiplier = 1;
		        ShowNoise = true; 
		        ColorUp = Brushes.LightGreen;
		        ColorDown = Brushes.Red;
			}
			else if (State == State.DataLoaded)
			{
				maxPeriod = Math.Max(45, LookbackLength + EmaLookbackLength);
				priceDeviation = new Series<double>(this);
		        normalizedValue = new Series<double>(this);
		        positiveValues = new Series<double>(this);
		        negativeValues = new Series<double>(this);
		        percentileValue = new Series<double>(this);
				cloudTrend = new Series<int>(this);
				
				stdDevPriceDeviation = StdDev(priceDeviation, 45);
				stdDevPositiveValues = StdDev(positiveValues, LookbackLength);
				stdDevNegativeValues = StdDev(negativeValues, LookbackLength);
				emaPositiveValues = EMA(positiveValues, LookbackLength);
				emaNegativeValues = EMA(negativeValues, LookbackLength);
				hmaPercentileValue = HMA(percentileValue, EmaLookbackLength);
			}
		}

		protected override void OnBarUpdate()
		{
			if(CurrentBar < maxPeriod)
				return;
			
			cloudTrend[0] = cloudTrend[1];
			double medianValue = GetMedian(Input, LookbackLength);
			priceDeviation[0] = (Input[0] - medianValue);
			normalizedValue[0] = priceDeviation[0] / (stdDevPriceDeviation[0] + stdDevPriceDeviation[0]);
			
			positiveValues[0] = normalizedValue[0] > 0 ? normalizedValue[0] : 0;
			negativeValues[0] = normalizedValue[0] < 0 ? normalizedValue[0] : 0;
			
			double upperBoundary = emaPositiveValues[0] + stdDevPositiveValues[0] * StdDevMultiplier;
			double lowerBoundary = emaNegativeValues[0] - stdDevNegativeValues[0] * StdDevMultiplier;
			
			percentileValue[0] = 100 * (normalizedValue[0] - lowerBoundary) / (upperBoundary - lowerBoundary) - 50;
			
			HmaPlot[0] = hmaPercentileValue[0];
			HmaPlot1[0] = hmaPercentileValue[1];
			Noise[0] = percentileValue[0];
			
			if(HmaPlot[0] > HmaPlot[1])
			{
				cloudTrend[0] = 1;
				PlotBrushes[0][0] = PlotBrushes[1][0] = ColorUp;
			}
			else
			{
				cloudTrend[0] = -1;
				PlotBrushes[0][0] = PlotBrushes[1][0] = ColorDown;
			}
			
			if(ShowNoise)
				PlotBrushes[2][0] = percentileValue[0] > 0 ? ColorUp : (percentileValue[0] < 0 ? ColorDown : Brushes.Transparent);
			else 
				PlotBrushes[2][0] = Brushes.Transparent;
			
			if(cloudTrend[0] != cloudTrend[1])
			{
				startFillTime = Time[0];
				
				if (cloudTrend[0] > 0)
				{
			        Draw.Dot(this, "Bullish Swing " + CurrentBar.ToString(), false, 0, HmaPlot[0], ColorUp);
				    if (hmaPercentileValue[1] < Lines[3].Value)
				        Draw.ArrowUp(this, "BullishReversal" + CurrentBar, true, 0, HmaPlot[0] - TickSize, ColorUp);
				}
			    if (cloudTrend[0] < 0)
				{
					Draw.Dot(this, "Bearish Swing " + CurrentBar.ToString(), false, 0, HmaPlot[0], ColorDown);
					if (hmaPercentileValue[1] > Lines[1].Value)
				        Draw.ArrowDown(this, "BearishReversal" + CurrentBar, true, 0, HmaPlot[0] + TickSize, ColorDown);
				}
			}
			if(startFillTime != DateTime.MinValue)
				Draw.Region(this, startFillTime.ToString(), startFillTime, Time[0], HmaPlot, HmaPlot1, Brushes.Transparent, (cloudTrend[0] > 0 ? ColorUp : ColorDown), 40);
			
		}
		
		private double GetMedian(ISeries<double> series, int period)
        {
            double highest = double.MinValue;
            double lowest = double.MaxValue;

            for (int i = 0; i < period; i++)
            {
                double currentValue = series[i];
                if (currentValue > highest)
                    highest = currentValue;
                if (currentValue < lowest)
                    lowest = currentValue;
            }

            return (highest + lowest) / 2.0;
        }
		
		[Browsable(false)][XmlIgnore] public Series<double> HmaPlot  { get { return Values[0]; } }
		[Browsable(false)][XmlIgnore] public Series<double> HmaPlot1 { get { return Values[1]; } }
		[Browsable(false)][XmlIgnore] public Series<double> Noise    { get { return Values[2]; } }
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private indTradingView.MedianProximityPercentile[] cacheMedianProximityPercentile;
		public indTradingView.MedianProximityPercentile MedianProximityPercentile(int lookbackLength, int emaLookbackLength, double stdDevMultiplier, bool showNoise)
		{
			return MedianProximityPercentile(Input, lookbackLength, emaLookbackLength, stdDevMultiplier, showNoise);
		}

		public indTradingView.MedianProximityPercentile MedianProximityPercentile(ISeries<double> input, int lookbackLength, int emaLookbackLength, double stdDevMultiplier, bool showNoise)
		{
			if (cacheMedianProximityPercentile != null)
				for (int idx = 0; idx < cacheMedianProximityPercentile.Length; idx++)
					if (cacheMedianProximityPercentile[idx] != null && cacheMedianProximityPercentile[idx].LookbackLength == lookbackLength && cacheMedianProximityPercentile[idx].EmaLookbackLength == emaLookbackLength && cacheMedianProximityPercentile[idx].StdDevMultiplier == stdDevMultiplier && cacheMedianProximityPercentile[idx].ShowNoise == showNoise && cacheMedianProximityPercentile[idx].EqualsInput(input))
						return cacheMedianProximityPercentile[idx];
			return CacheIndicator<indTradingView.MedianProximityPercentile>(new indTradingView.MedianProximityPercentile(){ LookbackLength = lookbackLength, EmaLookbackLength = emaLookbackLength, StdDevMultiplier = stdDevMultiplier, ShowNoise = showNoise }, input, ref cacheMedianProximityPercentile);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.MedianProximityPercentile MedianProximityPercentile(int lookbackLength, int emaLookbackLength, double stdDevMultiplier, bool showNoise)
		{
			return indicator.MedianProximityPercentile(Input, lookbackLength, emaLookbackLength, stdDevMultiplier, showNoise);
		}

		public Indicators.indTradingView.MedianProximityPercentile MedianProximityPercentile(ISeries<double> input , int lookbackLength, int emaLookbackLength, double stdDevMultiplier, bool showNoise)
		{
			return indicator.MedianProximityPercentile(input, lookbackLength, emaLookbackLength, stdDevMultiplier, showNoise);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.MedianProximityPercentile MedianProximityPercentile(int lookbackLength, int emaLookbackLength, double stdDevMultiplier, bool showNoise)
		{
			return indicator.MedianProximityPercentile(Input, lookbackLength, emaLookbackLength, stdDevMultiplier, showNoise);
		}

		public Indicators.indTradingView.MedianProximityPercentile MedianProximityPercentile(ISeries<double> input , int lookbackLength, int emaLookbackLength, double stdDevMultiplier, bool showNoise)
		{
			return indicator.MedianProximityPercentile(input, lookbackLength, emaLookbackLength, stdDevMultiplier, showNoise);
		}
	}
}

#endregion
