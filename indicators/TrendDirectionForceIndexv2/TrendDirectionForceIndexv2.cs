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

// NT8 Version of Trend Direction Force Index v2 - TDFI [wm]
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the Mozilla Public License 2.0 (MPL 2.0).
// The original Pine Script™ code is by causecelebre and can be found at: https://www.tradingview.com/script/HUpIful1-Trend-Direction-Force-Index-v2-TDFI-wm/
// Adaptation for NinjaTrader by jack@mydailytake.com
// Write-up + downloads: https://mydailytake.com/tradingview-trend-direction-force-index-v2-tdfi-wm-to-ninjatrader-8/
// Source + all conversions: https://github.com/MyDailyTake/tradingview-to-ninjatrader-8
// © 2024 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the Mozilla Public License 2.0. Full license details at https://mozilla.org/MPL/2.0/
// The use of causecelebre name or its adapted code in this work does not imply endorsement by the original authors.

#region enums TrendDirectionForceIndexv2

public enum TrendDirectionForceIndexv2_MaType
{
	EMA,
	WMA,
	VWMA,
	Hull,
	TEMA,
}

#endregion

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	#region Categories
	
	[Gui.CategoryOrder("Indicator Setup", 				10100)]
	[Gui.CategoryOrder("Display Inputs", 				10300)]
	
	#endregion
	
	public class TrendDirectionForceIndexv2 : Indicator
	{
		#region indInfo
		
		private string indName = "Trend Direction Force Index v2 - TDFI [wm]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by causecelebre can be found here: https://www.tradingview.com/script/HUpIful1-Trend-Direction-Force-Index-v2-TDFI-wm/";
		
		#endregion
		
		#region Properties

	    // Indicator Setup
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 01, GroupName = "Indicator Setup", Name = "Lookback", Description = "Lookback period.")]
		public int Lookback { get; set; }
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 02, GroupName = "Indicator Setup", Name = "MMA Length", Description = "MMA Length period.")]
		public int MmaLength { get; set; }
		
		[NinjaScriptProperty]
		[Display(Order = 03, GroupName = "Indicator Setup", Name = "MMA Mode", Description = "MMA Mode selection.")]
		public TrendDirectionForceIndexv2_MaType MmaMode { get; set; }
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 04, GroupName = "Indicator Setup", Name = "SMMA Length", Description = "SMMA Length period.")]
		public int SmmaLength { get; set; }
		
		[NinjaScriptProperty]
		[Display(Order = 05, GroupName = "Indicator Setup", Name = "SMMA Mode", Description = "SMMA Mode selection.")]
		public TrendDirectionForceIndexv2_MaType SmmaMode { get; set; }
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 06, GroupName = "Indicator Setup", Name = "N Length", Description = "N Length period.")]
		public int NLength { get; set; }
			
		// Display Inputs
		[XmlIgnore()]
	    [Display(Order = 01, GroupName = "Display Inputs", Name = "Color Up", Description = "")]
	    public Brush ColorUp { get; set; }
			[Browsable(false)]
			public string ColorUpSerialize
			{
			    get { return Serialize.BrushToString(ColorUp); }
			    set { ColorUp = Serialize.StringToBrush(value); }
			}
		
		[XmlIgnore()]
	    [Display(Order = 02, GroupName = "Display Inputs", Name = "Color Down", Description = "")]
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
		private Series<double> price, absTDF;
		private ISeries<double> mma, smma; 
		private MAX maxAbsTDF;
			
		private bool isOnPriceChange;
			
		#endregion
		
		#region OnStateChange
			
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
				
		        Lookback = 13;
		        MmaLength = 13;
		        MmaMode = TrendDirectionForceIndexv2_MaType.EMA;
		        SmmaLength = 13;
		        SmmaMode = TrendDirectionForceIndexv2_MaType.EMA;
		        NLength = 3;
		        
				ColorUp = Brushes.DodgerBlue;
				ColorDown = Brushes.Firebrick;
				
				AddPlot(new Stroke(Brushes.DimGray, DashStyleHelper.Solid, 3f), PlotStyle.Line, "TDFI");
				
				AddLine(new Stroke(Brushes.DimGray, DashStyleHelper.Solid, 2f),  0.05, "UpperLine");
				AddLine(new Stroke(Brushes.DimGray, DashStyleHelper.Solid, 2f), -0.05, "LowerLine");
			}
			else if (State == State.DataLoaded)
			{
				price = new Series<double>(this);
				absTDF = new Series<double>(this);
				
				mma = GetMA(MmaMode, price, MmaLength);
		    	smma = GetMA(SmmaMode, mma, SmmaLength);
				
				maxAbsTDF = MAX(absTDF, (Lookback * NLength));
				
				maxPeriod = Math.Max(Lookback * NLength, Math.Max(MmaLength, SmmaLength));
				
				if(Calculate == Calculate.OnEachTick)
					Calculate = Calculate.OnPriceChange;
				isOnPriceChange = Calculate == Calculate.OnPriceChange;
			}
		}
		
		#endregion

		protected override void OnBarUpdate()
		{
			#region Pre-Process
			
			if(CurrentBar < 0)
				return;
			
			if(isOnPriceChange)
			{
				for(int i = 0; i <= Values.Length-1; i++)
					Values[i].Reset();
			}
			
			price[0] = Input[0] * 1000;
			
			if(CurrentBar <= maxPeriod)
				return;
			
			#endregion
			
			TDFI[0] = GetTDFI();
			if(TDFI[0] > Lines[0].Value) PlotBrushes[0][0] = ColorUp;
			if(TDFI[0] < Lines[1].Value) PlotBrushes[0][0] = ColorDown;
		}
		
		private double GetTDFI()
		{
		    double impetmma = mma[0] - mma[1];
		    double impetsmma = smma[0] - smma[1];
		    double divma = Math.Abs(mma[0] - smma[0]);
		    double averimpet = (impetmma + impetsmma) / 2;
		    double tdf = Math.Pow(divma, 1) * Math.Pow(averimpet, NLength);
		    absTDF[0] = Math.Abs(tdf);
			
			return tdf / maxAbsTDF[0];
		}
		
		private ISeries<double> GetMA(TrendDirectionForceIndexv2_MaType mode, ISeries<double> src, int len)
		{
			switch (mode)
            {
                case TrendDirectionForceIndexv2_MaType.EMA:
                    return EMA(src, len);
                case TrendDirectionForceIndexv2_MaType.WMA:
                    return WMA(src, len);
                case TrendDirectionForceIndexv2_MaType.VWMA:
                    return VWMA(src, len);
                case TrendDirectionForceIndexv2_MaType.Hull:
                    return HMA(src, len);
                case TrendDirectionForceIndexv2_MaType.TEMA:
                    return TEMA(src, len);
                default:
                    return SMA(src, len);
            }
		}
		
		[Browsable(false)][XmlIgnore] public Series<double> TDFI { get { return Values[0]; } }
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private indTradingView.TrendDirectionForceIndexv2[] cacheTrendDirectionForceIndexv2;
		public indTradingView.TrendDirectionForceIndexv2 TrendDirectionForceIndexv2(int lookback, int mmaLength, TrendDirectionForceIndexv2_MaType mmaMode, int smmaLength, TrendDirectionForceIndexv2_MaType smmaMode, int nLength)
		{
			return TrendDirectionForceIndexv2(Input, lookback, mmaLength, mmaMode, smmaLength, smmaMode, nLength);
		}

		public indTradingView.TrendDirectionForceIndexv2 TrendDirectionForceIndexv2(ISeries<double> input, int lookback, int mmaLength, TrendDirectionForceIndexv2_MaType mmaMode, int smmaLength, TrendDirectionForceIndexv2_MaType smmaMode, int nLength)
		{
			if (cacheTrendDirectionForceIndexv2 != null)
				for (int idx = 0; idx < cacheTrendDirectionForceIndexv2.Length; idx++)
					if (cacheTrendDirectionForceIndexv2[idx] != null && cacheTrendDirectionForceIndexv2[idx].Lookback == lookback && cacheTrendDirectionForceIndexv2[idx].MmaLength == mmaLength && cacheTrendDirectionForceIndexv2[idx].MmaMode == mmaMode && cacheTrendDirectionForceIndexv2[idx].SmmaLength == smmaLength && cacheTrendDirectionForceIndexv2[idx].SmmaMode == smmaMode && cacheTrendDirectionForceIndexv2[idx].NLength == nLength && cacheTrendDirectionForceIndexv2[idx].EqualsInput(input))
						return cacheTrendDirectionForceIndexv2[idx];
			return CacheIndicator<indTradingView.TrendDirectionForceIndexv2>(new indTradingView.TrendDirectionForceIndexv2(){ Lookback = lookback, MmaLength = mmaLength, MmaMode = mmaMode, SmmaLength = smmaLength, SmmaMode = smmaMode, NLength = nLength }, input, ref cacheTrendDirectionForceIndexv2);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.TrendDirectionForceIndexv2 TrendDirectionForceIndexv2(int lookback, int mmaLength, TrendDirectionForceIndexv2_MaType mmaMode, int smmaLength, TrendDirectionForceIndexv2_MaType smmaMode, int nLength)
		{
			return indicator.TrendDirectionForceIndexv2(Input, lookback, mmaLength, mmaMode, smmaLength, smmaMode, nLength);
		}

		public Indicators.indTradingView.TrendDirectionForceIndexv2 TrendDirectionForceIndexv2(ISeries<double> input , int lookback, int mmaLength, TrendDirectionForceIndexv2_MaType mmaMode, int smmaLength, TrendDirectionForceIndexv2_MaType smmaMode, int nLength)
		{
			return indicator.TrendDirectionForceIndexv2(input, lookback, mmaLength, mmaMode, smmaLength, smmaMode, nLength);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.TrendDirectionForceIndexv2 TrendDirectionForceIndexv2(int lookback, int mmaLength, TrendDirectionForceIndexv2_MaType mmaMode, int smmaLength, TrendDirectionForceIndexv2_MaType smmaMode, int nLength)
		{
			return indicator.TrendDirectionForceIndexv2(Input, lookback, mmaLength, mmaMode, smmaLength, smmaMode, nLength);
		}

		public Indicators.indTradingView.TrendDirectionForceIndexv2 TrendDirectionForceIndexv2(ISeries<double> input , int lookback, int mmaLength, TrendDirectionForceIndexv2_MaType mmaMode, int smmaLength, TrendDirectionForceIndexv2_MaType smmaMode, int nLength)
		{
			return indicator.TrendDirectionForceIndexv2(input, lookback, mmaLength, mmaMode, smmaLength, smmaMode, nLength);
		}
	}
}

#endregion
