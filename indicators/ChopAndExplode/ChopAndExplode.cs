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

// NT8 Version of Chop and explode (ps5)
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the Mozilla Public License 2.0 (MPL 2.0).
// The original Pine Script™ code is by capissimo and can be found at: https://www.tradingview.com/script/L7ydBiKM-Chop-and-explode-ps5/
// Adaptation for NinjaTrader by jack@mydailytake.com
// Write-up + downloads: https://mydailytake.com/chop-and-explode-capissimo-ninjatrader-8/
// Source + all conversions: https://github.com/MyDailyTake/tradingview-to-ninjatrader-8
// © 2026 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the Mozilla Public License 2.0. Full license details at https://mozilla.org/MPL/2.0/
// The use of capissimo's name or its adapted code in this work does not imply endorsement by the original author.
//
// Notes:
//   RSI applied to a minimax-scaled (1..100) source. Optional Ehlers high-pass + SuperSmoother data
//   "cleaning" pre-filter. RSI > 60 = BUY regime, RSI < 40 = SELL regime, in between holds the prior
//   signal. Tight chop is 45-55 (band), regular chop is 40-60 (wider band).
//
//   Documented scope decisions:
//     • Pine "Seasonal Random Index" (SRI) overlay — a secondary visualization layered on the same
//       panel — is omitted. The core RSI-of-minimax detection + signaling chain is preserved. Document
//       deviation: the SRI is a discussion-piece overlay; it doesn't affect the BUY/SELL signal.
//     • 2 alertconditions stripped per QC convention.
//     • Pine 10-shade color gradient for chop intensity collapsed to bull / bear / neutral pair —
//       NT's plot dialog handles single-color shading.
//
//   Non-repainting: cleaning filter and RSI use closed-bar history. Public Series outputs: Rsi,
//   Signal (1 / 0 / -1), StartedLong, StartedShort.

#region Enums ChopAndExplode

public enum ChopAndExplode_Source
{
	Close,
	HL2,
	HLC3,
	OHLC4,
	Open,
	High,
	Low
}

