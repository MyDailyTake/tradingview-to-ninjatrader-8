#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

// NT8 Version of KNN Supertrend Horizon
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the Attribution-NonCommercial-ShareAlike 4.0 International (CC BY-NC-SA 4.0).
// The original Pine Script™ code is by LuxAlgo and can be found at: https://www.tradingview.com/script/O6LjCPzY-KNN-Supertrend-Horizon-LuxAlgo/
// Adaptation for NinjaTrader by jack@mydailytake.com
// Write-up + downloads: https://mydailytake.com/knn-supertrend-horizon-luxalgo-ninjatrader-8/
// Source + all conversions: https://github.com/MyDailyTake/tradingview-to-ninjatrader-8
// © 2026 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License. Full license details at https://creativecommons.org/licenses/by-nc-sa/4.0/
// The use of LuxAlgo name or its adapted code in this work does not imply endorsement by the original authors.
//
// Notes:
//   - Non-repainting: KNN votes on confirmed bar history; rejection orbs anchor to the closed signal bar.
//   - Public Series exposed for strategies: MlBullish, SmoothedProb, BullRejection, BearRejection, MlSupertrend.

#region Enums KnnSupertrendHorizon

public enum KnnSupertrendHorizon_DashCorner
{
	TopRight,
	BottomRight,
	BottomLeft
}

public enum KnnSupertrendHorizon_DashSize
{
	Tiny,
	Small,
	Normal,
	Large,
	Huge
}

