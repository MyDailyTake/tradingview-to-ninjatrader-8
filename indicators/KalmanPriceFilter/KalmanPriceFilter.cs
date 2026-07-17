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

// NT8 Version of Kalman Price Filter
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the Mozilla Public License 2.0 (MPL 2.0).
// The original Pine Script™ code is by BackQuant and can be found at: https://www.tradingview.com/script/3N2zym2w-Kalman-Price-Filter-BackQuant/
// Adaptation for NinjaTrader by jack@mydailytake.com
// Write-up + downloads: https://mydailytake.com/tradingview-kalman-price-filter-backquant-conversion-to-ninjatrader-8/
// Source + all conversions: https://github.com/MyDailyTake/tradingview-to-ninjatrader-8
// © 2024 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the Mozilla Public License 2.0. Full license details at https://mozilla.org/MPL/2.0/
// The use of BackQuant's name or its adapted code in this work does not imply endorsement by the original authors.

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	public class KalmanPriceFilter : Indicator
	{
		#region indInfo
		
		private string indName = "Kalman Price Filter [BackQuant]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by BackQuant can be found here: https://www.tradingview.com/script/3N2zym2w-Kalman-Price-Filter-BackQuant/";
		
		#endregion
		
		#region Properties
		
		[NinjaScriptProperty]
		[Range(0.01, double.MaxValue)]
		[Display(Order = 01, GroupName = "Calculation", Name = "Process Noise", Description = "Set the process noise level.")]
		public double ProcessNoise { get; set; }
		
		[NinjaScriptProperty]
		[Range(0.1, double.MaxValue)]
		[Display(Order = 02, GroupName = "Calculation", Name = "Measurement Noise", Description = "Set the measurement noise level.")]
		public double MeasurementNoise { get; set; }
		
		[NinjaScriptProperty]
		[Range(1, double.MaxValue)]
		[Display(Order = 03, GroupName = "Calculation", Name = "Filter Order", Description = "Set the order of the filter.")]
		public int FilterOrder { get; set; }
		
		[NinjaScriptProperty]
		[Display(Order = 04, GroupName = "UI Settings", Name = "Show Filtered Price on chart", Description = "Enable to show the filtered price on the chart.")]
		public bool ShowKalman { get; set; }
		
		[NinjaScriptProperty]
		[Display(Order = 05, GroupName = "UI Settings", Name = "Paint candles according to Trend", Description = "Enable to paint candles according to the trend detected by the Kalman filter.")]
		public bool PaintCandles { get; set; }
		
		#endregion
		
		#region Variables
		
		private double[] stateEstimate;
        private double[] errorCovariance;
		
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
				DrawOnPricePanel							= true;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				IsSuspendedWhileInactive					= true;
				
				AddPlot(new Stroke(Brushes.DimGray, DashStyleHelper.Solid, 3f), PlotStyle.Line, "Kalman");
				
				ProcessNoise = 0.01;
				MeasurementNoise = 3.0;
				FilterOrder = 5;
				ShowKalman = true;
				PaintCandles = true;
			}
			else if (State == State.DataLoaded)
			{
				stateEstimate = new double[FilterOrder];
                errorCovariance = new double[FilterOrder];
				for (int i = 0; i < FilterOrder; i++)
                {
                    stateEstimate[i] = double.NaN;
                    errorCovariance[i] = 100.0;
                }
			}
		}
		
		#endregion

		protected override void OnBarUpdate()
		{
			if (CurrentBar < FilterOrder)
                return;
			
            InitKalman(Input[0]);
            Kalman[0] = UpdateKalman(Input[0]);
			int trend = DetermineTrend(Kalman[0]);
			
            if (ShowKalman)
				PlotBrushes[0][0] = GetTrendBrush(trend);
			else 
				PlotBrushes[0][0] = Brushes.Transparent;
			
            if (PaintCandles)
            {
				BarBrushes[0] = GetTrendBrush(trend);
				CandleOutlineBrushes[0] = GetTrendBrush(trend);
            }
		}
		
		private void InitKalman(double priceSource)
        {
            for (int i = 0; i < FilterOrder; i++)
            {
                if (double.IsNaN(stateEstimate[i]))
                {
                    stateEstimate[i] = priceSource;
                    errorCovariance[i] = 1.0;
                }
            }
        }

        private double UpdateKalman(double priceSource)
        {
            double[] predictedStateEstimate = new double[FilterOrder];
            double[] predictedErrorCovariance = new double[FilterOrder];
            double[] kalmanGain = new double[FilterOrder];

            for (int i = 0; i < FilterOrder; i++)
            {
                predictedStateEstimate[i] = stateEstimate[i];
                predictedErrorCovariance[i] = errorCovariance[i] + ProcessNoise;

                double kg = predictedErrorCovariance[i] / (predictedErrorCovariance[i] + MeasurementNoise);
                kalmanGain[i] = kg;
                stateEstimate[i] = predictedStateEstimate[i] + kg * (priceSource - predictedStateEstimate[i]);
                errorCovariance[i] = (1 - kg) * predictedErrorCovariance[i];
            }

            return stateEstimate[0];
        }

        private int DetermineTrend(double kalmanFilteredPrice)
        {
            if (kalmanFilteredPrice > Values[0][1])
                return 1;
            if (kalmanFilteredPrice < Values[0][1])
                return -1;
            return 0;
        }
		
		private Brush GetTrendBrush(int trend)
		{
		    if (trend == 1)
		        return Brushes.Green;
		    else if (trend == -1)
		        return Brushes.Red;
		    else
		        return Brushes.White;
		}
		
		[Browsable(false)][XmlIgnore] public Series<double> Kalman { get { return Values[0]; } }
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private indTradingView.KalmanPriceFilter[] cacheKalmanPriceFilter;
		public indTradingView.KalmanPriceFilter KalmanPriceFilter(double processNoise, double measurementNoise, int filterOrder, bool showKalman, bool paintCandles)
		{
			return KalmanPriceFilter(Input, processNoise, measurementNoise, filterOrder, showKalman, paintCandles);
		}

		public indTradingView.KalmanPriceFilter KalmanPriceFilter(ISeries<double> input, double processNoise, double measurementNoise, int filterOrder, bool showKalman, bool paintCandles)
		{
			if (cacheKalmanPriceFilter != null)
				for (int idx = 0; idx < cacheKalmanPriceFilter.Length; idx++)
					if (cacheKalmanPriceFilter[idx] != null && cacheKalmanPriceFilter[idx].ProcessNoise == processNoise && cacheKalmanPriceFilter[idx].MeasurementNoise == measurementNoise && cacheKalmanPriceFilter[idx].FilterOrder == filterOrder && cacheKalmanPriceFilter[idx].ShowKalman == showKalman && cacheKalmanPriceFilter[idx].PaintCandles == paintCandles && cacheKalmanPriceFilter[idx].EqualsInput(input))
						return cacheKalmanPriceFilter[idx];
			return CacheIndicator<indTradingView.KalmanPriceFilter>(new indTradingView.KalmanPriceFilter(){ ProcessNoise = processNoise, MeasurementNoise = measurementNoise, FilterOrder = filterOrder, ShowKalman = showKalman, PaintCandles = paintCandles }, input, ref cacheKalmanPriceFilter);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.KalmanPriceFilter KalmanPriceFilter(double processNoise, double measurementNoise, int filterOrder, bool showKalman, bool paintCandles)
		{
			return indicator.KalmanPriceFilter(Input, processNoise, measurementNoise, filterOrder, showKalman, paintCandles);
		}

		public Indicators.indTradingView.KalmanPriceFilter KalmanPriceFilter(ISeries<double> input , double processNoise, double measurementNoise, int filterOrder, bool showKalman, bool paintCandles)
		{
			return indicator.KalmanPriceFilter(input, processNoise, measurementNoise, filterOrder, showKalman, paintCandles);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.KalmanPriceFilter KalmanPriceFilter(double processNoise, double measurementNoise, int filterOrder, bool showKalman, bool paintCandles)
		{
			return indicator.KalmanPriceFilter(Input, processNoise, measurementNoise, filterOrder, showKalman, paintCandles);
		}

		public Indicators.indTradingView.KalmanPriceFilter KalmanPriceFilter(ISeries<double> input , double processNoise, double measurementNoise, int filterOrder, bool showKalman, bool paintCandles)
		{
			return indicator.KalmanPriceFilter(input, processNoise, measurementNoise, filterOrder, showKalman, paintCandles);
		}
	}
}

#endregion
