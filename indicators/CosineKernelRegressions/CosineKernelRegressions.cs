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

// NT8 Version of Cosine Kernel Regressions
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the Mozilla Public License 2.0 (MPL 2.0). 
// The original Pine Script™ code is by QuantraSystems and can be found at: https://www.tradingview.com/script/wgTxuL34-Cosine-Kernel-Regressions-QuantraSystems/
// Adaptation for NinjaTrader by jack@mydailytake.com
// Write-up + downloads: https://mydailytake.com/tradingview-cosine-kernel-regressions-quantrasystems-conversion-to-ninjatrader-8/
// Source + all conversions: https://github.com/MyDailyTake/tradingview-to-ninjatrader-8
// © [Current Year] MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the Mozilla Public License 2.0. Full license details at https://mozilla.org/MPL/2.0/
// The use of QuantraSystems' name or its adapted code in this work does not imply endorsement by the original authors.

#region CosineKernelRegressions_ENUM

public enum CosineKernelRegressions_ColType
{
	None,
	FastTrend,
	SlowTrend
}

public enum CosineKernelRegressions_Varient
{
	Tuneable,
	Stepped,
}

public enum CosineKernelRegressions_ColMode
{
    Classic,
    Modern,
    Robust,
    Accented,
    Monochrome
}