#endregion

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	#region Categories
	[Gui.CategoryOrder("Parameters",			10100)]
	[Gui.CategoryOrder("Signaling",		10200)]
	[Gui.CategoryOrder("Appearance",	10300)]
	#endregion

	public class ChopAndExplode : Indicator
	{
		#region indInfo

		private string indName        = "Chop and explode (ps5) [capissimo]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by capissimo can be found here: https://www.tradingview.com/script/L7ydBiKM-Chop-and-explode-ps5/";

		#endregion

		#region Properties

		[NinjaScriptProperty]
		[Display(Order = 1, GroupName = "Parameters", Name = "Source",
			Description = "Price input fed into the cleaning filter and minimax scaler.")]
		public ChopAndExplode_Source Source { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 2, GroupName = "Parameters", Name = "Clean Source",
			Description = "Apply Ehlers high-pass + SuperSmoother to remove low-frequency drift before scaling.")]
		public bool CleanSource { get; set; }

		[NinjaScriptProperty]
		[Range(2, int.MaxValue)]
		[Display(Order = 3, GroupName = "Parameters", Name = "RSI Lookback",
			Description = "RMA length used inside the RSI calculation.")]
		public int RsiLookback { get; set; }

		[NinjaScriptProperty]
		[Range(2, int.MaxValue)]
		[Display(Order = 4, GroupName = "Parameters", Name = "Minimax Window",
			Description = "Window over which the source is scaled to 1..100.")]
		public int MinimaxWindow { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 1, GroupName = "Signaling", Name = "Show BUY/SELL Labels",
			Description = "Render arrow labels on signal flips.")]
		public bool ShowLabels { get; set; }

		[XmlIgnore]
		[Display(Order = 1, GroupName = "Appearance", Name = "Bullish Color",
			Description = "Color used for the BUY-regime overlay line and labels.")]
		public Brush BullColor { get; set; }
			[Browsable(false)]
			public string BullColorSerialize
			{
				get { return Serialize.BrushToString(BullColor); }
				set { BullColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore]
		[Display(Order = 2, GroupName = "Appearance", Name = "Bearish Color",
			Description = "Color used for the SELL-regime overlay line and labels.")]
		public Brush BearColor { get; set; }
			[Browsable(false)]
			public string BearColorSerialize
			{
				get { return Serialize.BrushToString(BearColor); }
				set { BearColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		#endregion

		#region Public outputs

		[Browsable(false)][XmlIgnore] public Series<double>	Rsi          { get { return Values[0]; } }
		[Browsable(false)][XmlIgnore] public Series<int>	Signal       { get { Update(); return sSignal; } }
		[Browsable(false)][XmlIgnore] public Series<bool>	StartedLong  { get { Update(); return sStartedLong; } }
		[Browsable(false)][XmlIgnore] public Series<bool>	StartedShort { get { Update(); return sStartedShort; } }

		#endregion

		#region Variables

		private Series<double>	sSrc;
		private Series<double>	sHp;		// high-pass intermediate
		private Series<double>	sCleaned;
		private Series<double>	sScaled;
		private Series<double>	sRsi;
		private Series<double>	sUp;
		private Series<double>	sDown;
		private Series<int>		sSignal;
		private Series<bool>	sStartedLong;
		private Series<bool>	sStartedShort;

		// SuperSmoother coefficient — fixed by hpPeriod = 0.00001, computed once in DataLoaded.
		private double alpha;

		#endregion

		#region OnStateChange

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description					= indDescription;
				Name						= indName;
				Calculate					= Calculate.OnBarClose;
				IsOverlay					= false;
				DisplayInDataBox			= true;
				DrawOnPricePanel			= false;
				DrawHorizontalGridLines		= true;
				DrawVerticalGridLines		= true;
				PaintPriceMarkers			= true;
				ScaleJustification			= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				IsSuspendedWhileInactive	= true;

				Source			= ChopAndExplode_Source.Close;
				CleanSource		= false;
				RsiLookback		= 10;
				MinimaxWindow	= 20;
				ShowLabels		= true;

				BullColor = new SolidColorBrush(Color.FromRgb(0x00, 0x80, 0x00));
				BearColor = new SolidColorBrush(Color.FromRgb(0xC0, 0x10, 0x10));
				EnsureFrozen(BullColor);
				EnsureFrozen(BearColor);

				AddPlot(new Stroke(Brushes.Black,    DashStyleHelper.Solid, 1f), PlotStyle.Line, "RSI");
				AddPlot(new Stroke(Brushes.DimGray,  DashStyleHelper.Solid, 3f), PlotStyle.Line, "Regime");

				AddLine(new Stroke(Brushes.Silver, DashStyleHelper.Dot, 1f), 70, "Upper");
				AddLine(new Stroke(Brushes.Silver, DashStyleHelper.Dot, 1f), 60, "Bull Threshold");
				AddLine(new Stroke(Brushes.Silver, DashStyleHelper.Dot, 1f), 55, "Tight Chop Top");
				AddLine(new Stroke(Brushes.Silver, DashStyleHelper.Dot, 1f), 50, "Mid");
				AddLine(new Stroke(Brushes.Silver, DashStyleHelper.Dot, 1f), 45, "Tight Chop Bottom");
				AddLine(new Stroke(Brushes.Silver, DashStyleHelper.Dot, 1f), 40, "Bear Threshold");
				AddLine(new Stroke(Brushes.Silver, DashStyleHelper.Dot, 1f), 30, "Lower");
			}
			else if (State == State.DataLoaded)
			{
				sSrc          = new Series<double>(this);
				sHp           = new Series<double>(this);
				sCleaned      = new Series<double>(this);
				sScaled       = new Series<double>(this);
				sRsi          = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sUp           = new Series<double>(this);
				sDown         = new Series<double>(this);
				sSignal       = new Series<int>(this,    MaximumBarsLookBack.Infinite);
				sStartedLong  = new Series<bool>(this,   MaximumBarsLookBack.Infinite);
				sStartedShort = new Series<bool>(this,   MaximumBarsLookBack.Infinite);

				double pi = 2.0 * Math.Asin(1.0);
				double hpPeriod = 0.00001;
				alpha = (1.0 - Math.Sin(2.0 * pi / hpPeriod)) / Math.Cos(2.0 * pi / hpPeriod);
			}
		}

		#endregion

		#region OnBarUpdate

		protected override void OnBarUpdate()
		{
			sSrc[0] = SourceValue();

			// Cleaning filter — only meaningful warmed up.
			if (CurrentBar < 6)
			{
				sHp[0]      = sSrc[0];
				sCleaned[0] = sSrc[0];
			}
			else
			{
				sHp[0] = 0.5 * (1.0 + alpha) * (sSrc[0] - sSrc[1]) + alpha * sHp[1];
				sCleaned[0] = (sHp[0] + 2.0 * sHp[1] + 3.0 * sHp[2] + 3.0 * sHp[3] + 2.0 * sHp[4] + sHp[5]) / 12.0;
			}

			double cleaned = CleanSource ? sCleaned[0] : sSrc[0];

			// Minimax(cleaned, MMX, 1, 100): scale to 1..100 over the window.
			sScaled[0] = MinimaxScale(cleaned, MinimaxWindow, 1.0, 100.0);

			// RSI on the scaled series (RMA-based).
			double change = CurrentBar > 0 ? sScaled[0] - sScaled[1] : 0.0;
			double upTick   = Math.Max(change, 0);
			double dnTick   = -Math.Min(change, 0);

			double k = 1.0 / RsiLookback;
			sUp[0]   = CurrentBar == 0 ? upTick : (1 - k) * sUp[1]   + k * upTick;
			sDown[0] = CurrentBar == 0 ? dnTick : (1 - k) * sDown[1] + k * dnTick;

			double rsi = sDown[0] == 0 ? 100 : sUp[0] == 0 ? 0 : 100.0 - 100.0 / (1.0 + sUp[0] / sDown[0]);
			sRsi[0]    = rsi;
			Values[0][0] = rsi;

			bool isLong  = rsi > 60;
			bool isShort = rsi < 40;

			int prevSig = CurrentBar > 0 ? sSignal[1] : 0;
			int sig = isLong ? 1 : isShort ? -1 : prevSig;
			sSignal[0] = sig;

			bool changed = CurrentBar > 0 && sig != prevSig;
			sStartedLong[0]  = changed && sig ==  1;
			sStartedShort[0] = changed && sig == -1;

			// Regime overlay line — colored via the second plot, only when actively in a regime.
			if (isLong)        { Values[1][0] = rsi; PlotBrushes[1][0] = BullColor; }
			else if (isShort)  { Values[1][0] = rsi; PlotBrushes[1][0] = BearColor; }
			else                Values[1].Reset();

			if (ShowLabels)
			{
				// isAutoScale = true so the panel expands to include these positions even when RSI is mid-range.
				if (sStartedLong[0])
					Draw.TriangleUp(this, "ceLong" + CurrentBar, true, 0, 5, BullColor);
				if (sStartedShort[0])
					Draw.TriangleDown(this, "ceShort" + CurrentBar, true, 0, 95, BearColor);
			}
		}

		private double SourceValue()
		{
			switch (Source)
			{
				case ChopAndExplode_Source.HL2:    return (High[0] + Low[0]) / 2.0;
				case ChopAndExplode_Source.HLC3:   return (High[0] + Low[0] + Close[0]) / 3.0;
				case ChopAndExplode_Source.OHLC4:  return (Open[0] + High[0] + Low[0] + Close[0]) / 4.0;
				case ChopAndExplode_Source.Open:   return Open[0];
				case ChopAndExplode_Source.High:   return High[0];
				case ChopAndExplode_Source.Low:    return Low[0];
			}
			return Close[0];
		}

		// (max - min) * (X - lo) / (hi - lo) + min
		private double MinimaxScale(double x, int p, double mn, double mx)
		{
			int n = Math.Min(p, CurrentBar + 1);
			double hi = double.MinValue, lo = double.MaxValue;
			for (int i = 0; i < n; i++)
			{
				double v = sSrc[i];
				if (v > hi) hi = v;
				if (v < lo) lo = v;
			}
			if (hi == lo) return (mn + mx) / 2.0;
			return (mx - mn) * (x - lo) / (hi - lo) + mn;
		}

		#endregion

		#region Helpers

		private static Brush EnsureFrozen(Brush b)
		{
			if (b != null && b.CanFreeze && !b.IsFrozen) b.Freeze();
			return b;
		}

		#endregion
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private indTradingView.ChopAndExplode[] cacheChopAndExplode;
		public indTradingView.ChopAndExplode ChopAndExplode(ChopAndExplode_Source source, bool cleanSource, int rsiLookback, int minimaxWindow, bool showLabels)
		{
			return ChopAndExplode(Input, source, cleanSource, rsiLookback, minimaxWindow, showLabels);
		}

		public indTradingView.ChopAndExplode ChopAndExplode(ISeries<double> input, ChopAndExplode_Source source, bool cleanSource, int rsiLookback, int minimaxWindow, bool showLabels)
		{
			if (cacheChopAndExplode != null)
				for (int idx = 0; idx < cacheChopAndExplode.Length; idx++)
					if (cacheChopAndExplode[idx] != null && cacheChopAndExplode[idx].Source == source && cacheChopAndExplode[idx].CleanSource == cleanSource && cacheChopAndExplode[idx].RsiLookback == rsiLookback && cacheChopAndExplode[idx].MinimaxWindow == minimaxWindow && cacheChopAndExplode[idx].ShowLabels == showLabels && cacheChopAndExplode[idx].EqualsInput(input))
						return cacheChopAndExplode[idx];
			return CacheIndicator<indTradingView.ChopAndExplode>(new indTradingView.ChopAndExplode(){ Source = source, CleanSource = cleanSource, RsiLookback = rsiLookback, MinimaxWindow = minimaxWindow, ShowLabels = showLabels }, input, ref cacheChopAndExplode);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.ChopAndExplode ChopAndExplode(ChopAndExplode_Source source, bool cleanSource, int rsiLookback, int minimaxWindow, bool showLabels)
		{
			return indicator.ChopAndExplode(Input, source, cleanSource, rsiLookback, minimaxWindow, showLabels);
		}

		public Indicators.indTradingView.ChopAndExplode ChopAndExplode(ISeries<double> input , ChopAndExplode_Source source, bool cleanSource, int rsiLookback, int minimaxWindow, bool showLabels)
		{
			return indicator.ChopAndExplode(input, source, cleanSource, rsiLookback, minimaxWindow, showLabels);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.ChopAndExplode ChopAndExplode(ChopAndExplode_Source source, bool cleanSource, int rsiLookback, int minimaxWindow, bool showLabels)
		{
			return indicator.ChopAndExplode(Input, source, cleanSource, rsiLookback, minimaxWindow, showLabels);
		}

		public Indicators.indTradingView.ChopAndExplode ChopAndExplode(ISeries<double> input , ChopAndExplode_Source source, bool cleanSource, int rsiLookback, int minimaxWindow, bool showLabels)
		{
			return indicator.ChopAndExplode(input, source, cleanSource, rsiLookback, minimaxWindow, showLabels);
		}
	}
}

#endregion
