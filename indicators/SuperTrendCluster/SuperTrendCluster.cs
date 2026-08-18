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

// NT8 Version of SuperTrend Cluster (Zeiierman)
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the
// Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License (CC BY-NC-SA 4.0).
// The original Pine Script™ code is by Zeiierman and can be found at: https://www.tradingview.com/script/r8j7m88J-SuperTrend-Cluster-Zeiierman/
// Adaptation for NinjaTrader by jack@mydailytake.com
// © 2026 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the CC BY-NC-SA 4.0 license. Full license details at https://creativecommons.org/licenses/by-nc-sa/4.0/
// The use of Zeiierman name or its adapted code in this work does not imply endorsement by the original authors.
//
// Notes:
//   Five-member SuperTrend ensemble with weighted consensus filtering:
//     • Each member: ATR(atrLen) × Factor bands placed around an MA-smoothed source (HLC3).
//       MA type independently selectable per member (SMA / EMA / LinReg / WMA / HMA / RMA).
//     • Per-bar: weighted bull / bear share computed across the 5 members. Final regime flips only
//       when both the consensus share crosses ConsensusThreshold AND the user-selected base ST
//       member agrees.
//     • Cluster line: weighted-average SuperTrend across agreeing members (bull-side or bear-side).
//     • Cloud fill: SharpDX trapezoid between the active cluster line and SMA(HLC3, CloudReferenceLength).
//     • Bar coloring: gradient interpolation between Neutral and Bull / Bear by cluster strength.
//     • Major flip labels (Bull Cluster %, Bear Cluster %) + base-ST flip dots when the chosen
//       member changes direction.
//   3 alertconditions stripped per house QC.

#region Enums SuperTrendCluster

public enum SuperTrendCluster_MAType
{
	SMA,
	EMA,
	LinReg,
	WMA,
	HMA,
	RMA
}

