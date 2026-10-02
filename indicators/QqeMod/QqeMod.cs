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

// NT8 Version of QQE MOD
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the Mozilla Public License 2.0 (MPL 2.0).
// The original Pine Script™ code is by Mihkel00 and can be found at: https://www.tradingview.com/script/TpUW4muw-QQE-MOD/
// Adaptation for NinjaTrader by jack@mydailytake.com
// © 2026 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the Mozilla Public License 2.0. Full license details at https://mozilla.org/MPL/2.0/
// The use of Mihkel00 name or its adapted code in this work does not imply endorsement by the original authors.
//
// Notes:
//   Two QQE oscillators (Primary + Secondary) running in parallel, plus a Bollinger band of the
//   primary trend line acting as a zero-cross filter. The secondary RSI is plotted as a histogram;
//   bars confirmed by both QQEs (secondary outside its threshold AND primary outside the BB) light
//   up Up / Down. Bars within the secondary threshold are hidden.
//
//   `thresholdPrimary` control was dropped — it was unused upstream (no dead user-facing properties).
//   The primary QQE's confirmation gate is the Bollinger upper / lower band.
//
//   Non-repainting. Public Series outputs: PrimaryTrendLine, SecondaryTrendLine, PrimaryRsi, SecondaryRsi.

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	#region Categories
	[Gui.CategoryOrder("Primary QQE",			10100)]
	[Gui.CategoryOrder("Secondary QQE",			10200)]
	[Gui.CategoryOrder("Bollinger Bands",		10300)]
	[Gui.CategoryOrder("Display",				10400)]
	#endregion

	public class QqeMod : Indicator
	{
		#region indInfo

		private string indName        = "QQE MOD [Mihkel00]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by Mihkel00 can be found here: https://www.tradingview.com/script/TpUW4muw-QQE-MOD/";

		#endregion

		#region Properties

		// ── Primary QQE ──────────────────────────────────────────────────────

		[NinjaScriptProperty]
		[Display(Order = 1, GroupName = "Primary QQE", Name = "Source",
			Description = "Price input for the primary QQE.")]
		public PriceType SourcePrim { get; set; }

		[NinjaScriptProperty]
		[Range(2, int.MaxValue)]
		[Display(Order = 2, GroupName = "Primary QQE", Name = "RSI Length",
			Description = "Lookback for the primary RSI.")]
		public int RsiLengthPrim { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 3, GroupName = "Primary QQE", Name = "RSI Smoothing",
			Description = "EMA length applied to the primary RSI before band derivation.")]
		public int RsiSmoothingPrim { get; set; }

		[NinjaScriptProperty]
		[Range(0.01, double.MaxValue)]
		[Display(Order = 4, GroupName = "Primary QQE", Name = "QQE Factor",
			Description = "Multiplier on the smoothed ATR(RSI) to derive primary band width.")]
		public double QqeFactorPrim { get; set; }

		// ── Secondary QQE ────────────────────────────────────────────────────

		[NinjaScriptProperty]
		[Display(Order = 1, GroupName = "Secondary QQE", Name = "Source",
			Description = "Price input for the secondary QQE.")]
		public PriceType SourceSec { get; set; }

		[NinjaScriptProperty]
		[Range(2, int.MaxValue)]
		[Display(Order = 2, GroupName = "Secondary QQE", Name = "RSI Length",
			Description = "Lookback for the secondary RSI.")]
		public int RsiLengthSec { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 3, GroupName = "Secondary QQE", Name = "RSI Smoothing",
			Description = "EMA length applied to the secondary RSI before band derivation.")]
		public int RsiSmoothingSec { get; set; }

		[NinjaScriptProperty]
		[Range(0.01, double.MaxValue)]
		[Display(Order = 4, GroupName = "Secondary QQE", Name = "QQE Factor",
			Description = "Multiplier on the smoothed ATR(RSI) to derive secondary band width.")]
		public double QqeFactorSec { get; set; }

		[NinjaScriptProperty]
		[Range(0.0, double.MaxValue)]
		[Display(Order = 5, GroupName = "Secondary QQE", Name = "Threshold",
			Description = "|Secondary RSI − 50| must exceed this for the histogram to render and signal logic to fire.")]
		public double ThresholdSec { get; set; }

		// ── Bollinger Bands (around primary trend line − 50) ─────────────────

		[NinjaScriptProperty]
		[Range(2, int.MaxValue)]
		[Display(Order = 1, GroupName = "Bollinger Bands", Name = "Length",
			Description = "Bollinger band length applied to (primary QQE trend line − 50).")]
		public int BollingerLength { get; set; }

		[NinjaScriptProperty]
		[Range(0.001, 5.0)]
		[Display(Order = 2, GroupName = "Bollinger Bands", Name = "Multiplier",
			Description = "Standard-deviation multiplier for the Bollinger upper / lower bands.")]
		public double BollingerMultiplier { get; set; }

		// ── Display ──────────────────────────────────────────────────────────

		[XmlIgnore]
		[Display(Order = 1, GroupName = "Display", Name = "Up Signal Color",
			Description = "Histogram color when both QQEs agree on a bullish signal.")]
		public Brush UpSignalColor { get; set; }
			[Browsable(false)]
			public string UpSignalColorSerialize
			{
				get { return Serialize.BrushToString(UpSignalColor); }
				set { UpSignalColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore]
		[Display(Order = 2, GroupName = "Display", Name = "Down Signal Color",
			Description = "Histogram color when both QQEs agree on a bearish signal.")]
		public Brush DownSignalColor { get; set; }
			[Browsable(false)]
			public string DownSignalColorSerialize
			{
				get { return Serialize.BrushToString(DownSignalColor); }
				set { DownSignalColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore]
		[Display(Order = 3, GroupName = "Display", Name = "Neutral Histogram Color",
			Description = "Histogram color for bars that exceed the secondary threshold but lack primary BB confirmation.")]
		public Brush NeutralColor { get; set; }
			[Browsable(false)]
			public string NeutralColorSerialize
			{
				get { return Serialize.BrushToString(NeutralColor); }
				set { NeutralColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		#endregion

		#region Public outputs

		[Browsable(false)][XmlIgnore] public Series<double> PrimaryTrendLine    { get { return Values[0]; } }
		[Browsable(false)][XmlIgnore] public Series<double> SecondaryTrendLine  { get { return Values[1]; } }
		[Browsable(false)][XmlIgnore] public Series<double> PrimaryRsi          { get { Update(); return sPrimaryRsi;   } }
		[Browsable(false)][XmlIgnore] public Series<double> SecondaryRsi        { get { Update(); return sSecondaryRsi; } }

		#endregion

		#region Variables

		private ISeries<double>	srcPrim, srcSec;

		// Primary QQE
		private RSI				rsiPrim;
		private EMA				smoothedRsiPrim;
		private Series<double>	atrRsiInputPrim;
		private EMA				smoothedAtrRsiPrim;
		private Series<double>	sLongBandPrim, sShortBandPrim;
		private Series<int>		sTrendDirPrim;
		private Series<double>	sPrimaryRsi;

		// Secondary QQE
		private RSI				rsiSec;
		private EMA				smoothedRsiSec;
		private Series<double>	atrRsiInputSec;
		private EMA				smoothedAtrRsiSec;
		private Series<double>	sLongBandSec, sShortBandSec;
		private Series<int>		sTrendDirSec;
		private Series<double>	sSecondaryRsi;

		// Bollinger
		private Series<double>	primaryTrendMinus50;
		private SMA				smaBoll;
		private StdDev			stdDevBoll;

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

				SourcePrim			= PriceType.Close;
				RsiLengthPrim		= 6;
				RsiSmoothingPrim	= 5;
				QqeFactorPrim		= 3.0;

				SourceSec			= PriceType.Close;
				RsiLengthSec		= 6;
				RsiSmoothingSec		= 5;
				QqeFactorSec		= 1.61;
				ThresholdSec		= 3.0;

				BollingerLength		= 50;
				BollingerMultiplier	= 0.35;

				UpSignalColor	= new SolidColorBrush(Color.FromRgb(0x00, 0xc3, 0xff));
				DownSignalColor	= new SolidColorBrush(Color.FromRgb(0xff, 0x00, 0x62));
				NeutralColor	= new SolidColorBrush(Color.FromArgb(204, 0x70, 0x70, 0x70));
				EnsureFrozen(UpSignalColor);
				EnsureFrozen(DownSignalColor);
				EnsureFrozen(NeutralColor);

				AddPlot(new Stroke(Brushes.WhiteSmoke,	DashStyleHelper.Solid, 2f), PlotStyle.Line, "Primary Trend Line");
				AddPlot(new Stroke(Brushes.WhiteSmoke,	DashStyleHelper.Solid, 2f), PlotStyle.Line, "Secondary Trend Line");
				// Histogram + signal plots: stroke defaults DimGray since per-bar color is driven by the Display properties below.
				AddPlot(new Stroke(Brushes.DimGray,		DashStyleHelper.Solid, 4f), PlotStyle.Bar,  "Secondary Histogram");
				AddPlot(new Stroke(Brushes.DimGray,		DashStyleHelper.Solid, 4f), PlotStyle.Bar,  "Up Signal");
				AddPlot(new Stroke(Brushes.DimGray,		DashStyleHelper.Solid, 4f), PlotStyle.Bar,  "Down Signal");

				AddLine(new Stroke(Brushes.WhiteSmoke, DashStyleHelper.Dot, 1f), 0.0, "Zero");
			}
			else if (State == State.DataLoaded)
			{
				srcPrim = GetSource(SourcePrim);
				srcSec  = GetSource(SourceSec);

				rsiPrim				= RSI(srcPrim, RsiLengthPrim, 1);
				smoothedRsiPrim		= EMA(rsiPrim, RsiSmoothingPrim);
				// EMA period is RsiLengthPrim*2-1 (user-configurable, can exceed 256) — needs Infinite.
				atrRsiInputPrim		= new Series<double>(this, MaximumBarsLookBack.Infinite);
				smoothedAtrRsiPrim	= EMA(atrRsiInputPrim, Math.Max(2, RsiLengthPrim * 2 - 1));
				// IsValidDataPoint(N) requires Infinite — these are read with that check in ComputeQqe.
				sLongBandPrim		= new Series<double>(this, MaximumBarsLookBack.Infinite);
				sShortBandPrim		= new Series<double>(this, MaximumBarsLookBack.Infinite);
				sTrendDirPrim		= new Series<int>   (this, MaximumBarsLookBack.Infinite);
				sPrimaryRsi			= new Series<double>(this);

				rsiSec				= RSI(srcSec, RsiLengthSec, 1);
				smoothedRsiSec		= EMA(rsiSec, RsiSmoothingSec);
				atrRsiInputSec		= new Series<double>(this, MaximumBarsLookBack.Infinite);
				smoothedAtrRsiSec	= EMA(atrRsiInputSec, Math.Max(2, RsiLengthSec * 2 - 1));
				sLongBandSec		= new Series<double>(this, MaximumBarsLookBack.Infinite);
				sShortBandSec		= new Series<double>(this, MaximumBarsLookBack.Infinite);
				sTrendDirSec		= new Series<int>   (this, MaximumBarsLookBack.Infinite);
				sSecondaryRsi		= new Series<double>(this);

				// SMA / StdDev wrap with BollingerLength (user-configurable, can exceed 256) — needs Infinite.
				primaryTrendMinus50	= new Series<double>(this, MaximumBarsLookBack.Infinite);
				smaBoll				= SMA(primaryTrendMinus50, BollingerLength);
				stdDevBoll			= StdDev(primaryTrendMinus50, BollingerLength);
			}
		}

		#endregion

		#region OnBarUpdate

		protected override void OnBarUpdate()
		{
			int wildersPrim   = Math.Max(2, RsiLengthPrim * 2 - 1);
			int wildersSec    = Math.Max(2, RsiLengthSec  * 2 - 1);
			int needed        = Math.Max(Math.Max(RsiLengthPrim + wildersPrim, RsiLengthSec + wildersSec), BollingerLength) + 2;
			if (CurrentBar < needed) return;

			double primaryTrendLine, primaryRsiVal;
			ComputeQqe(rsiPrim, smoothedRsiPrim, atrRsiInputPrim, smoothedAtrRsiPrim,
				QqeFactorPrim, sLongBandPrim, sShortBandPrim, sTrendDirPrim,
				out primaryTrendLine, out primaryRsiVal);

			double secondaryTrendLine, secondaryRsiVal;
			ComputeQqe(rsiSec, smoothedRsiSec, atrRsiInputSec, smoothedAtrRsiSec,
				QqeFactorSec, sLongBandSec, sShortBandSec, sTrendDirSec,
				out secondaryTrendLine, out secondaryRsiVal);

			sPrimaryRsi[0]    = primaryRsiVal;
			sSecondaryRsi[0]  = secondaryRsiVal;

			double primaryMinus50      = primaryTrendLine - 50.0;
			double secondaryMinus50    = secondaryTrendLine - 50.0;
			double secondaryRsiMinus50 = secondaryRsiVal - 50.0;

			primaryTrendMinus50[0] = primaryMinus50;
			double basis      = smaBoll[0];
			double dev        = BollingerMultiplier * stdDevBoll[0];
			double bbUpper    = basis + dev;
			double bbLower    = basis - dev;

			Values[0][0] = primaryMinus50;
			Values[1][0] = secondaryMinus50;

			// Secondary histogram — only rendered when |secondaryRsi − 50| > threshold.
			bool histVisible = secondaryRsiMinus50 > ThresholdSec || secondaryRsiMinus50 < -ThresholdSec;
			if (histVisible)
			{
				Values[2][0]      = secondaryRsiMinus50;
				PlotBrushes[2][0] = NeutralColor;
			}
			else
			{
				Values[2].Reset();
			}

			// Up signal: secondary above threshold AND primary RSI − 50 above BB upper.
			double primaryRsiMinus50 = primaryRsiVal - 50.0;
			if (secondaryRsiMinus50 > ThresholdSec && primaryRsiMinus50 > bbUpper)
			{
				Values[3][0]      = secondaryRsiMinus50;
				PlotBrushes[3][0] = UpSignalColor;
			}
			else
			{
				Values[3].Reset();
			}

			// Down signal: secondary below −threshold AND primary RSI − 50 below BB lower.
			if (secondaryRsiMinus50 < -ThresholdSec && primaryRsiMinus50 < bbLower)
			{
				Values[4][0]      = secondaryRsiMinus50;
				PlotBrushes[4][0] = DownSignalColor;
			}
			else
			{
				Values[4].Reset();
			}
		}

		#endregion

		#region QQE engine

		private void ComputeQqe(RSI rsi, EMA smoothedRsi, Series<double> atrInput, EMA smoothedAtr,
			double qqeFactor, Series<double> sLong, Series<double> sShort, Series<int> sTrend,
			out double trendLine, out double currentSmoothedRsi)
		{
			currentSmoothedRsi = smoothedRsi[0];
			double prevSmoothed = smoothedRsi[1];
			double atrRsi       = Math.Abs(prevSmoothed - currentSmoothedRsi);
			atrInput[0]         = atrRsi;

			double dynamicAtrRsi = smoothedAtr[0] * qqeFactor;
			double newLongBand   = currentSmoothedRsi - dynamicAtrRsi;
			double newShortBand  = currentSmoothedRsi + dynamicAtrRsi;

			double prevLong  = sLong.IsValidDataPoint(1)  ? sLong[1]  : newLongBand;
			double prevShort = sShort.IsValidDataPoint(1) ? sShort[1] : newShortBand;

			double longBand  = (prevSmoothed > prevLong  && currentSmoothedRsi > prevLong)
				? Math.Max(prevLong,  newLongBand)
				: newLongBand;
			double shortBand = (prevSmoothed < prevShort && currentSmoothedRsi < prevShort)
				? Math.Min(prevShort, newShortBand)
				: newShortBand;

			sLong[0]  = longBand;
			sShort[0] = shortBand;

			// Trend cross detection on the prior short / long bands.
			int prevTrend = sTrend.IsValidDataPoint(1) ? sTrend[1] : 0;
			bool crossOverShort = prevSmoothed <= prevShort && currentSmoothedRsi >  prevShort;
			bool crossUnderLong = prevSmoothed >= prevLong  && currentSmoothedRsi <  prevLong;

			int trend = crossOverShort ? 1 : crossUnderLong ? -1 : prevTrend;
			sTrend[0] = trend;

			trendLine = trend == 1 ? longBand : shortBand;
		}

		#endregion

		#region Helpers

		private ISeries<double> GetSource(PriceType priceType)
		{
			switch (priceType)
			{
				case PriceType.Close:    return Close;
				case PriceType.High:     return High;
				case PriceType.Low:      return Low;
				case PriceType.Median:   return Median;
				case PriceType.Open:     return Open;
				case PriceType.Typical:  return Typical;
				case PriceType.Weighted: return Weighted;
				default:                 return Input;
			}
		}

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
		private indTradingView.QqeMod[] cacheQqeMod;
		public indTradingView.QqeMod QqeMod(PriceType sourcePrim, int rsiLengthPrim, int rsiSmoothingPrim, double qqeFactorPrim, PriceType sourceSec, int rsiLengthSec, int rsiSmoothingSec, double qqeFactorSec, double thresholdSec, int bollingerLength, double bollingerMultiplier)
		{
			return QqeMod(Input, sourcePrim, rsiLengthPrim, rsiSmoothingPrim, qqeFactorPrim, sourceSec, rsiLengthSec, rsiSmoothingSec, qqeFactorSec, thresholdSec, bollingerLength, bollingerMultiplier);
		}

		public indTradingView.QqeMod QqeMod(ISeries<double> input, PriceType sourcePrim, int rsiLengthPrim, int rsiSmoothingPrim, double qqeFactorPrim, PriceType sourceSec, int rsiLengthSec, int rsiSmoothingSec, double qqeFactorSec, double thresholdSec, int bollingerLength, double bollingerMultiplier)
		{
			if (cacheQqeMod != null)
				for (int idx = 0; idx < cacheQqeMod.Length; idx++)
					if (cacheQqeMod[idx] != null && cacheQqeMod[idx].SourcePrim == sourcePrim && cacheQqeMod[idx].RsiLengthPrim == rsiLengthPrim && cacheQqeMod[idx].RsiSmoothingPrim == rsiSmoothingPrim && cacheQqeMod[idx].QqeFactorPrim == qqeFactorPrim && cacheQqeMod[idx].SourceSec == sourceSec && cacheQqeMod[idx].RsiLengthSec == rsiLengthSec && cacheQqeMod[idx].RsiSmoothingSec == rsiSmoothingSec && cacheQqeMod[idx].QqeFactorSec == qqeFactorSec && cacheQqeMod[idx].ThresholdSec == thresholdSec && cacheQqeMod[idx].BollingerLength == bollingerLength && cacheQqeMod[idx].BollingerMultiplier == bollingerMultiplier && cacheQqeMod[idx].EqualsInput(input))
						return cacheQqeMod[idx];
			return CacheIndicator<indTradingView.QqeMod>(new indTradingView.QqeMod(){ SourcePrim = sourcePrim, RsiLengthPrim = rsiLengthPrim, RsiSmoothingPrim = rsiSmoothingPrim, QqeFactorPrim = qqeFactorPrim, SourceSec = sourceSec, RsiLengthSec = rsiLengthSec, RsiSmoothingSec = rsiSmoothingSec, QqeFactorSec = qqeFactorSec, ThresholdSec = thresholdSec, BollingerLength = bollingerLength, BollingerMultiplier = bollingerMultiplier }, input, ref cacheQqeMod);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.QqeMod QqeMod(PriceType sourcePrim, int rsiLengthPrim, int rsiSmoothingPrim, double qqeFactorPrim, PriceType sourceSec, int rsiLengthSec, int rsiSmoothingSec, double qqeFactorSec, double thresholdSec, int bollingerLength, double bollingerMultiplier)
		{
			return indicator.QqeMod(Input, sourcePrim, rsiLengthPrim, rsiSmoothingPrim, qqeFactorPrim, sourceSec, rsiLengthSec, rsiSmoothingSec, qqeFactorSec, thresholdSec, bollingerLength, bollingerMultiplier);
		}

		public Indicators.indTradingView.QqeMod QqeMod(ISeries<double> input , PriceType sourcePrim, int rsiLengthPrim, int rsiSmoothingPrim, double qqeFactorPrim, PriceType sourceSec, int rsiLengthSec, int rsiSmoothingSec, double qqeFactorSec, double thresholdSec, int bollingerLength, double bollingerMultiplier)
		{
			return indicator.QqeMod(input, sourcePrim, rsiLengthPrim, rsiSmoothingPrim, qqeFactorPrim, sourceSec, rsiLengthSec, rsiSmoothingSec, qqeFactorSec, thresholdSec, bollingerLength, bollingerMultiplier);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.QqeMod QqeMod(PriceType sourcePrim, int rsiLengthPrim, int rsiSmoothingPrim, double qqeFactorPrim, PriceType sourceSec, int rsiLengthSec, int rsiSmoothingSec, double qqeFactorSec, double thresholdSec, int bollingerLength, double bollingerMultiplier)
		{
			return indicator.QqeMod(Input, sourcePrim, rsiLengthPrim, rsiSmoothingPrim, qqeFactorPrim, sourceSec, rsiLengthSec, rsiSmoothingSec, qqeFactorSec, thresholdSec, bollingerLength, bollingerMultiplier);
		}

		public Indicators.indTradingView.QqeMod QqeMod(ISeries<double> input , PriceType sourcePrim, int rsiLengthPrim, int rsiSmoothingPrim, double qqeFactorPrim, PriceType sourceSec, int rsiLengthSec, int rsiSmoothingSec, double qqeFactorSec, double thresholdSec, int bollingerLength, double bollingerMultiplier)
		{
			return indicator.QqeMod(input, sourcePrim, rsiLengthPrim, rsiSmoothingPrim, qqeFactorPrim, sourceSec, rsiLengthSec, rsiSmoothingSec, qqeFactorSec, thresholdSec, bollingerLength, bollingerMultiplier);
		}
	}
}

#endregion