#endregion

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	#region Categories

	[Gui.CategoryOrder("Machine Learning",       10100)]
	[Gui.CategoryOrder("SuperTrend",             10200)]
	[Gui.CategoryOrder("Noise Filter",           10300)]
	[Gui.CategoryOrder("Rejection Signals",      10400)]
	[Gui.CategoryOrder("Visual",                 10500)]
	[Gui.CategoryOrder("Dashboard",              10600)]

	#endregion

	public class KnnSupertrendHorizon : Indicator
	{
		#region indInfo

		private string indName        = "KNN Supertrend Horizon [LuxAlgo]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by LuxAlgo can be found here: https://www.tradingview.com/script/O6LjCPzY-KNN-Supertrend-Horizon-LuxAlgo/";

		#endregion

		#region Indicator instances

		private HMA    hmaSrc;
		private RSI    rsiF1;
		private ATR    atrFilter;   // ATR(14) for f2 + rejection reference
		private ATR    atrSt;       // ATR(AtrLength) for SuperTrend
		private EMA    emaProb;
		private SMA    volMa;
		private StdDev volSd;

		#endregion

		#region Custom series

		private Series<double> srcSeries;
		private Series<double> probSeries;
		private Series<double> f1Series;
		private Series<double> f2Series;
		private Series<int>    targetTrendSeries;

		private Series<bool>   sMlBullish;
		private Series<bool>   sBullRejection;
		private Series<bool>   sBearRejection;
		private Series<double> sSmoothedProb;
		private Series<double> sGlowPower;

		#endregion

		#region SuperTrend & ML state

		private double stUpper      = double.NaN;
		private double stLower      = double.NaN;
		private double stValue      = double.NaN;
		private int    stDirection  = 0;
		private double mlProb       = 50.0;
		private bool   mlBullish    = false;
		private int    lastBubbleBar = -10000;
		private int    barsSinceChange = 0;
		private bool   prevMlBullish = false;

		#endregion

		#region Rejection orb storage

		private struct RejectionOrb
		{
			public int    BarIdx;
			public bool   IsBull;
			public double Anchor;       // wick extreme (low for bull, high for bear)
			public double OrbCenter;    // anchor ± stem
			public int    SizeUnits;    // dynamic 8..30
			public string VolumeText;
		}

		private readonly List<RejectionOrb> orbs = new List<RejectionOrb>();

		#endregion

		#region SharpDX resources

		private SharpDX.Direct2D1.Brush dxBullSolid;
		private SharpDX.Direct2D1.Brush dxBearSolid;
		private SharpDX.Direct2D1.Brush dxOrbStem;
		private SharpDX.Direct2D1.Brush dxOrbWhite;
		private SharpDX.Direct2D1.Brush dxOrbBlackHalo;
		private SharpDX.Direct2D1.Brush dxLabelBg;
		private SharpDX.Direct2D1.Brush dxLabelText;

		private SharpDX.Direct2D1.Brush dxDashBg;
		private SharpDX.Direct2D1.Brush dxDashBorder;
		private SharpDX.Direct2D1.Brush dxDashHeader;
		private SharpDX.Direct2D1.Brush dxDashData;

		private SharpDX.DirectWrite.TextFormat tfTitle;
		private SharpDX.DirectWrite.TextFormat tfBody;
		private SharpDX.DirectWrite.TextFormat tfOrb;

		private const int GLOW_LEVELS = 8;
		private SharpDX.Direct2D1.Brush[] dxBullGlow;
		private SharpDX.Direct2D1.Brush[] dxBearGlow;

		private int lastTitleSize = -1;
		private int lastBodySize  = -1;

		#endregion

		#region Properties — Machine Learning

		[NinjaScriptProperty]
		[Range(1, 50)]
		[Display(Order = 1, GroupName = "Machine Learning", Name = "K-Neighbors", Description = "Number of nearest neighbors used to vote on the next-bar SuperTrend direction.")]
		public int Neighbors { get; set; }

		[NinjaScriptProperty]
		[Range(100, 2000)]
		[Display(Order = 2, GroupName = "Machine Learning", Name = "Search Window", Description = "How many historical bars to scan for nearest-neighbor lookups.")]
		public int WindowSize { get; set; }

		#endregion

		#region Properties — SuperTrend

		[NinjaScriptProperty]
		[Range(1, 200)]
		[Display(Order = 1, GroupName = "SuperTrend", Name = "ATR Length", Description = "ATR period used inside the SuperTrend calculation.")]
		public int AtrLength { get; set; }

		[NinjaScriptProperty]
		[Range(0.01, 20.0)]
		[Display(Order = 2, GroupName = "SuperTrend", Name = "Factor", Description = "ATR multiplier for the SuperTrend bands.")]
		public double Factor { get; set; }

		#endregion

		#region Properties — Noise Filter

		[NinjaScriptProperty]
		[Display(Order = 1, GroupName = "Noise Filter", Name = "Smooth Price Input", Description = "Apply HMA smoothing to the price source before feeding the RSI feature.")]
		public bool SmoothSource { get; set; }

		[NinjaScriptProperty]
		[Range(1, 200)]
		[Display(Order = 2, GroupName = "Noise Filter", Name = "Smoothing Length", Description = "HMA length for the smoothed price source.")]
		public int SmoothingLength { get; set; }

		[NinjaScriptProperty]
		[Range(0.0, 20.0)]
		[Display(Order = 3, GroupName = "Noise Filter", Name = "ML Confidence Buffer (%)", Description = "Hysteresis around 50% — smoothed probability must clear 50±buffer to flip direction.")]
		public double MlBuffer { get; set; }

		#endregion

		#region Properties — Rejection Signals

		[NinjaScriptProperty]
		[Display(Order = 1, GroupName = "Rejection Signals", Name = "Show 3D Rejection Orbs", Description = "Render layered rejection orbs at wicks that contradict the current SuperTrend.")]
		public bool ShowOrbs { get; set; }

		[NinjaScriptProperty]
		[Range(1.0, 5.0)]
		[Display(Order = 2, GroupName = "Rejection Signals", Name = "Min Wick-to-Body Multiplier", Description = "Wick must be at least this many times the body for an orb to fire.")]
		public double WickMultiplier { get; set; }

		[NinjaScriptProperty]
		[Range(1, 20)]
		[Display(Order = 3, GroupName = "Rejection Signals", Name = "Min Bubble Gap (Bars)", Description = "Refractory period between consecutive rejection orbs.")]
		public int BubbleGap { get; set; }

		#endregion

		#region Properties — Visual

		[XmlIgnore]
		[Display(Order = 1, GroupName = "Visual", Name = "Uptrend Color", Description = "Bull theme color.")]
		public Brush BullColor { get; set; }
		[Browsable(false)]
		public string BullColorSerialize
		{
			get { return Serialize.BrushToString(BullColor); }
			set { BullColor = EnsureFrozen(Serialize.StringToBrush(value)); }
		}

		[XmlIgnore]
		[Display(Order = 2, GroupName = "Visual", Name = "Downtrend Color", Description = "Bear theme color.")]
		public Brush BearColor { get; set; }
		[Browsable(false)]
		public string BearColorSerialize
		{
			get { return Serialize.BrushToString(BearColor); }
			set { BearColor = EnsureFrozen(Serialize.StringToBrush(value)); }
		}

		[NinjaScriptProperty]
		[Range(1, 200)]
		[Display(Order = 3, GroupName = "Visual", Name = "Liquid Smoothness", Description = "EMA length applied to the raw KNN probability before threshold detection.")]
		public int LiquidSmoothness { get; set; }

		[NinjaScriptProperty]
		[Range(1.0, 3.0)]
		[Display(Order = 4, GroupName = "Visual", Name = "Vibrancy", Description = "Power curve applied to confidence intensity — higher values intensify saturation.")]
		public double Vibrancy { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 5, GroupName = "Visual", Name = "Gradient Candle Coloring", Description = "Recolor candles by trend with confidence-driven brightness.")]
		public bool ColorCandles { get; set; }

		#endregion

		#region Properties — Dashboard

		[NinjaScriptProperty]
		[Display(Order = 1, GroupName = "Dashboard", Name = "Show Dashboard", Description = "Render the SharpDX HUD with ML and SuperTrend stats.")]
		public bool ShowDashboard { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 2, GroupName = "Dashboard", Name = "Position", Description = "Corner placement for the dashboard.")]
		public KnnSupertrendHorizon_DashCorner DashboardPosition { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 3, GroupName = "Dashboard", Name = "Size", Description = "Overall dashboard scale.")]
		public KnnSupertrendHorizon_DashSize DashboardSize { get; set; }

		#endregion

		#region OnStateChange

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description                  = indDescription;
				Name                         = indName;
				Calculate                    = Calculate.OnBarClose;
				IsOverlay                    = true;
				DisplayInDataBox             = true;
				DrawOnPricePanel             = true;
				DrawHorizontalGridLines      = true;
				DrawVerticalGridLines        = true;
				PaintPriceMarkers            = true;
				ScaleJustification           = NinjaTrader.Gui.Chart.ScaleJustification.Right;
				IsSuspendedWhileInactive     = true;

				Neighbors          = 10;
				WindowSize         = 500;
				AtrLength          = 10;
				Factor             = 3.0;
				SmoothSource       = true;
				SmoothingLength    = 10;
				MlBuffer           = 5.0;
				ShowOrbs           = true;
				WickMultiplier     = 1.5;
				BubbleGap          = 5;
				LiquidSmoothness   = 20;
				Vibrancy           = 1.5;
				ColorCandles       = true;
				ShowDashboard      = true;
				DashboardPosition  = KnnSupertrendHorizon_DashCorner.TopRight;
				DashboardSize      = KnnSupertrendHorizon_DashSize.Small;

				BullColor = new SolidColorBrush(Color.FromRgb(0x08, 0x99, 0x81));
				BearColor = new SolidColorBrush(Color.FromRgb(0xf2, 0x36, 0x45));
				if (BullColor.CanFreeze) BullColor.Freeze();
				if (BearColor.CanFreeze) BearColor.Freeze();

				AddPlot(new Stroke(Brushes.DimGray, DashStyleHelper.Solid, 2f), PlotStyle.Line, "MlSupertrend");
			}
			else if (State == State.Configure)
			{
			}
			else if (State == State.DataLoaded)
			{
				srcSeries         = new Series<double>(this, MaximumBarsLookBack.Infinite);
				probSeries        = new Series<double>(this);
				f1Series          = new Series<double>(this, MaximumBarsLookBack.Infinite);
				f2Series          = new Series<double>(this, MaximumBarsLookBack.Infinite);
				targetTrendSeries = new Series<int>   (this, MaximumBarsLookBack.Infinite);

				sMlBullish     = new Series<bool>(this, MaximumBarsLookBack.Infinite);
				sBullRejection = new Series<bool>(this, MaximumBarsLookBack.Infinite);
				sBearRejection = new Series<bool>(this, MaximumBarsLookBack.Infinite);
				sSmoothedProb  = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sGlowPower     = new Series<double>(this, MaximumBarsLookBack.Infinite);

				hmaSrc    = HMA(Close, SmoothingLength);
				rsiF1     = RSI(srcSeries, 14, 1);
				atrFilter = ATR(14);
				atrSt     = ATR(AtrLength);
				emaProb   = EMA(probSeries, LiquidSmoothness);
				volMa     = SMA(Volume, 100);
				volSd     = StdDev(Volume, 100);

				stUpper = stLower = stValue = double.NaN;
				stDirection = 0;
				mlProb = 50.0;
				mlBullish = false;
				prevMlBullish = false;
				lastBubbleBar = -10000;
				barsSinceChange = 0;
				orbs.Clear();
			}
			else if (State == State.Realtime)
			{
				try
				{
					if (ChartControl != null)
						ChartControl.Dispatcher.InvokeAsync(() => { try { ChartControl.InvalidateVisual(); } catch { } });
				}
				catch { }
			}
			else if (State == State.Terminated)
			{
				ReleaseRenderResources();
			}
		}

		#endregion

		#region OnBarUpdate

		protected override void OnBarUpdate()
		{
			if (CurrentBar < 1)
			{
				srcSeries[0]         = Close[0];
				f1Series[0]          = 50.0;
				f2Series[0]          = 0.0;
				targetTrendSeries[0] = -1;
				probSeries[0]        = 50.0;
				sSmoothedProb[0]     = 50.0;
				sGlowPower[0]        = 0.0;
				sMlBullish[0]        = false;
				sBullRejection[0]    = false;
				sBearRejection[0]    = false;
				Values[0].Reset(0);
				return;
			}

			srcSeries[0] = SmoothSource ? hmaSrc[0] : Close[0];

			double atrV  = atrFilter[0];
			double atrSv = atrSt[0];
			double srcV  = srcSeries[0];

			double f1 = rsiF1[0];
			double f2 = (srcV != 0.0) ? (atrV / srcV) * 100.0 : 0.0;
			f1Series[0] = f1;
			f2Series[0] = f2;

			ComputeSuperTrend(atrSv);
			Values[0][0] = stValue;
			targetTrendSeries[0] = (stDirection < 0) ? 1 : -1;

			if (CurrentBar > WindowSize)
				mlProb = ComputeKnnProbability(f1, f2);

			probSeries[0] = mlProb;
			double smoothedProb = emaProb[0];
			sSmoothedProb[0] = smoothedProb;

			prevMlBullish = mlBullish;
			if (smoothedProb > 50.0 + MlBuffer)       mlBullish = true;
			else if (smoothedProb < 50.0 - MlBuffer)  mlBullish = false;
			sMlBullish[0] = mlBullish;

			if (mlBullish != prevMlBullish) barsSinceChange = 0;
			else                             barsSinceChange++;

			double intensity = mlBullish ? (smoothedProb - 50.0) * 2.0 : (50.0 - smoothedProb) * 2.0;
			intensity = Math.Max(0.0, intensity);
			double glowPower = Math.Pow(intensity / 100.0, Vibrancy) * 100.0;
			sGlowPower[0] = glowPower;

			Color bullRgb = ((SolidColorBrush)BullColor).Color;
			Color bearRgb = ((SolidColorBrush)BearColor).Color;

			PlotBrushes[0][0] = mlBullish
				? AlphaBrush(bullRgb, 102)
				: AlphaBrush(bearRgb, 102);

			if (ColorCandles)
			{
				int candleAlpha = (int)Math.Round(38 + (217 - 38) * (glowPower / 100.0));
				if (candleAlpha < 38)  candleAlpha = 38;
				if (candleAlpha > 217) candleAlpha = 217;
				Color cBrush = mlBullish
					? Color.FromArgb((byte)candleAlpha, bullRgb.R, bullRgb.G, bullRgb.B)
					: Color.FromArgb((byte)candleAlpha, bearRgb.R, bearRgb.G, bearRgb.B);
				SolidColorBrush sb = new SolidColorBrush(cBrush);
				if (sb.CanFreeze) sb.Freeze();
				BarBrushes[0]            = sb;
				CandleOutlineBrushes[0]  = sb;
			}

			DetectRejection(atrV);
		}

		private void ComputeSuperTrend(double atrV)
		{
			double hl2       = (High[0] + Low[0]) * 0.5;
			double rawUpper  = hl2 + Factor * atrV;
			double rawLower  = hl2 - Factor * atrV;

			double prevClose = Close[1];
			double prevUpper = double.IsNaN(stUpper) ? rawUpper : stUpper;
			double prevLower = double.IsNaN(stLower) ? rawLower : stLower;

			double newUpper = (prevClose < prevUpper) ? Math.Min(rawUpper, prevUpper) : rawUpper;
			double newLower = (prevClose > prevLower) ? Math.Max(rawLower, prevLower) : rawLower;

			int prevDir = stDirection;
			int dir;
			if (CurrentBar <= AtrLength)
				dir = 1;
			else if (prevDir == -1)
				dir = (Close[0] < newLower) ? 1 : -1;
			else
				dir = (Close[0] > newUpper) ? -1 : 1;

			stUpper      = newUpper;
			stLower      = newLower;
			stDirection  = dir;
			stValue      = (dir == -1) ? newLower : newUpper;
		}

		private double ComputeKnnProbability(double f1Cur, double f2Cur)
		{
			int n = WindowSize;
			double[] dists = new double[n];
			for (int i = 1; i <= n; i++)
			{
				double df1 = f1Cur - f1Series[i];
				double df2 = f2Cur - f2Series[i];
				dists[i - 1] = Math.Sqrt(df1 * df1 + df2 * df2);
			}

			double[] sorted = (double[])dists.Clone();
			Array.Sort(sorted);
			int k = Math.Min(Neighbors - 1, sorted.Length - 1);
			if (k < 0) k = 0;
			double threshold = sorted[k];

			double bull = 0.0;
			double bear = 0.0;
			for (int i = 0; i < dists.Length; i++)
			{
				if (dists[i] <= threshold)
				{
					int back = i + 1; // Pine: targetTrend[i+1] where array index i corresponds to bar (i+1) ago
					if (back > CurrentBar) continue;
					int t = targetTrendSeries[back];
					if (t > 0) bull += 1.0;
					else       bear += 1.0;
				}
			}

			double total = bull + bear;
			return total > 0.0 ? (bull / total) * 100.0 : mlProb;
		}

		private void DetectRejection(double atrV)
		{
			double bodySize  = Math.Abs(Close[0] - Open[0]);
			double upperWick = High[0] - Math.Max(Open[0], Close[0]);
			double lowerWick = Math.Min(Open[0], Close[0]) - Low[0];

			bool bear = !mlBullish
				&& High[0]  > stValue
				&& Close[0] < stValue
				&& upperWick > bodySize * WickMultiplier
				&& (CurrentBar - lastBubbleBar) >= BubbleGap;

			bool bull = mlBullish
				&& Low[0]   < stValue
				&& Close[0] > stValue
				&& lowerWick > bodySize * WickMultiplier
				&& (CurrentBar - lastBubbleBar) >= BubbleGap;

			sBullRejection[0] = bull;
			sBearRejection[0] = bear;

			if (!ShowOrbs || (!bull && !bear)) return;

			double stem        = atrV * 1.5;
			int    sizeUnits   = DynamicBubbleSize();
			string volText     = FormatVolume(Volume[0]);
			Brush  themeBrush  = bull ? BullColor : BearColor;
			Color  themeColor  = ((SolidColorBrush)themeBrush).Color;

			double anchor    = bull ? Low[0]  : High[0];
			double orbCenter = bull ? Low[0] - stem : High[0] + stem;

			Draw.Line(this, "knnstem_" + CurrentBar, false,
				0, anchor, 0, orbCenter,
				AlphaBrush(themeColor, 102), DashStyleHelper.Dash, 1);

			RejectionOrb orb;
			orb.BarIdx     = CurrentBar;
			orb.IsBull     = bull;
			orb.Anchor     = anchor;
			orb.OrbCenter  = orbCenter;
			orb.SizeUnits  = sizeUnits;
			orb.VolumeText = volText;
			orbs.Add(orb);

			lastBubbleBar = CurrentBar;
		}

		private int DynamicBubbleSize()
		{
			double avg = volMa[0];
			double sd  = volSd[0];
			double z   = (sd > 0.0) ? (Volume[0] - avg) / sd : 0.0;
			int sz = (int)Math.Round(14 + (z * 2.0));
			if (sz < 8)  sz = 8;
			if (sz > 30) sz = 30;
			return sz;
		}

		private static string FormatVolume(double v)
		{
			if (v >= 1_000_000_000) return (v / 1_000_000_000.0).ToString("0.##", CultureInfo.InvariantCulture) + "B";
			if (v >= 1_000_000)     return (v / 1_000_000.0    ).ToString("0.##", CultureInfo.InvariantCulture) + "M";
			if (v >= 1_000)         return (v / 1_000.0        ).ToString("0.#",  CultureInfo.InvariantCulture) + "K";
			return v.ToString("0", CultureInfo.InvariantCulture);
		}

		#endregion

		#region OnRender

		public override void OnRenderTargetChanged()
		{
			try
			{
				ReleaseRenderResources();
				if (RenderTarget == null) return;

				Color bullRgb = ((SolidColorBrush)BullColor).Color;
				Color bearRgb = ((SolidColorBrush)BearColor).Color;

				dxBullSolid    = MakeDxBrush(bullRgb, 1f);
				dxBearSolid    = MakeDxBrush(bearRgb, 1f);

				BuildGlowPalettes(bullRgb, bearRgb);
				dxOrbStem      = MakeDxBrush(Color.FromRgb(160, 160, 160), 0.6f);
				dxOrbWhite     = MakeDxBrush(Colors.White, 1f);
				dxOrbBlackHalo = MakeDxBrush(Colors.Black, 1f);
				dxLabelBg      = MakeDxBrush(Colors.Black, 0.6f);
				dxLabelText    = MakeDxBrush(Colors.White, 1f);

				dxDashBg       = MakeDxBrush(Color.FromRgb(0x16, 0x16, 0x16), 0.92f);
				dxDashBorder   = MakeDxBrush(Color.FromRgb(0x2E, 0x2E, 0x2E), 1f);
				dxDashHeader   = MakeDxBrush(Color.FromRgb(0x80, 0x80, 0x80), 1f);
				dxDashData     = MakeDxBrush(Color.FromRgb(0xDB, 0xDB, 0xDB), 1f);

				BuildTextFormats();
			}
			catch { }
		}

		private void ReleaseRenderResources()
		{
			SafeDispose(ref dxBullSolid);
			SafeDispose(ref dxBearSolid);
			SafeDispose(ref dxOrbStem);
			SafeDispose(ref dxOrbWhite);
			SafeDispose(ref dxOrbBlackHalo);
			SafeDispose(ref dxLabelBg);
			SafeDispose(ref dxLabelText);
			SafeDispose(ref dxDashBg);
			SafeDispose(ref dxDashBorder);
			SafeDispose(ref dxDashHeader);
			SafeDispose(ref dxDashData);

			DisposePalette(ref dxBullGlow);
			DisposePalette(ref dxBearGlow);

			if (tfTitle != null) { tfTitle.Dispose(); tfTitle = null; }
			if (tfBody  != null) { tfBody.Dispose();  tfBody  = null; }
			if (tfOrb   != null) { tfOrb.Dispose();   tfOrb   = null; }
			lastTitleSize = -1;
			lastBodySize  = -1;
		}

		private void BuildGlowPalettes(Color bullRgb, Color bearRgb)
		{
			DisposePalette(ref dxBullGlow);
			DisposePalette(ref dxBearGlow);

			dxBullGlow = new SharpDX.Direct2D1.Brush[GLOW_LEVELS];
			dxBearGlow = new SharpDX.Direct2D1.Brush[GLOW_LEVELS];
			for (int i = 0; i < GLOW_LEVELS; i++)
			{
				float alpha = (i + 1) / (float)GLOW_LEVELS * 0.25f; // 0..0.25
				dxBullGlow[i] = MakeDxBrush(bullRgb, alpha);
				dxBearGlow[i] = MakeDxBrush(bearRgb, alpha);
			}
		}

		private static void DisposePalette(ref SharpDX.Direct2D1.Brush[] palette)
		{
			if (palette == null) return;
			for (int i = 0; i < palette.Length; i++)
			{
				if (palette[i] != null) { try { palette[i].Dispose(); } catch { } palette[i] = null; }
			}
			palette = null;
		}

		private void BuildTextFormats()
		{
			int titleSize = DashTitlePx();
			int bodySize  = DashBodyPx();

			if (tfTitle != null) { tfTitle.Dispose(); tfTitle = null; }
			if (tfBody  != null) { tfBody.Dispose();  tfBody  = null; }
			if (tfOrb   != null) { tfOrb.Dispose();   tfOrb   = null; }

			using (var dwFactory = new SharpDX.DirectWrite.Factory())
			{
				tfTitle = new SharpDX.DirectWrite.TextFormat(dwFactory, "Segoe UI", SharpDX.DirectWrite.FontWeight.SemiBold, SharpDX.DirectWrite.FontStyle.Normal, titleSize);
				tfBody  = new SharpDX.DirectWrite.TextFormat(dwFactory, "Segoe UI", SharpDX.DirectWrite.FontWeight.Normal,   SharpDX.DirectWrite.FontStyle.Normal, bodySize);
				tfOrb   = new SharpDX.DirectWrite.TextFormat(dwFactory, "Segoe UI", SharpDX.DirectWrite.FontWeight.SemiBold, SharpDX.DirectWrite.FontStyle.Normal, Math.Max(10, bodySize - 1));
			}

			lastTitleSize = titleSize;
			lastBodySize  = bodySize;
		}

		private int DashTitlePx()
		{
			switch (DashboardSize)
			{
				case KnnSupertrendHorizon_DashSize.Tiny:   return 10;
				case KnnSupertrendHorizon_DashSize.Small:  return 12;
				case KnnSupertrendHorizon_DashSize.Normal: return 14;
				case KnnSupertrendHorizon_DashSize.Large:  return 17;
				case KnnSupertrendHorizon_DashSize.Huge:   return 20;
			}
			return 12;
		}

		private int DashBodyPx()
		{
			switch (DashboardSize)
			{
				case KnnSupertrendHorizon_DashSize.Tiny:   return 9;
				case KnnSupertrendHorizon_DashSize.Small:  return 11;
				case KnnSupertrendHorizon_DashSize.Normal: return 13;
				case KnnSupertrendHorizon_DashSize.Large:  return 15;
				case KnnSupertrendHorizon_DashSize.Huge:   return 18;
			}
			return 11;
		}

		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			base.OnRender(chartControl, chartScale);

			if (RenderTarget == null) return;
			if (ChartBars    == null) return;
			if (!IsVisible)           return;
			if (IsInHitTest)          return;
			if (dxBullSolid  == null) return;
			if (dxBullGlow   == null) return;

			if (lastBodySize != DashBodyPx() || lastTitleSize != DashTitlePx())
				BuildTextFormats();

			RenderGlowBars(chartControl, chartScale);
			RenderOrbs(chartControl, chartScale);
			RenderDashboard(chartControl);
		}

		private void RenderGlowBars(ChartControl chartControl, ChartScale chartScale)
		{
			if (dxBullGlow == null || dxBearGlow == null) return;

			int fromIdx = Math.Max(ChartBars.FromIndex, 0);
			int toIdx   = Math.Min(ChartBars.ToIndex, CurrentBars[0]);
			if (toIdx <= fromIdx) return;

			float panelTop    = ChartPanel.Y;
			float panelBottom = ChartPanel.Y + ChartPanel.H;
			float barHeight   = Math.Max(6f, ChartPanel.H * 0.035f);

			for (int j = fromIdx; j <= toIdx; j++)
			{
				if (!sMlBullish.IsValidDataPointAt(j)) continue;

				float xL = chartControl.GetXByBarIndex(ChartBars, j);
				float xR = (j + 1 <= ChartBars.ToIndex)
					? chartControl.GetXByBarIndex(ChartBars, j + 1)
					: xL + (j > fromIdx ? xL - chartControl.GetXByBarIndex(ChartBars, j - 1) : 6f);
				float w = Math.Max(1f, xR - xL);

				bool bull = sMlBullish.GetValueAt(j);
				double gp = sGlowPower.IsValidDataPointAt(j) ? sGlowPower.GetValueAt(j) : 0.0;
				int idx = (int)Math.Round((gp / 100.0) * (GLOW_LEVELS - 1));
				if (idx < 0) idx = 0;
				if (idx >= GLOW_LEVELS) idx = GLOW_LEVELS - 1;

				if (bull)
				{
					var rect  = new SharpDX.RectangleF(xL, panelBottom - barHeight, w, barHeight);
					RenderTarget.FillRectangle(rect, dxBullGlow[idx]);
				}
				else
				{
					var rect  = new SharpDX.RectangleF(xL, panelTop, w, barHeight);
					RenderTarget.FillRectangle(rect, dxBearGlow[idx]);
				}
			}
		}

		private void RenderOrbs(ChartControl chartControl, ChartScale chartScale)
		{
			if (!ShowOrbs || orbs.Count == 0) return;

			int fromIdx = ChartBars.FromIndex;
			int toIdx   = ChartBars.ToIndex;

			Color bullRgb = ((SolidColorBrush)BullColor).Color;
			Color bearRgb = ((SolidColorBrush)BearColor).Color;

			for (int oi = 0; oi < orbs.Count; oi++)
			{
				var orb = orbs[oi];
				if (orb.BarIdx < fromIdx - 5 || orb.BarIdx > toIdx + 5) continue;

				float cx = chartControl.GetXByBarIndex(ChartBars, orb.BarIdx);
				float cy = (float)chartScale.GetYByValue(orb.OrbCenter);
				float baseR = orb.SizeUnits * 1.4f;

				Color theme = orb.IsBull ? bullRgb : bearRgb;

				DrawOrbLayer(cx, cy + baseR * 0.4f, baseR + 4f, Colors.Black, 0.18f);
				DrawOrbLayer(cx, cy,                baseR + 2f, theme,        0.30f);
				DrawOrbLayer(cx, cy,                baseR,      theme,        0.85f);
				DrawOrbLayer(cx - baseR * 0.25f, cy - baseR * 0.25f, baseR * 0.55f, Colors.White, 0.18f);
				DrawOrbLayer(cx - baseR * 0.4f,  cy - baseR * 0.4f,  baseR * 0.18f, Colors.White, 0.65f);

				DrawOrbLabel(orb, chartControl, chartScale, baseR);
			}
		}

		private void DrawOrbLayer(float cx, float cy, float r, Color color, float alpha)
		{
			var brush = MakeDxBrush(color, alpha);
			var ellipse = new SharpDX.Direct2D1.Ellipse(new SharpDX.Vector2(cx, cy), r, r);
			RenderTarget.FillEllipse(ellipse, brush);
			brush.Dispose();
		}

		private void DrawOrbLabel(RejectionOrb orb, ChartControl chartControl, ChartScale chartScale, float baseR)
		{
			if (tfOrb == null) return;

			float cx = chartControl.GetXByBarIndex(ChartBars, orb.BarIdx);
			float labelY = (float)chartScale.GetYByValue(orb.OrbCenter)
				+ (orb.IsBull ? -(baseR * 6f) : (baseR * 6f));

			var layout = new SharpDX.DirectWrite.TextLayout(
				Core.Globals.DirectWriteFactory, orb.VolumeText, tfOrb, 200f, 32f);

			float w = layout.Metrics.Width  + 8f;
			float h = layout.Metrics.Height + 4f;

			var bgRect = new SharpDX.RectangleF(cx - w / 2f, labelY - h / 2f, w, h);
			RenderTarget.FillRectangle(bgRect, dxLabelBg);

			RenderTarget.DrawTextLayout(
				new SharpDX.Vector2(cx - layout.Metrics.Width / 2f, labelY - layout.Metrics.Height / 2f),
				layout, dxLabelText);

			layout.Dispose();
		}

		private void RenderDashboard(ChartControl chartControl)
		{
			if (!ShowDashboard) return;
			if (tfTitle == null || tfBody == null) return;

			float pad     = 8f;
			float rowH    = lastBodySize + 6f;
			float titleH  = lastTitleSize + 8f;
			float colW    = Math.Max(120f, lastBodySize * 8.5f);
			float w       = colW * 2f + pad * 2f;
			float h       = titleH + rowH * 6 + pad * 2f;

			float originX, originY;
			switch (DashboardPosition)
			{
				case KnnSupertrendHorizon_DashCorner.BottomRight:
					originX = ChartPanel.X + ChartPanel.W - w - 8f;
					originY = ChartPanel.Y + ChartPanel.H - h - 8f;
					break;
				case KnnSupertrendHorizon_DashCorner.BottomLeft:
					originX = ChartPanel.X + 8f;
					originY = ChartPanel.Y + ChartPanel.H - h - 8f;
					break;
				default:
					originX = ChartPanel.X + ChartPanel.W - w - 8f;
					originY = ChartPanel.Y + 8f;
					break;
			}

			var bg = new SharpDX.RectangleF(originX, originY, w, h);
			RenderTarget.FillRectangle(bg, dxDashBg);
			RenderTarget.DrawRectangle(bg, dxDashBorder, 1f);

			float y = originY + pad;
			DrawCenteredCell(originX + pad, y, w - pad * 2, titleH, indName, tfTitle, dxDashData);
			y += titleH;
			DrawSeparator(originX + pad, y + 2f, w - pad * 2);
			y += 4f;

			double smoothed = sSmoothedProb.IsValidDataPoint(0) ? sSmoothedProb[0] : 50.0;
			double f2v      = f2Series.IsValidDataPoint(0)      ? f2Series[0]      : 0.0;
			double stV      = !double.IsNaN(stValue) ? stValue : Close[0];
			double distPct  = (Close[0] != 0.0) ? Math.Abs(Close[0] - stV) / Close[0] * 100.0 : 0.0;

			SharpDX.Direct2D1.Brush trendBrush = mlBullish ? dxBullSolid : dxBearSolid;
			DrawRowLeft (originX + pad,        y, colW, rowH, "Trend Direction", dxDashHeader);
			DrawRowRight(originX + pad + colW, y, colW, rowH, mlBullish ? "Bullish" : "Bearish", trendBrush);
			y += rowH;

			DrawRowLeft (originX + pad,        y, colW, rowH, "ML Confidence", dxDashHeader);
			DrawRowRight(originX + pad + colW, y, colW, rowH, smoothed.ToString("0.0", CultureInfo.InvariantCulture) + "%", dxDashData);
			y += rowH;

			DrawSeparator(originX + pad, y + 2f, w - pad * 2);
			y += 4f;

			DrawRowLeft (originX + pad,        y, colW, rowH, "Bars In Trend", dxDashHeader);
			DrawRowRight(originX + pad + colW, y, colW, rowH, barsSinceChange.ToString(CultureInfo.InvariantCulture), dxDashData);
			y += rowH;

			DrawRowLeft (originX + pad,        y, colW, rowH, "ST Distance", dxDashHeader);
			DrawRowRight(originX + pad + colW, y, colW, rowH, distPct.ToString("0.##", CultureInfo.InvariantCulture) + "%", dxDashData);
			y += rowH;

			DrawSeparator(originX + pad, y + 2f, w - pad * 2);
			y += 4f;

			DrawRowLeft (originX + pad,        y, colW, rowH, "Rel. Volatility", dxDashHeader);
			DrawRowRight(originX + pad + colW, y, colW, rowH, f2v.ToString("0.##", CultureInfo.InvariantCulture) + "%", dxDashData);
		}

		private void DrawRowLeft(float x, float y, float w, float h, string text, SharpDX.Direct2D1.Brush brush)
		{
			var layout = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, text, tfBody, w, h);
			layout.TextAlignment      = SharpDX.DirectWrite.TextAlignment.Leading;
			layout.ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center;
			RenderTarget.DrawTextLayout(new SharpDX.Vector2(x, y), layout, brush);
			layout.Dispose();
		}

		private void DrawRowRight(float x, float y, float w, float h, string text, SharpDX.Direct2D1.Brush brush)
		{
			var layout = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, text, tfBody, w, h);
			layout.TextAlignment      = SharpDX.DirectWrite.TextAlignment.Trailing;
			layout.ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center;
			RenderTarget.DrawTextLayout(new SharpDX.Vector2(x, y), layout, brush);
			layout.Dispose();
		}

		private void DrawCenteredCell(float x, float y, float w, float h, string text, SharpDX.DirectWrite.TextFormat format, SharpDX.Direct2D1.Brush brush)
		{
			var layout = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, text, format, w, h);
			layout.TextAlignment      = SharpDX.DirectWrite.TextAlignment.Center;
			layout.ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center;
			RenderTarget.DrawTextLayout(new SharpDX.Vector2(x, y), layout, brush);
			layout.Dispose();
		}

		private void DrawSeparator(float x, float y, float w)
		{
			RenderTarget.DrawLine(new SharpDX.Vector2(x, y), new SharpDX.Vector2(x + w, y), dxDashBorder, 1f);
		}

		#endregion

		#region Helpers

		private SharpDX.Direct2D1.Brush MakeDxBrush(Color c, float alpha)
		{
			if (alpha < 0f) alpha = 0f;
			if (alpha > 1f) alpha = 1f;
			return new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
				new SharpDX.Color4(c.R / 255f, c.G / 255f, c.B / 255f, alpha));
		}

		private static void SafeDispose(ref SharpDX.Direct2D1.Brush b)
		{
			if (b != null) { try { b.Dispose(); } catch { } b = null; }
		}

		private static Brush AlphaBrush(Color c, int alpha)
		{
			if (alpha < 0)   alpha = 0;
			if (alpha > 255) alpha = 255;
			SolidColorBrush sb = new SolidColorBrush(Color.FromArgb((byte)alpha, c.R, c.G, c.B));
			if (sb.CanFreeze) sb.Freeze();
			return sb;
		}

		private static Brush EnsureFrozen(Brush b)
		{
			if (b != null && b.CanFreeze && !b.IsFrozen) b.Freeze();
			return b;
		}

		#endregion

		#region Public Series

		[Browsable(false)][XmlIgnore] public Series<double> MlSupertrend  { get { return Values[0]; } }
		[Browsable(false)][XmlIgnore] public Series<double> SmoothedProb  { get { Update(); return sSmoothedProb;  } }
		[Browsable(false)][XmlIgnore] public Series<bool>   MlBullish     { get { Update(); return sMlBullish;     } }
		[Browsable(false)][XmlIgnore] public Series<bool>   BullRejection { get { Update(); return sBullRejection; } }
		[Browsable(false)][XmlIgnore] public Series<bool>   BearRejection { get { Update(); return sBearRejection; } }

		#endregion
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private indTradingView.KnnSupertrendHorizon[] cacheKnnSupertrendHorizon;
		public indTradingView.KnnSupertrendHorizon KnnSupertrendHorizon(int neighbors, int windowSize, int atrLength, double factor, bool smoothSource, int smoothingLength, double mlBuffer, bool showOrbs, double wickMultiplier, int bubbleGap, int liquidSmoothness, double vibrancy, bool colorCandles, bool showDashboard, KnnSupertrendHorizon_DashCorner dashboardPosition, KnnSupertrendHorizon_DashSize dashboardSize)
		{
			return KnnSupertrendHorizon(Input, neighbors, windowSize, atrLength, factor, smoothSource, smoothingLength, mlBuffer, showOrbs, wickMultiplier, bubbleGap, liquidSmoothness, vibrancy, colorCandles, showDashboard, dashboardPosition, dashboardSize);
		}

		public indTradingView.KnnSupertrendHorizon KnnSupertrendHorizon(ISeries<double> input, int neighbors, int windowSize, int atrLength, double factor, bool smoothSource, int smoothingLength, double mlBuffer, bool showOrbs, double wickMultiplier, int bubbleGap, int liquidSmoothness, double vibrancy, bool colorCandles, bool showDashboard, KnnSupertrendHorizon_DashCorner dashboardPosition, KnnSupertrendHorizon_DashSize dashboardSize)
		{
			if (cacheKnnSupertrendHorizon != null)
				for (int idx = 0; idx < cacheKnnSupertrendHorizon.Length; idx++)
					if (cacheKnnSupertrendHorizon[idx] != null && cacheKnnSupertrendHorizon[idx].Neighbors == neighbors && cacheKnnSupertrendHorizon[idx].WindowSize == windowSize && cacheKnnSupertrendHorizon[idx].AtrLength == atrLength && cacheKnnSupertrendHorizon[idx].Factor == factor && cacheKnnSupertrendHorizon[idx].SmoothSource == smoothSource && cacheKnnSupertrendHorizon[idx].SmoothingLength == smoothingLength && cacheKnnSupertrendHorizon[idx].MlBuffer == mlBuffer && cacheKnnSupertrendHorizon[idx].ShowOrbs == showOrbs && cacheKnnSupertrendHorizon[idx].WickMultiplier == wickMultiplier && cacheKnnSupertrendHorizon[idx].BubbleGap == bubbleGap && cacheKnnSupertrendHorizon[idx].LiquidSmoothness == liquidSmoothness && cacheKnnSupertrendHorizon[idx].Vibrancy == vibrancy && cacheKnnSupertrendHorizon[idx].ColorCandles == colorCandles && cacheKnnSupertrendHorizon[idx].ShowDashboard == showDashboard && cacheKnnSupertrendHorizon[idx].DashboardPosition == dashboardPosition && cacheKnnSupertrendHorizon[idx].DashboardSize == dashboardSize && cacheKnnSupertrendHorizon[idx].EqualsInput(input))
						return cacheKnnSupertrendHorizon[idx];
			return CacheIndicator<indTradingView.KnnSupertrendHorizon>(new indTradingView.KnnSupertrendHorizon(){ Neighbors = neighbors, WindowSize = windowSize, AtrLength = atrLength, Factor = factor, SmoothSource = smoothSource, SmoothingLength = smoothingLength, MlBuffer = mlBuffer, ShowOrbs = showOrbs, WickMultiplier = wickMultiplier, BubbleGap = bubbleGap, LiquidSmoothness = liquidSmoothness, Vibrancy = vibrancy, ColorCandles = colorCandles, ShowDashboard = showDashboard, DashboardPosition = dashboardPosition, DashboardSize = dashboardSize }, input, ref cacheKnnSupertrendHorizon);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.KnnSupertrendHorizon KnnSupertrendHorizon(int neighbors, int windowSize, int atrLength, double factor, bool smoothSource, int smoothingLength, double mlBuffer, bool showOrbs, double wickMultiplier, int bubbleGap, int liquidSmoothness, double vibrancy, bool colorCandles, bool showDashboard, KnnSupertrendHorizon_DashCorner dashboardPosition, KnnSupertrendHorizon_DashSize dashboardSize)
		{
			return indicator.KnnSupertrendHorizon(Input, neighbors, windowSize, atrLength, factor, smoothSource, smoothingLength, mlBuffer, showOrbs, wickMultiplier, bubbleGap, liquidSmoothness, vibrancy, colorCandles, showDashboard, dashboardPosition, dashboardSize);
		}

		public Indicators.indTradingView.KnnSupertrendHorizon KnnSupertrendHorizon(ISeries<double> input , int neighbors, int windowSize, int atrLength, double factor, bool smoothSource, int smoothingLength, double mlBuffer, bool showOrbs, double wickMultiplier, int bubbleGap, int liquidSmoothness, double vibrancy, bool colorCandles, bool showDashboard, KnnSupertrendHorizon_DashCorner dashboardPosition, KnnSupertrendHorizon_DashSize dashboardSize)
		{
			return indicator.KnnSupertrendHorizon(input, neighbors, windowSize, atrLength, factor, smoothSource, smoothingLength, mlBuffer, showOrbs, wickMultiplier, bubbleGap, liquidSmoothness, vibrancy, colorCandles, showDashboard, dashboardPosition, dashboardSize);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.KnnSupertrendHorizon KnnSupertrendHorizon(int neighbors, int windowSize, int atrLength, double factor, bool smoothSource, int smoothingLength, double mlBuffer, bool showOrbs, double wickMultiplier, int bubbleGap, int liquidSmoothness, double vibrancy, bool colorCandles, bool showDashboard, KnnSupertrendHorizon_DashCorner dashboardPosition, KnnSupertrendHorizon_DashSize dashboardSize)
		{
			return indicator.KnnSupertrendHorizon(Input, neighbors, windowSize, atrLength, factor, smoothSource, smoothingLength, mlBuffer, showOrbs, wickMultiplier, bubbleGap, liquidSmoothness, vibrancy, colorCandles, showDashboard, dashboardPosition, dashboardSize);
		}

		public Indicators.indTradingView.KnnSupertrendHorizon KnnSupertrendHorizon(ISeries<double> input , int neighbors, int windowSize, int atrLength, double factor, bool smoothSource, int smoothingLength, double mlBuffer, bool showOrbs, double wickMultiplier, int bubbleGap, int liquidSmoothness, double vibrancy, bool colorCandles, bool showDashboard, KnnSupertrendHorizon_DashCorner dashboardPosition, KnnSupertrendHorizon_DashSize dashboardSize)
		{
			return indicator.KnnSupertrendHorizon(input, neighbors, windowSize, atrLength, factor, smoothSource, smoothingLength, mlBuffer, showOrbs, wickMultiplier, bubbleGap, liquidSmoothness, vibrancy, colorCandles, showDashboard, dashboardPosition, dashboardSize);
		}
	}
}

#endregion