#endregion

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	#region Categories
	[Gui.CategoryOrder("Cluster Engine",	10100)]
	[Gui.CategoryOrder("Visual Analytics",	10200)]
	[Gui.CategoryOrder("Cloud Fill",		10300)]
	[Gui.CategoryOrder("SuperTrend 1",		10400)]
	[Gui.CategoryOrder("SuperTrend 2",		10500)]
	[Gui.CategoryOrder("SuperTrend 3",		10600)]
	[Gui.CategoryOrder("SuperTrend 4",		10700)]
	[Gui.CategoryOrder("SuperTrend 5",		10800)]
	#endregion

	public class SuperTrendCluster : Indicator
	{
		#region indInfo

		private string indName        = "SuperTrend Cluster (Zeiierman)";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by Zeiierman can be found here: https://www.tradingview.com/script/r8j7m88J-SuperTrend-Cluster-Zeiierman/";

		#endregion

		#region Properties — Cluster engine

		[NinjaScriptProperty]
		[Range(0.0, 1.0)]
		[Display(Order = 1, GroupName = "Cluster Engine", Name = "Consensus Threshold",
			Description = "Minimum weighted bull / bear share required to register the regime.")]
		public double ConsensusThreshold { get; set; }

		[NinjaScriptProperty]
		[Range(1, 5)]
		[Display(Order = 2, GroupName = "Cluster Engine", Name = "Base SuperTrend Index",
			Description = "Which of the five members drives flip markers + final direction alignment.")]
		public int BaseIndex { get; set; }

		#endregion

		#region Properties — Visual Analytics

		[NinjaScriptProperty]
		[Display(Order = 1, GroupName = "Visual Analytics", Name = "Dynamic Bar Coloring",
			Description = "Recolor bars by cluster-strength gradient (Neutral ↔ Bull / Bear).")]
		public bool DynamicBarColoring { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 2, GroupName = "Visual Analytics", Name = "Show Cluster Labels",
			Description = "Render Bull Cluster / Bear Cluster labels at the base ST flip bar.")]
		public bool ShowClusterLabels { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 3, GroupName = "Visual Analytics", Name = "Show Base ST Flip Dots",
			Description = "Render dots at the base ST line on flip bars.")]
		public bool ShowFlipDots { get; set; }

		[XmlIgnore]
		[Display(Order = 4, GroupName = "Visual Analytics", Name = "Bull Color")]
		public Brush BullColor { get; set; }
			[Browsable(false)]
			public string BullColorSerialize
			{
				get { return Serialize.BrushToString(BullColor); }
				set { BullColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore]
		[Display(Order = 5, GroupName = "Visual Analytics", Name = "Bear Color")]
		public Brush BearColor { get; set; }
			[Browsable(false)]
			public string BearColorSerialize
			{
				get { return Serialize.BrushToString(BearColor); }
				set { BearColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore]
		[Display(Order = 6, GroupName = "Visual Analytics", Name = "Neutral Color")]
		public Brush NeutralColor { get; set; }
			[Browsable(false)]
			public string NeutralColorSerialize
			{
				get { return Serialize.BrushToString(NeutralColor); }
				set { NeutralColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		#endregion

		#region Properties — Cloud Fill

		[NinjaScriptProperty]
		[Display(Order = 1, GroupName = "Cloud Fill", Name = "Show Cloud Fill",
			Description = "Render the translucent cloud between the active cluster line and SMA(HLC3, ref).")]
		public bool ShowCloud { get; set; }

		[NinjaScriptProperty]
		[Range(1, 200)]
		[Display(Order = 2, GroupName = "Cloud Fill", Name = "Cloud Reference Length",
			Description = "SMA length on HLC3 used as the cloud's other edge.")]
		public int CloudReferenceLength { get; set; }

		[NinjaScriptProperty]
		[Range(0, 95)]
		[Display(Order = 3, GroupName = "Cloud Fill", Name = "Cloud Transparency",
			Description = "0 = solid, 95 = nearly invisible (Pine semantics).")]
		public int CloudTransparency { get; set; }

		#endregion

		#region Properties — SuperTrend members 1-5

		[NinjaScriptProperty][Range(1, 999)] [Display(Order = 1, GroupName = "SuperTrend 1", Name = "ATR Length")]    public int A1 { get; set; }
		[NinjaScriptProperty][Range(0.01, 50)][Display(Order = 2, GroupName = "SuperTrend 1", Name = "Factor")]       public double F1 { get; set; }
		[NinjaScriptProperty]                 [Display(Order = 3, GroupName = "SuperTrend 1", Name = "Smoothing")]    public SuperTrendCluster_MAType M1 { get; set; }
		[NinjaScriptProperty][Range(1, 999)] [Display(Order = 4, GroupName = "SuperTrend 1", Name = "MA Length")]     public int L1 { get; set; }
		[NinjaScriptProperty][Range(0.0, 5)] [Display(Order = 5, GroupName = "SuperTrend 1", Name = "Weight")]        public double W1 { get; set; }

		[NinjaScriptProperty][Range(1, 999)] [Display(Order = 1, GroupName = "SuperTrend 2", Name = "ATR Length")]    public int A2 { get; set; }
		[NinjaScriptProperty][Range(0.01, 50)][Display(Order = 2, GroupName = "SuperTrend 2", Name = "Factor")]       public double F2 { get; set; }
		[NinjaScriptProperty]                 [Display(Order = 3, GroupName = "SuperTrend 2", Name = "Smoothing")]    public SuperTrendCluster_MAType M2 { get; set; }
		[NinjaScriptProperty][Range(1, 999)] [Display(Order = 4, GroupName = "SuperTrend 2", Name = "MA Length")]     public int L2 { get; set; }
		[NinjaScriptProperty][Range(0.0, 5)] [Display(Order = 5, GroupName = "SuperTrend 2", Name = "Weight")]        public double W2 { get; set; }

		[NinjaScriptProperty][Range(1, 999)] [Display(Order = 1, GroupName = "SuperTrend 3", Name = "ATR Length")]    public int A3 { get; set; }
		[NinjaScriptProperty][Range(0.01, 50)][Display(Order = 2, GroupName = "SuperTrend 3", Name = "Factor")]       public double F3 { get; set; }
		[NinjaScriptProperty]                 [Display(Order = 3, GroupName = "SuperTrend 3", Name = "Smoothing")]    public SuperTrendCluster_MAType M3 { get; set; }
		[NinjaScriptProperty][Range(1, 999)] [Display(Order = 4, GroupName = "SuperTrend 3", Name = "MA Length")]     public int L3 { get; set; }
		[NinjaScriptProperty][Range(0.0, 5)] [Display(Order = 5, GroupName = "SuperTrend 3", Name = "Weight")]        public double W3 { get; set; }

		[NinjaScriptProperty][Range(1, 999)] [Display(Order = 1, GroupName = "SuperTrend 4", Name = "ATR Length")]    public int A4 { get; set; }
		[NinjaScriptProperty][Range(0.01, 50)][Display(Order = 2, GroupName = "SuperTrend 4", Name = "Factor")]       public double F4 { get; set; }
		[NinjaScriptProperty]                 [Display(Order = 3, GroupName = "SuperTrend 4", Name = "Smoothing")]    public SuperTrendCluster_MAType M4 { get; set; }
		[NinjaScriptProperty][Range(1, 999)] [Display(Order = 4, GroupName = "SuperTrend 4", Name = "MA Length")]     public int L4 { get; set; }
		[NinjaScriptProperty][Range(0.0, 5)] [Display(Order = 5, GroupName = "SuperTrend 4", Name = "Weight")]        public double W4 { get; set; }

		[NinjaScriptProperty][Range(1, 999)] [Display(Order = 1, GroupName = "SuperTrend 5", Name = "ATR Length")]    public int A5 { get; set; }
		[NinjaScriptProperty][Range(0.01, 50)][Display(Order = 2, GroupName = "SuperTrend 5", Name = "Factor")]       public double F5 { get; set; }
		[NinjaScriptProperty]                 [Display(Order = 3, GroupName = "SuperTrend 5", Name = "Smoothing")]    public SuperTrendCluster_MAType M5 { get; set; }
		[NinjaScriptProperty][Range(1, 999)] [Display(Order = 4, GroupName = "SuperTrend 5", Name = "MA Length")]     public int L5 { get; set; }
		[NinjaScriptProperty][Range(0.0, 5)] [Display(Order = 5, GroupName = "SuperTrend 5", Name = "Weight")]        public double W5 { get; set; }

		#endregion

		#region Public outputs

		[Browsable(false)][XmlIgnore] public Series<double> ClusterLine { get { return Values[0]; } }
		[Browsable(false)][XmlIgnore] public Series<int>    Direction   { get { Update(); return sDir; } }
		[Browsable(false)][XmlIgnore] public Series<double> BullShare   { get { Update(); return sBull; } }
		[Browsable(false)][XmlIgnore] public Series<double> BearShare   { get { Update(); return sBear; } }

		#endregion

		#region Variables

		private Series<double> sHlc3;

		// Per-member MA series (output of the chosen MA on HLC3 source).
		private ISeries<double>[] sMa = new ISeries<double>[5];

		// Per-member ATR primitives.
		private ATR[] memAtr = new ATR[5];

		// SuperTrend state per member.
		private Series<double>[] sUb  = new Series<double>[5];
		private Series<double>[] sLb  = new Series<double>[5];
		private Series<int>[]    sDi  = new Series<int>[5];
		private Series<double>[] sSt  = new Series<double>[5];

		// Per-MA-type primitives (lazy: only the ones requested).
		private Dictionary<int, object> maPrimitives = new Dictionary<int, object>();

		// RMA state per member (when M = RMA).
		private Series<double>[] sRma = new Series<double>[5];

		// Final outputs.
		private SMA cloudRefSma;
		private Series<int>    sDir;
		private Series<double> sBull, sBear, sDLast;
		private Series<double> sClusterUp, sClusterDn;	// for bull / bear weighted lines

		// SharpDX cloud brushes.
		private SharpDX.Direct2D1.SolidColorBrush dxCloudBull, dxCloudBear;

		#endregion

		#region OnStateChange

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description					= indDescription;
				Name						= indName;
				Calculate					= Calculate.OnBarClose;
				IsOverlay					= true;
				DisplayInDataBox			= true;
				DrawOnPricePanel			= true;
				DrawHorizontalGridLines		= true;
				DrawVerticalGridLines		= true;
				PaintPriceMarkers			= true;
				ScaleJustification			= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				IsSuspendedWhileInactive	= true;

				ConsensusThreshold	= 0.60;
				BaseIndex			= 3;
				DynamicBarColoring	= true;
				ShowClusterLabels	= true;
				ShowFlipDots		= true;
				BullColor			= Brushes.Lime;
				BearColor			= new SolidColorBrush(Color.FromRgb(0xF7, 0x52, 0x5F));
				NeutralColor		= new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));
				ShowCloud			= true;
				CloudReferenceLength= 8;
				CloudTransparency	= 65;
				EnsureFrozen(BullColor); EnsureFrozen(BearColor); EnsureFrozen(NeutralColor);

				// Pine member defaults.
				A1 = 7;  F1 = 1.5; M1 = SuperTrendCluster_MAType.EMA;  L1 = 3;  W1 = 1.0;
				A2 = 10; F2 = 2.0; M2 = SuperTrendCluster_MAType.EMA;  L2 = 5;  W2 = 1.0;
				A3 = 14; F3 = 2.5; M3 = SuperTrendCluster_MAType.SMA;  L3 = 8;  W3 = 1.2;
				A4 = 21; F4 = 3.0; M4 = SuperTrendCluster_MAType.WMA;  L4 = 13; W4 = 1.4;
				A5 = 34; F5 = 4.0; M5 = SuperTrendCluster_MAType.HMA;  L5 = 21; W5 = 1.6;

				AddPlot(new Stroke(Brushes.LimeGreen, DashStyleHelper.Solid, 2f), PlotStyle.Line, "Cluster Trend");
			}
			else if (State == State.DataLoaded)
			{
				sHlc3 = new Series<double>(this, MaximumBarsLookBack.Infinite);

				int[] aLens = new[] { A1, A2, A3, A4, A5 };
				int[] mLens = new[] { L1, L2, L3, L4, L5 };
				SuperTrendCluster_MAType[] mTypes = new[] { M1, M2, M3, M4, M5 };

				for (int i = 0; i < 5; i++)
				{
					memAtr[i] = ATR(aLens[i]);
					sMa[i]    = BuildMa(mTypes[i], sHlc3, mLens[i]);
					sUb[i]    = new Series<double>(this, MaximumBarsLookBack.Infinite);
					sLb[i]    = new Series<double>(this, MaximumBarsLookBack.Infinite);
					sDi[i]    = new Series<int>   (this, MaximumBarsLookBack.Infinite);
					sSt[i]    = new Series<double>(this, MaximumBarsLookBack.Infinite);
					sRma[i]   = new Series<double>(this, MaximumBarsLookBack.Infinite);
				}

				cloudRefSma = SMA(sHlc3, CloudReferenceLength);
				sDir   = new Series<int>   (this, MaximumBarsLookBack.Infinite);
				sBull  = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sBear  = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sDLast = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sClusterUp = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sClusterDn = new Series<double>(this, MaximumBarsLookBack.Infinite);
			}
			else if (State == State.Realtime)
			{
				if (ChartControl == null) return;
				OnRenderTargetChanged();
				if (Dispatcher.CheckAccess())
					ChartControl.InvalidateVisual();
				else
					ChartControl.Dispatcher.InvokeAsync(() => ChartControl.InvalidateVisual());
			}
			else if (State == State.Terminated)
			{
				ReleaseRenderResources();
			}
		}

		// Build MA wrapper that produces an ISeries<double> for the chosen type.
		// For RMA we return src as a placeholder — the actual RMA is computed inline in OnBarUpdate.
		private ISeries<double> BuildMa(SuperTrendCluster_MAType type, Series<double> src, int len)
		{
			switch (type)
			{
				case SuperTrendCluster_MAType.SMA:    return SMA(src, len);
				case SuperTrendCluster_MAType.EMA:    return EMA(src, len);
				case SuperTrendCluster_MAType.LinReg: return LinReg(src, len);
				case SuperTrendCluster_MAType.WMA:    return WMA(src, len);
				case SuperTrendCluster_MAType.HMA:    return HMA(src, len);
				default:                              return src;	// RMA handled inline; placeholder
			}
		}

		#endregion

		#region OnBarUpdate

		protected override void OnBarUpdate()
		{
			if (CurrentBar < 1) return;

			sHlc3[0] = (High[0] + Low[0] + Close[0]) / 3.0;

			SuperTrendCluster_MAType[] mTypes = new[] { M1, M2, M3, M4, M5 };
			int[] mLens   = new[] { L1, L2, L3, L4, L5 };
			double[] facs = new[] { F1, F2, F3, F4, F5 };
			double[] wts  = new[] { W1, W2, W3, W4, W5 };

			for (int i = 0; i < 5; i++)
			{
				double srcMa;
				if (mTypes[i] == SuperTrendCluster_MAType.RMA)
				{
					double alpha = 1.0 / Math.Max(1, mLens[i]);
					double prev  = CurrentBar > 0 && sRma[i].IsValidDataPointAt(CurrentBar - 1) ? sRma[i][1] : sHlc3[0];
					sRma[i][0] = (1 - alpha) * prev + alpha * sHlc3[0];
					srcMa = sRma[i][0];
				}
				else
				{
					srcMa = sMa[i][0];
				}

				double atrV = memAtr[i][0];
				double ub0  = srcMa + facs[i] * atrV;
				double lb0  = srcMa - facs[i] * atrV;

				double ubPrev = CurrentBar > 0 && sUb[i].IsValidDataPointAt(CurrentBar - 1) ? sUb[i][1] : ub0;
				double lbPrev = CurrentBar > 0 && sLb[i].IsValidDataPointAt(CurrentBar - 1) ? sLb[i][1] : lb0;
				double srcPrev = CurrentBar > 0 ? sHlc3[1] : sHlc3[0];

				double ub = (ub0 < ubPrev || srcPrev > ubPrev) ? ub0 : ubPrev;
				double lb = (lb0 > lbPrev || srcPrev < lbPrev) ? lb0 : lbPrev;
				int    dPrev = CurrentBar > 0 && sDi[i].IsValidDataPointAt(CurrentBar - 1) ? sDi[i][1] : 1;
				int    d = dPrev;
				if (dPrev == -1 && sHlc3[0] > ubPrev) d = 1;
				else if (dPrev == 1 && sHlc3[0] < lbPrev) d = -1;
				double st = d == 1 ? lb : ub;

				sUb[i][0] = ub;
				sLb[i][0] = lb;
				sDi[i][0] = d;
				sSt[i][0] = st;
			}

			// Consensus.
			double wSum = 0, wBu = 0, wBe = 0, lnBuNum = 0, lnBeNum = 0;
			for (int i = 0; i < 5; i++)
			{
				int d = sDi[i][0]; double w = wts[i]; double st = sSt[i][0];
				wSum += w;
				if (d > 0) { wBu += w; lnBuNum += st * w; }
				else if (d < 0) { wBe += w; lnBeNum += st * w; }
			}
			if (wSum < 1e-9) wSum = 1e-9;
			double scBu = wBu / wSum, scBe = wBe / wSum;
			double lnBu = wBu > 0 ? lnBuNum / wBu : double.NaN;
			double lnBe = wBe > 0 ? lnBeNum / wBe : double.NaN;

			sBull[0] = scBu;
			sBear[0] = scBe;

			int baseRow = Math.Max(0, Math.Min(4, BaseIndex - 1));
			int baseDir = sDi[baseRow][0];
			double baseSt = sSt[baseRow][0];

			bool flipBu = CurrentBar > 0 && baseDir > 0 && sDi[baseRow][1] <= 0;
			bool flipBe = CurrentBar > 0 && baseDir < 0 && sDi[baseRow][1] >= 0;

			// Final filtered regime.
			bool isBu = scBu >= ConsensusThreshold;
			bool isBe = scBe >= ConsensusThreshold;
			bool okBu = isBu && baseDir > 0;
			bool okBe = isBe && baseDir < 0;

			double dLast = CurrentBar > 0 ? sDLast[1] : 0;
			if (okBu && !okBe) dLast = 1;
			else if (okBe && !okBu) dLast = -1;
			sDLast[0] = dLast;
			sDir[0] = (int)dLast;

			double lnCl = dLast > 0 ? lnBu : (dLast < 0 ? lnBe : double.NaN);
			sClusterUp[0] = dLast > 0 ? lnCl : double.NaN;
			sClusterDn[0] = dLast < 0 ? lnCl : double.NaN;

			if (!double.IsNaN(lnCl)) Values[0][0] = lnCl;
			else Values[0].Reset();
			PlotBrushes[0][0] = dLast > 0 ? BullColor : (dLast < 0 ? BearColor : NeutralColor);

			// Bar coloring (gradient between Neutral and Bull/Bear).
			if (DynamicBarColoring)
			{
				double scCl = scBu - scBe;
				double strCl = Math.Min(1, Math.Abs(scCl));
				Brush c = scCl > 0 ? Lerp(NeutralColor, BullColor, strCl) : Lerp(NeutralColor, BearColor, strCl);
				BarBrushes[0]           = c;
				CandleOutlineBrushes[0] = c;
			}

			// Flip labels.
			bool dLastFlipBu = CurrentBar > 0 && dLast == 1  && sDLast[1] != 1;
			bool dLastFlipBe = CurrentBar > 0 && dLast == -1 && sDLast[1] != -1;

			if (ShowClusterLabels && flipBu)
				Draw.Text(this, "stcLblBu" + CurrentBar, false, "Bull Cluster\n" + (scBu * 100).ToString("0.#") + "%",
					0, baseSt, -20, BullColor, new SimpleFont("Arial", 11), TextAlignment.Center, WithAlpha(BullColor, 25), WithAlpha(BullColor, 25), 90);
			if (ShowClusterLabels && flipBe)
				Draw.Text(this, "stcLblBe" + CurrentBar, false, "Bear Cluster\n" + (scBe * 100).ToString("0.#") + "%",
					0, baseSt, 20, BearColor, new SimpleFont("Arial", 11), TextAlignment.Center, WithAlpha(BearColor, 25), WithAlpha(BearColor, 25), 90);

			if (ShowFlipDots && flipBu)
				Draw.Dot(this, "stcFlipBu" + CurrentBar, false, 0, baseSt, dLast > 0 ? WithAlpha(BullColor, 153) : WithAlpha(BearColor, 153));
			if (ShowFlipDots && flipBe)
				Draw.Dot(this, "stcFlipBe" + CurrentBar, false, 0, baseSt, dLast > 0 ? WithAlpha(BullColor, 153) : WithAlpha(BearColor, 153));

			if (dLastFlipBu)
				Draw.Text(this, "stcMajLong" + CurrentBar, false, "▲", 0, Low[0] - TickSize * 6, 0,
					Brushes.WhiteSmoke, new SimpleFont("Arial", 10), TextAlignment.Center, BullColor, BullColor, 70);
			if (dLastFlipBe)
				Draw.Text(this, "stcMajShort" + CurrentBar, false, "▼", 0, High[0] + TickSize * 6, 0,
					Brushes.WhiteSmoke, new SimpleFont("Arial", 10), TextAlignment.Center, BearColor, BearColor, 70);
		}

		#endregion

		#region SharpDX cloud fill

		public override void OnRenderTargetChanged()
		{
			try
			{
				ReleaseRenderResources();
				if (RenderTarget == null) return;

				float a = 1.0f - CloudTransparency / 100f;
				Color bullC = (BullColor as SolidColorBrush)?.Color ?? Colors.Lime;
				Color bearC = (BearColor as SolidColorBrush)?.Color ?? Colors.Red;

				dxCloudBull = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, ToColor4(bullC, a));
				dxCloudBear = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, ToColor4(bearC, a));
			}
			catch
			{
				// Suppress during teardown.
			}
		}

		private void ReleaseRenderResources()
		{
			void D(ref SharpDX.Direct2D1.SolidColorBrush bx) { if (bx != null) { bx.Dispose(); bx = null; } }
			D(ref dxCloudBull); D(ref dxCloudBear);
		}

		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			base.OnRender(chartControl, chartScale);
			if (!ShowCloud) return;
			if (RenderTarget == null) return;
			if (!IsVisible || IsInHitTest) return;
			if (ChartBars == null) return;
			if (sDLast == null) return;

			int fromIdx = Math.Max(ChartBars.FromIndex, 0);
			int toIdx   = Math.Min(ChartBars.ToIndex,   CurrentBar);

			for (int j = fromIdx; j < toIdx; j++)
			{
				if (!Values[0].IsValidDataPointAt(j) || !Values[0].IsValidDataPointAt(j + 1)) continue;
				if (!sHlc3.IsValidDataPointAt(j) || !sHlc3.IsValidDataPointAt(j + 1)) continue;
				if (!cloudRefSma.IsValidDataPointAt(j) || !cloudRefSma.IsValidDataPointAt(j + 1)) continue;

				double cl1 = Values[0].GetValueAt(j);
				double cl2 = Values[0].GetValueAt(j + 1);
				double rf1 = cloudRefSma.GetValueAt(j);
				double rf2 = cloudRefSma.GetValueAt(j + 1);
				int dirJ = sDLast.GetValueAt(j) > 0 ? 1 : (sDLast.GetValueAt(j) < 0 ? -1 : 0);
				if (dirJ == 0) continue;

				DrawTrapezoid(chartControl, chartScale, j, cl1, cl2, rf1, rf2, dirJ > 0 ? dxCloudBull : dxCloudBear);
			}
		}

		private void DrawTrapezoid(ChartControl cc, ChartScale cs, int barLeftIdx,
			double topL, double topR, double botL, double botR, SharpDX.Direct2D1.Brush brush)
		{
			float xL = cc.GetXByBarIndex(ChartBars, barLeftIdx);
			float xR = cc.GetXByBarIndex(ChartBars, barLeftIdx + 1);
			if (xR <= xL) return;
			float yTL = (float)cs.GetYByValue(topL);
			float yTR = (float)cs.GetYByValue(topR);
			float yBL = (float)cs.GetYByValue(botL);
			float yBR = (float)cs.GetYByValue(botR);

			SharpDX.Direct2D1.PathGeometry geom = null;
			SharpDX.Direct2D1.GeometrySink sink = null;
			try
			{
				geom = new SharpDX.Direct2D1.PathGeometry(Core.Globals.D2DFactory);
				sink = geom.Open();
				sink.SetFillMode(SharpDX.Direct2D1.FillMode.Winding);
				sink.BeginFigure(new SharpDX.Vector2(xL, yTL), SharpDX.Direct2D1.FigureBegin.Filled);
				sink.AddLine(new SharpDX.Vector2(xR, yTR));
				sink.AddLine(new SharpDX.Vector2(xR, yBR));
				sink.AddLine(new SharpDX.Vector2(xL, yBL));
				sink.EndFigure(SharpDX.Direct2D1.FigureEnd.Closed);
				sink.Close();
				RenderTarget.FillGeometry(geom, brush);
			}
			finally
			{
				if (sink != null) sink.Dispose();
				if (geom != null) geom.Dispose();
			}
		}

		#endregion

		#region Helpers

		private static Brush Lerp(Brush a, Brush b, double t)
		{
			Color ca = (a as SolidColorBrush)?.Color ?? Colors.Gray;
			Color cb = (b as SolidColorBrush)?.Color ?? Colors.Gray;
			byte rr = (byte)(ca.R + (cb.R - ca.R) * t);
			byte gg = (byte)(ca.G + (cb.G - ca.G) * t);
			byte bb = (byte)(ca.B + (cb.B - ca.B) * t);
			var br = new SolidColorBrush(Color.FromRgb(rr, gg, bb));
			br.Freeze();
			return br;
		}

		private static SharpDX.Color4 ToColor4(Color c, float alpha)
		{
			return new SharpDX.Color4(c.R / 255f, c.G / 255f, c.B / 255f, alpha);
		}

		private static Brush WithAlpha(Brush src, byte alpha)
		{
			var scb = src as SolidColorBrush;
			Color c = scb != null ? scb.Color : Colors.Gray;
			var b = new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
			b.Freeze();
			return b;
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
		private indTradingView.SuperTrendCluster[] cacheSuperTrendCluster;
		public indTradingView.SuperTrendCluster SuperTrendCluster(double consensusThreshold, int baseIndex, bool dynamicBarColoring, bool showClusterLabels, bool showFlipDots, bool showCloud, int cloudReferenceLength, int cloudTransparency, int a1, double f1, SuperTrendCluster_MAType m1, int l1, double w1, int a2, double f2, SuperTrendCluster_MAType m2, int l2, double w2, int a3, double f3, SuperTrendCluster_MAType m3, int l3, double w3, int a4, double f4, SuperTrendCluster_MAType m4, int l4, double w4, int a5, double f5, SuperTrendCluster_MAType m5, int l5, double w5)
		{
			return SuperTrendCluster(Input, consensusThreshold, baseIndex, dynamicBarColoring, showClusterLabels, showFlipDots, showCloud, cloudReferenceLength, cloudTransparency, a1, f1, m1, l1, w1, a2, f2, m2, l2, w2, a3, f3, m3, l3, w3, a4, f4, m4, l4, w4, a5, f5, m5, l5, w5);
		}

		public indTradingView.SuperTrendCluster SuperTrendCluster(ISeries<double> input, double consensusThreshold, int baseIndex, bool dynamicBarColoring, bool showClusterLabels, bool showFlipDots, bool showCloud, int cloudReferenceLength, int cloudTransparency, int a1, double f1, SuperTrendCluster_MAType m1, int l1, double w1, int a2, double f2, SuperTrendCluster_MAType m2, int l2, double w2, int a3, double f3, SuperTrendCluster_MAType m3, int l3, double w3, int a4, double f4, SuperTrendCluster_MAType m4, int l4, double w4, int a5, double f5, SuperTrendCluster_MAType m5, int l5, double w5)
		{
			if (cacheSuperTrendCluster != null)
				for (int idx = 0; idx < cacheSuperTrendCluster.Length; idx++)
					if (cacheSuperTrendCluster[idx] != null && cacheSuperTrendCluster[idx].ConsensusThreshold == consensusThreshold && cacheSuperTrendCluster[idx].BaseIndex == baseIndex && cacheSuperTrendCluster[idx].DynamicBarColoring == dynamicBarColoring && cacheSuperTrendCluster[idx].ShowClusterLabels == showClusterLabels && cacheSuperTrendCluster[idx].ShowFlipDots == showFlipDots && cacheSuperTrendCluster[idx].ShowCloud == showCloud && cacheSuperTrendCluster[idx].CloudReferenceLength == cloudReferenceLength && cacheSuperTrendCluster[idx].CloudTransparency == cloudTransparency && cacheSuperTrendCluster[idx].A1 == a1 && cacheSuperTrendCluster[idx].F1 == f1 && cacheSuperTrendCluster[idx].M1 == m1 && cacheSuperTrendCluster[idx].L1 == l1 && cacheSuperTrendCluster[idx].W1 == w1 && cacheSuperTrendCluster[idx].A2 == a2 && cacheSuperTrendCluster[idx].F2 == f2 && cacheSuperTrendCluster[idx].M2 == m2 && cacheSuperTrendCluster[idx].L2 == l2 && cacheSuperTrendCluster[idx].W2 == w2 && cacheSuperTrendCluster[idx].A3 == a3 && cacheSuperTrendCluster[idx].F3 == f3 && cacheSuperTrendCluster[idx].M3 == m3 && cacheSuperTrendCluster[idx].L3 == l3 && cacheSuperTrendCluster[idx].W3 == w3 && cacheSuperTrendCluster[idx].A4 == a4 && cacheSuperTrendCluster[idx].F4 == f4 && cacheSuperTrendCluster[idx].M4 == m4 && cacheSuperTrendCluster[idx].L4 == l4 && cacheSuperTrendCluster[idx].W4 == w4 && cacheSuperTrendCluster[idx].A5 == a5 && cacheSuperTrendCluster[idx].F5 == f5 && cacheSuperTrendCluster[idx].M5 == m5 && cacheSuperTrendCluster[idx].L5 == l5 && cacheSuperTrendCluster[idx].W5 == w5 && cacheSuperTrendCluster[idx].EqualsInput(input))
						return cacheSuperTrendCluster[idx];
			return CacheIndicator<indTradingView.SuperTrendCluster>(new indTradingView.SuperTrendCluster(){ ConsensusThreshold = consensusThreshold, BaseIndex = baseIndex, DynamicBarColoring = dynamicBarColoring, ShowClusterLabels = showClusterLabels, ShowFlipDots = showFlipDots, ShowCloud = showCloud, CloudReferenceLength = cloudReferenceLength, CloudTransparency = cloudTransparency, A1 = a1, F1 = f1, M1 = m1, L1 = l1, W1 = w1, A2 = a2, F2 = f2, M2 = m2, L2 = l2, W2 = w2, A3 = a3, F3 = f3, M3 = m3, L3 = l3, W3 = w3, A4 = a4, F4 = f4, M4 = m4, L4 = l4, W4 = w4, A5 = a5, F5 = f5, M5 = m5, L5 = l5, W5 = w5 }, input, ref cacheSuperTrendCluster);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.SuperTrendCluster SuperTrendCluster(double consensusThreshold, int baseIndex, bool dynamicBarColoring, bool showClusterLabels, bool showFlipDots, bool showCloud, int cloudReferenceLength, int cloudTransparency, int a1, double f1, SuperTrendCluster_MAType m1, int l1, double w1, int a2, double f2, SuperTrendCluster_MAType m2, int l2, double w2, int a3, double f3, SuperTrendCluster_MAType m3, int l3, double w3, int a4, double f4, SuperTrendCluster_MAType m4, int l4, double w4, int a5, double f5, SuperTrendCluster_MAType m5, int l5, double w5)
		{
			return indicator.SuperTrendCluster(Input, consensusThreshold, baseIndex, dynamicBarColoring, showClusterLabels, showFlipDots, showCloud, cloudReferenceLength, cloudTransparency, a1, f1, m1, l1, w1, a2, f2, m2, l2, w2, a3, f3, m3, l3, w3, a4, f4, m4, l4, w4, a5, f5, m5, l5, w5);
		}

		public Indicators.indTradingView.SuperTrendCluster SuperTrendCluster(ISeries<double> input , double consensusThreshold, int baseIndex, bool dynamicBarColoring, bool showClusterLabels, bool showFlipDots, bool showCloud, int cloudReferenceLength, int cloudTransparency, int a1, double f1, SuperTrendCluster_MAType m1, int l1, double w1, int a2, double f2, SuperTrendCluster_MAType m2, int l2, double w2, int a3, double f3, SuperTrendCluster_MAType m3, int l3, double w3, int a4, double f4, SuperTrendCluster_MAType m4, int l4, double w4, int a5, double f5, SuperTrendCluster_MAType m5, int l5, double w5)
		{
			return indicator.SuperTrendCluster(input, consensusThreshold, baseIndex, dynamicBarColoring, showClusterLabels, showFlipDots, showCloud, cloudReferenceLength, cloudTransparency, a1, f1, m1, l1, w1, a2, f2, m2, l2, w2, a3, f3, m3, l3, w3, a4, f4, m4, l4, w4, a5, f5, m5, l5, w5);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.SuperTrendCluster SuperTrendCluster(double consensusThreshold, int baseIndex, bool dynamicBarColoring, bool showClusterLabels, bool showFlipDots, bool showCloud, int cloudReferenceLength, int cloudTransparency, int a1, double f1, SuperTrendCluster_MAType m1, int l1, double w1, int a2, double f2, SuperTrendCluster_MAType m2, int l2, double w2, int a3, double f3, SuperTrendCluster_MAType m3, int l3, double w3, int a4, double f4, SuperTrendCluster_MAType m4, int l4, double w4, int a5, double f5, SuperTrendCluster_MAType m5, int l5, double w5)
		{
			return indicator.SuperTrendCluster(Input, consensusThreshold, baseIndex, dynamicBarColoring, showClusterLabels, showFlipDots, showCloud, cloudReferenceLength, cloudTransparency, a1, f1, m1, l1, w1, a2, f2, m2, l2, w2, a3, f3, m3, l3, w3, a4, f4, m4, l4, w4, a5, f5, m5, l5, w5);
		}

		public Indicators.indTradingView.SuperTrendCluster SuperTrendCluster(ISeries<double> input , double consensusThreshold, int baseIndex, bool dynamicBarColoring, bool showClusterLabels, bool showFlipDots, bool showCloud, int cloudReferenceLength, int cloudTransparency, int a1, double f1, SuperTrendCluster_MAType m1, int l1, double w1, int a2, double f2, SuperTrendCluster_MAType m2, int l2, double w2, int a3, double f3, SuperTrendCluster_MAType m3, int l3, double w3, int a4, double f4, SuperTrendCluster_MAType m4, int l4, double w4, int a5, double f5, SuperTrendCluster_MAType m5, int l5, double w5)
		{
			return indicator.SuperTrendCluster(input, consensusThreshold, baseIndex, dynamicBarColoring, showClusterLabels, showFlipDots, showCloud, cloudReferenceLength, cloudTransparency, a1, f1, m1, l1, w1, a2, f2, m2, l2, w2, a3, f3, m3, l3, w3, a4, f4, m4, l4, w4, a5, f5, m5, l5, w5);
		}
	}
}

#endregion