#endregion

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	#region Categories
	
	[Gui.CategoryOrder("Indicator Setup", 				10100)]
	[Gui.CategoryOrder("Kernel Calibration", 			10200)]
	[Gui.CategoryOrder("Display", 						10300)]
	
	#endregion
	
	public class CosineKernelRegressions : Indicator
	{
		#region indInfo
		
		private string indName = "Cosine Kernel Regressions [QuantraSystems]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by QuantraSystems can be found here: https://www.tradingview.com/script/wgTxuL34-Cosine-Kernel-Regressions-QuantraSystems/";
		
		#endregion
		
		#region Properties

	    [NinjaScriptProperty]
	    [Display(Order = 02, GroupName = "Indicator Setup", Name = "STOCH", Description = "Enable STOCH indicator.")]
	    public bool Bool_STOCH { get; set; }

	    [NinjaScriptProperty]
	    [Display(Order = 03, GroupName = "Indicator Setup", Name = "RSI", Description = "Enable RSI indicator.")]
	    public bool Bool_RSI { get; set; }

	    [NinjaScriptProperty]
	    [Display(Order = 04, GroupName = "Indicator Setup", Name = "BBPCT", Description = "Enable BBPCT indicator.")]
	    public bool Bool_BBPCT { get; set; }

	    [NinjaScriptProperty]
	    [Display(Order = 05, GroupName = "Indicator Setup", Name = "CMO", Description = "Enable CMO indicator.")]
	    public bool Bool_CMO { get; set; }

	    [NinjaScriptProperty]
	    [Display(Order = 06, GroupName = "Indicator Setup", Name = "CCI", Description = "Enable CCI indicator.")]
	    public bool Bool_CCI { get; set; }

	    [NinjaScriptProperty]
	    [Display(Order = 07, GroupName = "Indicator Setup", Name = "FISH", Description = "Enable FISH indicator.")]
	    public bool Bool_FISH { get; set; }

	    [NinjaScriptProperty]
	    [Display(Order = 08, GroupName = "Indicator Setup", Name = "VZO", Description = "Enable VZO indicator.")]
	    public bool Bool_VZO { get; set; }

	    [NinjaScriptProperty]
	    [Display(Order = 09, GroupName = "Indicator Setup", Name = "Stochastic Length", Description = "Stochastic Length")]
	    public int Length_STOCH { get; set; }

	    [NinjaScriptProperty]
	    [Display(Order = 10, GroupName = "Indicator Setup", Name = "RSI Length", Description = "RSI Length")]
	    public int Length_RSI { get; set; }

	    [NinjaScriptProperty]
	    [Display(Order = 11, GroupName = "Indicator Setup", Name = "BBPCT Length", Description = "BBPCT Length")]
	    public int Length_BBPCT { get; set; }

	    [NinjaScriptProperty]
	    [Display(Order = 12, GroupName = "Indicator Setup", Name = "Chande Momentum Length", Description = "Chande Momentum Length")]
	    public int Length_CMO { get; set; }

	    [NinjaScriptProperty]
	    [Display(Order = 13, GroupName = "Indicator Setup", Name = "CCI Length", Description = "CCI Length")]
	    public int Length_CCI { get; set; }

	    [NinjaScriptProperty]
	    [Display(Order = 14, GroupName = "Indicator Setup", Name = "Fisher Transform Length", Description = "Fisher Transform Length")]
	    public int Length_FISH { get; set; }

	    [NinjaScriptProperty]
	    [Display(Order = 15, GroupName = "Indicator Setup", Name = "VZO Length", Description = "VZO Length")]
	    public int Length_VZO { get; set; }

	    [NinjaScriptProperty]
	    [Display(Order = 01, GroupName = "Kernel Calibration", Name = "Varient", Description = "Type of Cosine Kernel Regression")]
	    public CosineKernelRegressions_Varient Varient { get; set; }

	    [NinjaScriptProperty]
	    [Display(Order = 02, GroupName = "Kernel Calibration", Name = "LookbackR", Description = "Regression Lookback")]
	    public int LookbackR { get; set; }

	    [NinjaScriptProperty]
	    [Display(Order = 03, GroupName = "Kernel Calibration", Name = "Tuning", Description = "Tuning Coefficient")]
	    public double Tuning { get; set; }

	    [Display(Order = 01, GroupName = "Display", Name = "ColType", Description = "Choose Mode")]
	    public CosineKernelRegressions_ColType ColType { get; set; }

	    [Display(Order = 02, GroupName = "Display", Name = "ColMode", Description = "Color Palette Choice")]
	    public CosineKernelRegressions_ColMode ColMode { get; set; }

	    [Display(Order = 03, GroupName = "Display", Name = "Man", Description = "Custom Palette")]
	    public bool Man { get; set; }

		[XmlIgnore()]
	    [Display(Order = 04, GroupName = "Display", Name = "ManUpC", Description = "Custom Up")]
	    public Brush ManUpC { get; set; }
			[Browsable(false)]
			public string ManUpCSerialize
			{
			    get { return Serialize.BrushToString(ManUpC); }
			    set { ManUpC = Serialize.StringToBrush(value); }
			}
			
		[XmlIgnore()]
	    [Display(Order = 05, GroupName = "Display", Name = "ManDnC", Description = "Custom Down")]
	    public Brush ManDnC { get; set; }
			[Browsable(false)]
			public string ManDnCSerialize
			{
			    get { return Serialize.BrushToString(ManDnC); }
			    set { ManDnC = Serialize.StringToBrush(value); }
			}
		
		#endregion
			
		#region Variables
			
		private int activeIndicators;
		private DynamicRSI val_RSI;
		private DynamicStoch val_STOCH;
		private DynamicBBPCT val_BBPCT;
		private DynamicCMO val_CMO;
		private DynamicCCI val_CCI;
		private DynamicFisher val_FISH;
		private DynamicVZO val_VZO;
			
		private Series<double> sumValues, outValue, outValue2;
		private DynamicALMA value;
			
		private Brush UpC;
		private Brush DnC;
		private Brush UpCol;
		private Brush DnCol;
		private Brush UpCol2;
		private Brush DnCol2;
			
		private bool isRealtime;
		private DateTime startFillTime;
			
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
				
		        Bool_STOCH = true;
		        Bool_RSI = true;
		        Bool_BBPCT = true;
		        Bool_CMO = true;
		        Bool_CCI = true;
		        Bool_FISH = true;
		        Bool_VZO = true;

		        Length_STOCH = 14;
		        Length_RSI = 14;
		        Length_BBPCT = 20;
		        Length_CMO = 14;
		        Length_CCI = 20;
		        Length_FISH = 9;
		        Length_VZO = 21;

		        Varient = CosineKernelRegressions_Varient.Tuneable;
		        LookbackR = 60;
		        Tuning = 15.0;

		        ColType = CosineKernelRegressions_ColType.FastTrend;
		        ColMode = CosineKernelRegressions_ColMode.Modern;
		        Man = false;
		        ManUpC = Brushes.Lime;
		        ManDnC = Brushes.Red;
				
				AddPlot(new Stroke(Brushes.DimGray, DashStyleHelper.Solid, 3f), PlotStyle.Line, "Sig");
				AddPlot(new Stroke(Brushes.DimGray, DashStyleHelper.Solid, 3f), PlotStyle.Line, "Sig2");
				
				AddLine(new Stroke(Brushes.Transparent, DashStyleHelper.Solid, 2f), 100.0, "UpperLine");
				AddLine(new Stroke(Brushes.Transparent, DashStyleHelper.Solid, 2f), 50.0, "UpperMidLine");
				AddLine(new Stroke(Brushes.DimGray, 	DashStyleHelper.Solid, 2f), 0.0, "MidLine");
				AddLine(new Stroke(Brushes.Transparent, DashStyleHelper.Solid, 2f), -50.0, "LowerMidLine");
				AddLine(new Stroke(Brushes.Transparent, DashStyleHelper.Solid, 2f), -100.0, "LowerLine");
			}
			else if (State == State.DataLoaded)
			{
				if(Bool_RSI) val_RSI = DynamicRSI(Input, Length_RSI);
				if(Bool_STOCH) val_STOCH = DynamicStoch(Input, Length_STOCH);
		        if(Bool_BBPCT) val_BBPCT = DynamicBBPCT(Input, Length_BBPCT, 2);
		        if(Bool_CMO) val_CMO = DynamicCMO(Input, Length_CMO);
		        if(Bool_CCI) val_CCI = DynamicCCI(Input, Length_CCI);
		        if(Bool_FISH) val_FISH = DynamicFisher(Input, Length_FISH);
		        if(Bool_VZO) val_VZO = DynamicVZO(Input, Length_VZO);
				
				activeIndicators = 
								  CountCondition(Bool_RSI  ) +
						          CountCondition(Bool_STOCH) +
						          CountCondition(Bool_BBPCT) +
						          CountCondition(Bool_CMO  ) +
						          CountCondition(Bool_CCI  ) +
						          CountCondition(Bool_FISH ) +
						          CountCondition(Bool_VZO  );
				
				sumValues = new Series<double>(this);
				outValue = new Series<double>(this);
				outValue2 = new Series<double>(this);
				value = DynamicALMA(sumValues, 9, 0, 6);
				
				switch (ColMode)
			    {
			        case CosineKernelRegressions_ColMode.Classic:
			            UpC = Brushes.LimeGreen;  // #00E676
			            DnC = Brushes.DarkRed;    // #880E4F
			            break;
			        case CosineKernelRegressions_ColMode.Modern:
			            UpC = new SolidColorBrush(Color.FromRgb(95, 250, 224)); // #5ffae0
			            DnC = new SolidColorBrush(Color.FromRgb(194, 46, 208)); // #c22ed0
			            break;
			        case CosineKernelRegressions_ColMode.Robust:
			            UpC = new SolidColorBrush(Color.FromRgb(255, 187, 0)); // #ffbb00
			            DnC = new SolidColorBrush(Color.FromRgb(119, 7, 55));  // #770737
			            break;
			        case CosineKernelRegressions_ColMode.Accented:
			            UpC = new SolidColorBrush(Color.FromRgb(150, 24, 247)); // #9618f7
			            DnC = new SolidColorBrush(Color.FromRgb(255, 0, 120));  // #ff0078
			            break;
			        case CosineKernelRegressions_ColMode.Monochrome:
			            UpC = new SolidColorBrush(Color.FromRgb(222, 226, 230)); // #dee2e6
			            DnC = new SolidColorBrush(Color.FromRgb(73, 80, 87));    // #495057
			            break;
			    }
				
				UpC.Freeze();
				DnC.Freeze();
				
				if (Man)
			    {
			        UpCol = ManUpC;
			        DnCol = ManDnC;
					
					UpCol2 = BrushFromArgb(80, ManUpC);
			        DnCol2 = BrushFromArgb(80, ManDnC);
			    }
			    else
			    {
			        UpCol = UpC;
			        DnCol = DnC;
					
					UpCol2 = BrushFromArgb(80, UpC);
			        DnCol2 = BrushFromArgb(80, DnC);
			    }
				
				UpCol.Freeze();
				DnCol.Freeze();
				UpCol2.Freeze();
				DnCol2.Freeze();
			}
			else if (State == State.Realtime)
			{
				if(Calculate != Calculate.OnBarClose)
					isRealtime = true;
			}
		}
		
		private int CountCondition(bool condition)
		{
		    return condition ? 1 : 0;
		}
		
		private static SolidColorBrush BrushFromArgb(int argb)
		{
		    return new SolidColorBrush(Color.FromArgb(
		        (byte)(argb >> 24),
		        (byte)(argb >> 16),
		        (byte)(argb >> 8),
		        (byte)(argb)));
		}

		private static SolidColorBrush BrushFromArgb(int alpha, Brush baseBrush)
		{
		    var brush = (SolidColorBrush)baseBrush;

		    return new SolidColorBrush(Color.FromArgb(
		        (byte)alpha,
		        brush.Color.R,
		        brush.Color.G,
		        brush.Color.B));
		}
		
		#endregion

		protected override void OnBarUpdate()
		{
			if (CurrentBar < 20) 
				return;
			
			// Calculate the average only with active indicators
			// Gentle ALMA smoothing
			double sum = 0;
			if(Bool_RSI) sum += val_RSI[0];
	        if(Bool_STOCH) sum += val_STOCH[0];
	        if(Bool_BBPCT) sum += val_BBPCT[0];
	        if(Bool_CMO) sum += val_CMO[0];
	        if(Bool_CCI) sum += val_CCI[0];
	        if(Bool_FISH) sum += val_FISH[0];
	        if(Bool_VZO) sum += val_VZO[0];
			sumValues[0] = sum / activeIndicators;
			
			// Calulate the Output - Depending on the method of Cosine Regression Selected
			if(Varient == CosineKernelRegressions_Varient.Tuneable)
			{
				outValue[0] = kernelRegression(value, LookbackR, Tuning);
				outValue2[0] = kernelRegression(value, LookbackR, Math.Round(Tuning / 5));
			}
			if(Varient == CosineKernelRegressions_Varient.Stepped)
			{
				outValue[0] = multiCosine(value, LookbackR, (int)Tuning);
				outValue2[0] = multiCosine(value, LookbackR, (int)Math.Round(Tuning / 5));
			}
			
			// Define Alert Conditions2
			bool fastTrend_up  = outValue[0] > outValue[1] && !(outValue[1] > outValue[2]);
			bool fastTrend_dn  = outValue[0] < outValue[1] && !(outValue[1] < outValue[2]);
			bool slowTrend_up  = outValue2[0] > 0 && !(outValue2[1] > 0);
			bool slowTrend_dn  = outValue2[0] < 0 && !(outValue2[1] < 0);
			bool overbought    = outValue[0] > 50 && !(outValue[1] > 50);
			bool oversold      = outValue[0] < -50 && !(outValue[1] < -50);

			bool fastTrend     = fastTrend_up || fastTrend_dn;
			bool slowTrend     = slowTrend_up || slowTrend_dn;
			
			// Visualization
			sig[0] = outValue[0];
			sig2[0] = outValue2[0];
			
			PlotBrushes[0][0] = outValue[0] > outValue[1] ? UpCol : DnCol;
			PlotBrushes[1][0] = outValue2[0] > 0 ? UpCol2 : DnCol2; 
			
			if(slowTrend) 
				startFillTime = Time[0];
			if(startFillTime != DateTime.MinValue)
				Draw.Region(this, startFillTime.ToString(), startFillTime, Time[0], sig2, Lines[2].Value, (outValue2[0] > 0 ? UpCol2 : DnCol2), 80);
			
			if(isRealtime)
			{
				RemoveDrawObject("fastTrend_dn " + CurrentBar.ToString());
				RemoveDrawObject("fastTrend_up " + CurrentBar.ToString());
			}
			
			if(fastTrend_dn)
				Draw.Dot(this, "fastTrend_dn " + CurrentBar.ToString(), false, 0, outValue[0], DnCol);
			if(fastTrend_up)
				Draw.Dot(this, "fastTrend_up " + CurrentBar.ToString(), false, 0, outValue[0], UpCol);
			
			if(ColType != CosineKernelRegressions_ColType.None)
			{
				if(ColType == CosineKernelRegressions_ColType.FastTrend)
				{
					BarBrushes[0] = outValue[0] > outValue[1] ? UpCol : DnCol;
					CandleOutlineBrushes[0] = outValue[0] > outValue[1] ? UpCol : DnCol;
				}
				if(ColType == CosineKernelRegressions_ColType.SlowTrend)
				{
					BarBrushes[0] = outValue2[0] > 0 ? UpCol : DnCol;
					CandleOutlineBrushes[0] = outValue2[0] > 0 ? UpCol : DnCol;
				}
			}
		}
		
		[Browsable(false)][XmlIgnore] public Series<double> sig  { get { return Values[0]; } }
		[Browsable(false)][XmlIgnore] public Series<double> sig2 { get { return Values[1]; } }
		
		#region COSINE KERNEL REGRESSIONS
		
		// Function to compute the cosine of an input scaled by a frequency tuner
		private double cosine(double x, double z)
		{
		    // Where x = source input
		    //       y = function output
		    //       z = frequency tuner
		    return Math.Cos(z * x);
		}

		// Function that utilizes the cosine function to create a kernel
		private double kernel(double x, double z)
		{
		    double y = cosine(x, z);
		    // cos(zx) = 0 when x = π / 2z
		    return Math.Abs(x) <= Math.PI / (2 * z) ? Math.Abs(y) : 0;
		}

		// Kernel Regression Function
		private double kernelRegression(ISeries<double> src, int lookback, double tuning)
		{
		    // ║ Initialize the variable for the current weight
		    double currentWeight = 0;
		    // ║ Initialize the variable for the sum of weights
		    double totalWeight = 0;

		    for (int i = 0; i < Math.Min(lookback, CurrentBar); i++)
		    {
		        double y = src[i];  // Get the source value at 'offset' i (i bars back)
		        double w = kernel(i / (double)lookback, tuning);  // Calculate the weight using the kernel function
		        currentWeight += y * w;  // Sum the weighted source values
		        totalWeight += w;  // Sum the individual weights
		    }

		    // ║ Divide the accumulated weighted values by the total weights
		    return currentWeight / totalWeight;
		}

		// Multi Cosine Regression Function
		private double multiCosine(ISeries<double> src, int lookback, int steps)
		{
		    // ║ Initialize the variable for the regression output
		    double regression = 0;

		    for (int i = 1; i <= Math.Min(steps - 1, CurrentBar); i++)
		    {
		        // Sum the regression values from kernelRegression at varying frequencies
		        regression += kernelRegression(src, lookback, i);
		    }

		    // ║ Divide the accumulated frequencies by the total number of steps
		    return regression / steps;
		}
		
		#endregion
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private indTradingView.CosineKernelRegressions[] cacheCosineKernelRegressions;
		public indTradingView.CosineKernelRegressions CosineKernelRegressions(bool bool_STOCH, bool bool_RSI, bool bool_BBPCT, bool bool_CMO, bool bool_CCI, bool bool_FISH, bool bool_VZO, int length_STOCH, int length_RSI, int length_BBPCT, int length_CMO, int length_CCI, int length_FISH, int length_VZO, CosineKernelRegressions_Varient varient, int lookbackR, double tuning)
		{
			return CosineKernelRegressions(Input, bool_STOCH, bool_RSI, bool_BBPCT, bool_CMO, bool_CCI, bool_FISH, bool_VZO, length_STOCH, length_RSI, length_BBPCT, length_CMO, length_CCI, length_FISH, length_VZO, varient, lookbackR, tuning);
		}

		public indTradingView.CosineKernelRegressions CosineKernelRegressions(ISeries<double> input, bool bool_STOCH, bool bool_RSI, bool bool_BBPCT, bool bool_CMO, bool bool_CCI, bool bool_FISH, bool bool_VZO, int length_STOCH, int length_RSI, int length_BBPCT, int length_CMO, int length_CCI, int length_FISH, int length_VZO, CosineKernelRegressions_Varient varient, int lookbackR, double tuning)
		{
			if (cacheCosineKernelRegressions != null)
				for (int idx = 0; idx < cacheCosineKernelRegressions.Length; idx++)
					if (cacheCosineKernelRegressions[idx] != null && cacheCosineKernelRegressions[idx].Bool_STOCH == bool_STOCH && cacheCosineKernelRegressions[idx].Bool_RSI == bool_RSI && cacheCosineKernelRegressions[idx].Bool_BBPCT == bool_BBPCT && cacheCosineKernelRegressions[idx].Bool_CMO == bool_CMO && cacheCosineKernelRegressions[idx].Bool_CCI == bool_CCI && cacheCosineKernelRegressions[idx].Bool_FISH == bool_FISH && cacheCosineKernelRegressions[idx].Bool_VZO == bool_VZO && cacheCosineKernelRegressions[idx].Length_STOCH == length_STOCH && cacheCosineKernelRegressions[idx].Length_RSI == length_RSI && cacheCosineKernelRegressions[idx].Length_BBPCT == length_BBPCT && cacheCosineKernelRegressions[idx].Length_CMO == length_CMO && cacheCosineKernelRegressions[idx].Length_CCI == length_CCI && cacheCosineKernelRegressions[idx].Length_FISH == length_FISH && cacheCosineKernelRegressions[idx].Length_VZO == length_VZO && cacheCosineKernelRegressions[idx].Varient == varient && cacheCosineKernelRegressions[idx].LookbackR == lookbackR && cacheCosineKernelRegressions[idx].Tuning == tuning && cacheCosineKernelRegressions[idx].EqualsInput(input))
						return cacheCosineKernelRegressions[idx];
			return CacheIndicator<indTradingView.CosineKernelRegressions>(new indTradingView.CosineKernelRegressions(){ Bool_STOCH = bool_STOCH, Bool_RSI = bool_RSI, Bool_BBPCT = bool_BBPCT, Bool_CMO = bool_CMO, Bool_CCI = bool_CCI, Bool_FISH = bool_FISH, Bool_VZO = bool_VZO, Length_STOCH = length_STOCH, Length_RSI = length_RSI, Length_BBPCT = length_BBPCT, Length_CMO = length_CMO, Length_CCI = length_CCI, Length_FISH = length_FISH, Length_VZO = length_VZO, Varient = varient, LookbackR = lookbackR, Tuning = tuning }, input, ref cacheCosineKernelRegressions);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.CosineKernelRegressions CosineKernelRegressions(bool bool_STOCH, bool bool_RSI, bool bool_BBPCT, bool bool_CMO, bool bool_CCI, bool bool_FISH, bool bool_VZO, int length_STOCH, int length_RSI, int length_BBPCT, int length_CMO, int length_CCI, int length_FISH, int length_VZO, CosineKernelRegressions_Varient varient, int lookbackR, double tuning)
		{
			return indicator.CosineKernelRegressions(Input, bool_STOCH, bool_RSI, bool_BBPCT, bool_CMO, bool_CCI, bool_FISH, bool_VZO, length_STOCH, length_RSI, length_BBPCT, length_CMO, length_CCI, length_FISH, length_VZO, varient, lookbackR, tuning);
		}

		public Indicators.indTradingView.CosineKernelRegressions CosineKernelRegressions(ISeries<double> input , bool bool_STOCH, bool bool_RSI, bool bool_BBPCT, bool bool_CMO, bool bool_CCI, bool bool_FISH, bool bool_VZO, int length_STOCH, int length_RSI, int length_BBPCT, int length_CMO, int length_CCI, int length_FISH, int length_VZO, CosineKernelRegressions_Varient varient, int lookbackR, double tuning)
		{
			return indicator.CosineKernelRegressions(input, bool_STOCH, bool_RSI, bool_BBPCT, bool_CMO, bool_CCI, bool_FISH, bool_VZO, length_STOCH, length_RSI, length_BBPCT, length_CMO, length_CCI, length_FISH, length_VZO, varient, lookbackR, tuning);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.CosineKernelRegressions CosineKernelRegressions(bool bool_STOCH, bool bool_RSI, bool bool_BBPCT, bool bool_CMO, bool bool_CCI, bool bool_FISH, bool bool_VZO, int length_STOCH, int length_RSI, int length_BBPCT, int length_CMO, int length_CCI, int length_FISH, int length_VZO, CosineKernelRegressions_Varient varient, int lookbackR, double tuning)
		{
			return indicator.CosineKernelRegressions(Input, bool_STOCH, bool_RSI, bool_BBPCT, bool_CMO, bool_CCI, bool_FISH, bool_VZO, length_STOCH, length_RSI, length_BBPCT, length_CMO, length_CCI, length_FISH, length_VZO, varient, lookbackR, tuning);
		}

		public Indicators.indTradingView.CosineKernelRegressions CosineKernelRegressions(ISeries<double> input , bool bool_STOCH, bool bool_RSI, bool bool_BBPCT, bool bool_CMO, bool bool_CCI, bool bool_FISH, bool bool_VZO, int length_STOCH, int length_RSI, int length_BBPCT, int length_CMO, int length_CCI, int length_FISH, int length_VZO, CosineKernelRegressions_Varient varient, int lookbackR, double tuning)
		{
			return indicator.CosineKernelRegressions(input, bool_STOCH, bool_RSI, bool_BBPCT, bool_CMO, bool_CCI, bool_FISH, bool_VZO, length_STOCH, length_RSI, length_BBPCT, length_CMO, length_CCI, length_FISH, length_VZO, varient, lookbackR, tuning);
		}
	}
}

#endregion
