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

// NT8 Version of CM Ultimate MA MTF V4 [ChrisMoody]
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the Mozilla Public License 2.0 (MPL 2.0).
// The original Pine Script™ code is by ChrisMoody and can be found at: https://www.tradingview.com/script/bkvQ5hNF-CM-Ultimate-MA-MTF-V4/
// Adaptation for NinjaTrader by jack@mydailytake.com
// Write-up + downloads: https://mydailytake.com/cm-ultimate-ma-mtf-chrismoody-ninjatrader-8/
// Source + all conversions: https://github.com/MyDailyTake/tradingview-to-ninjatrader-8
// © 2026 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the Mozilla Public License 2.0. Full license details at https://mozilla.org/MPL/2.0/
// The use of ChrisMoody name or its adapted code in this work does not imply endorsement by the original authors.
//
// Notes:
//   Dual-MA overlay with 8 selectable MA types per leg (SMA, EMA, WMA, Hull MA, VWMA, RMA, TEMA, Tilson T3),
//   independent sources, dynamic color flow, and translucent fill between the two MAs.
//   Color modes (mutually exclusive — fast-vs-slow takes precedence when both ON):
//     • Slope based — bullish when Fast >= Fast[smooth], bearish otherwise.
//     • Fast vs Slow — bullish when Fast >= Slow, bearish otherwise.
//   Fill rendered via SharpDX OnRender per-bar trapezoid (configurable transparency).
//
//   Documented scope drops:
//     • Pine Multi-TimeFrame `request.security(...)` for Fast / Slow MA — NT8 MTF requires
//       AddDataSeries plumbing across all 8 MA types and is left for a future revision. Apply
//       this indicator twice (different chart timeframes) to approximate.
//     • Pine `offset` parameter — NT8 has no idiomatic plot-offset; dropped without replacement.
//     • Pine `alertcondition()` — stripped per house QC.

#region Enums CmUltimateMaMtf

public enum CmUltimateMaMtf_MAType
{
	SMA,
	EMA,
	WMA,
	HullMA,
	VWMA,
	RMA,
	TEMA,
	TilsonT3
}

public enum CmUltimateMaMtf_Source
{
	Close,
	Open,
	High,
	Low,
	HL2,
	HLC3,
	OHLC4
}

#endregion

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	#region Categories
	[Gui.CategoryOrder("Fast MA",		10100)]
	[Gui.CategoryOrder("Slow MA",		10200)]
	[Gui.CategoryOrder("Plot Settings",	10300)]
	#endregion

	public class CmUltimateMaMtf : Indicator
	{
		#region indInfo

		private string indName        = "CM Ultimate MA MTF V4 [ChrisMoody]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by ChrisMoody can be found here: https://www.tradingview.com/script/bkvQ5hNF-CM-Ultimate-MA-MTF-V4/";

		#endregion

		#region Properties — Fast MA

		[NinjaScriptProperty]
		[Range(1, 999)]
		[Display(Order = 1, GroupName = "Fast MA", Name = "Fast MA Length", Description = "Lookback for the Fast MA.")]
		public int FastMaLength { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 2, GroupName = "Fast MA", Name = "Fast MA Source", Description = "Price source feeding the Fast MA.")]
		public CmUltimateMaMtf_Source FastMaSource { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 3, GroupName = "Fast MA", Name = "Fast MA Type", Description = "Algorithm used to compute the Fast MA.")]
		public CmUltimateMaMtf_MAType FastMaType { get; set; }

		[NinjaScriptProperty]
		[Range(0.0, 99.0)]
		[Display(Order = 4, GroupName = "Fast MA", Name = "Fast Tilson T3 Factor", Description = "Smoothing factor for Tilson T3. Pine default 0.7.")]
		public double FastT3Factor { get; set; }

		[XmlIgnore]
		[Display(Order = 5, GroupName = "Fast MA", Name = "Fast MA Color", Description = "Default Fast MA stroke color.")]
		public Brush FastMaColor { get; set; }
			[Browsable(false)]
			public string FastMaColorSerialize
			{
				get { return Serialize.BrushToString(FastMaColor); }
				set { FastMaColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[NinjaScriptProperty]
		[Range(1, 5)]
		[Display(Order = 6, GroupName = "Fast MA", Name = "Fast MA Width", Description = "Plot stroke width for the Fast MA.")]
		public int FastMaWidth { get; set; }

		#endregion

		#region Properties — Slow MA

		[NinjaScriptProperty]
		[Display(Order = 1, GroupName = "Slow MA", Name = "Show Slow MA", Description = "Render the Slow MA line.")]
		public bool ShowSlowMa { get; set; }

		[NinjaScriptProperty]
		[Range(1, 999)]
		[Display(Order = 2, GroupName = "Slow MA", Name = "Slow MA Length", Description = "Lookback for the Slow MA.")]
		public int SlowMaLength { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 3, GroupName = "Slow MA", Name = "Slow MA Source", Description = "Price source feeding the Slow MA.")]
		public CmUltimateMaMtf_Source SlowMaSource { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 4, GroupName = "Slow MA", Name = "Slow MA Type", Description = "Algorithm used to compute the Slow MA.")]
		public CmUltimateMaMtf_MAType SlowMaType { get; set; }

		[NinjaScriptProperty]
		[Range(0.0, 99.0)]
		[Display(Order = 5, GroupName = "Slow MA", Name = "Slow Tilson T3 Factor", Description = "Smoothing factor for Tilson T3. Pine default 0.7.")]
		public double SlowT3Factor { get; set; }

		[XmlIgnore]
		[Display(Order = 6, GroupName = "Slow MA", Name = "Slow MA Color", Description = "Default Slow MA stroke color.")]
		public Brush SlowMaColor { get; set; }
			[Browsable(false)]
			public string SlowMaColorSerialize
			{
				get { return Serialize.BrushToString(SlowMaColor); }
				set { SlowMaColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[NinjaScriptProperty]
		[Range(1, 5)]
		[Display(Order = 7, GroupName = "Slow MA", Name = "Slow MA Width", Description = "Plot stroke width for the Slow MA.")]
		public int SlowMaWidth { get; set; }

		#endregion

		#region Properties — Plot Settings

		[NinjaScriptProperty]
		[Display(Order = 1, GroupName = "Plot Settings", Name = "Color Fast by Slope",
			Description = "Color the Fast MA bullish / bearish based on its own slope (Fast >= Fast[smooth]).")]
		public bool ShowFastSlopeColor { get; set; }

		[NinjaScriptProperty]
		[Range(1, 20)]
		[Display(Order = 2, GroupName = "Plot Settings", Name = "Slope Smoothing",
			Description = "Bars-back used in the slope-color comparison. 1 = no smoothing.")]
		public int FastSlopeSmooth { get; set; }

		[XmlIgnore]
		[Display(Order = 3, GroupName = "Plot Settings", Name = "Bullish Color",
			Description = "Color used when slope-up or Fast above Slow.")]
		public Brush BullishColor { get; set; }
			[Browsable(false)]
			public string BullishColorSerialize
			{
				get { return Serialize.BrushToString(BullishColor); }
				set { BullishColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore]
		[Display(Order = 4, GroupName = "Plot Settings", Name = "Bearish Color",
			Description = "Color used when slope-down or Fast below Slow.")]
		public Brush BearishColor { get; set; }
			[Browsable(false)]
			public string BearishColorSerialize
			{
				get { return Serialize.BrushToString(BearishColor); }
				set { BearishColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[NinjaScriptProperty]
		[Display(Order = 5, GroupName = "Plot Settings", Name = "Color Fast vs Slow",
			Description = "Color the Fast MA based on whether it sits above or below the Slow MA. Takes precedence over slope coloring.")]
		public bool ShowFastVsSlowColor { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 6, GroupName = "Plot Settings", Name = "Show Fill",
			Description = "Render translucent fill between Fast and Slow MAs.")]
		public bool ShowFill { get; set; }

		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Order = 7, GroupName = "Plot Settings", Name = "Fill Transparency (0-100)",
			Description = "0 = solid, 100 = invisible. Pine semantics.")]
		public int FillTransparency { get; set; }

		#endregion

		#region Public outputs

		[Browsable(false)][XmlIgnore] public Series<double> FastMa        { get { return Values[0]; } }
		[Browsable(false)][XmlIgnore] public Series<double> SlowMa        { get { return Values[1]; } }
		[Browsable(false)][XmlIgnore] public Series<bool>   IsFastAboveSlow { get { Update(); return sFastAbove; } }

		#endregion

		#region Variables

		// Source series for each leg (recomputed per bar from the chosen Source enum).
		private Series<double> sFastSrc, sSlowSrc;

		// Built-in MA primitives — instantiated for whichever types might be selected.
		private SMA   smaFast,  smaSlow;
		private EMA   emaFast,  emaSlow;
		private WMA   wmaFast,  wmaSlow;
		private HMA   hmaFast,  hmaSlow;
		private VWMA  vwmaFast, vwmaSlow;
		private TEMA  temaFast, temaSlow;

		// Custom RMA + T3 state series.
		private Series<double> sRmaFast,  sRmaSlow;
		private Series<double> sFastE1, sFastE2, sFastE3, sFastE4, sFastE5, sFastE6;	// for T3 chain (six EMAs of source / chained)
		private Series<double> sSlowE1, sSlowE2, sSlowE3, sSlowE4, sSlowE5, sSlowE6;

		private Series<bool>   sFastAbove;

		// SharpDX brushes for the fill.
		private SharpDX.Direct2D1.SolidColorBrush dxFillBull, dxFillBear, dxFillNeutral;

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

				FastMaLength		= 20;
				FastMaSource		= CmUltimateMaMtf_Source.Close;
				FastMaType			= CmUltimateMaMtf_MAType.SMA;
				FastT3Factor		= 0.7;
				FastMaColor			= new SolidColorBrush(Color.FromRgb(0x29, 0x62, 0xFF));
				FastMaWidth			= 3;

				ShowSlowMa			= false;
				SlowMaLength		= 50;
				SlowMaSource		= CmUltimateMaMtf_Source.Close;
				SlowMaType			= CmUltimateMaMtf_MAType.SMA;
				SlowT3Factor		= 0.7;
				SlowMaColor			= new SolidColorBrush(Color.FromRgb(0xFF, 0x6D, 0x00));
				SlowMaWidth			= 3;

				ShowFastSlopeColor	= false;
				FastSlopeSmooth		= 2;
				BullishColor		= Brushes.Green;
				BearishColor		= Brushes.Red;
				ShowFastVsSlowColor	= false;
				ShowFill			= true;
				FillTransparency	= 50;
				EnsureFrozen(FastMaColor);
				EnsureFrozen(SlowMaColor);
				EnsureFrozen(BullishColor);
				EnsureFrozen(BearishColor);

				AddPlot(new Stroke(Brushes.DodgerBlue, DashStyleHelper.Solid, 3f), PlotStyle.Line, "Fast MA");
				AddPlot(new Stroke(Brushes.Orange,     DashStyleHelper.Solid, 3f), PlotStyle.Line, "Slow MA");
			}
			else if (State == State.DataLoaded)
			{
				sFastSrc = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sSlowSrc = new Series<double>(this, MaximumBarsLookBack.Infinite);

				smaFast  = SMA(sFastSrc, FastMaLength);
				emaFast  = EMA(sFastSrc, FastMaLength);
				wmaFast  = WMA(sFastSrc, FastMaLength);
				hmaFast  = HMA(sFastSrc, FastMaLength);
				vwmaFast = VWMA(sFastSrc, FastMaLength);
				temaFast = TEMA(sFastSrc, FastMaLength);

				smaSlow  = SMA(sSlowSrc, SlowMaLength);
				emaSlow  = EMA(sSlowSrc, SlowMaLength);
				wmaSlow  = WMA(sSlowSrc, SlowMaLength);
				hmaSlow  = HMA(sSlowSrc, SlowMaLength);
				vwmaSlow = VWMA(sSlowSrc, SlowMaLength);
				temaSlow = TEMA(sSlowSrc, SlowMaLength);

				sRmaFast = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sRmaSlow = new Series<double>(this, MaximumBarsLookBack.Infinite);

				sFastE1 = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sFastE2 = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sFastE3 = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sFastE4 = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sFastE5 = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sFastE6 = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sSlowE1 = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sSlowE2 = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sSlowE3 = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sSlowE4 = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sSlowE5 = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sSlowE6 = new Series<double>(this, MaximumBarsLookBack.Infinite);

				sFastAbove = new Series<bool>(this, MaximumBarsLookBack.Infinite);
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

		#endregion

		#region OnBarUpdate

		protected override void OnBarUpdate()
		{
			if (CurrentBar < 1) return;

			sFastSrc[0] = SourceValue(FastMaSource);
			sSlowSrc[0] = SourceValue(SlowMaSource);

			double fast = ComputeMa(FastMaType, FastMaLength, FastT3Factor, isFast: true);
			double slow = ComputeMa(SlowMaType, SlowMaLength, SlowT3Factor, isFast: false);

			Values[0][0] = fast;
			if (ShowSlowMa) Values[1][0] = slow; else Values[1].Reset();

			sFastAbove[0] = fast >= slow;

			// Color flow on Fast.
			Brush fastBrush = FastMaColor;
			if (ShowFastVsSlowColor && ShowSlowMa)
			{
				fastBrush = fast >= slow ? BullishColor : BearishColor;
			}
			else if (ShowFastSlopeColor && CurrentBar >= FastSlopeSmooth)
			{
				bool up = fast >= Values[0][FastSlopeSmooth];
				fastBrush = up ? BullishColor : BearishColor;
			}
			PlotBrushes[0][0] = fastBrush;
			PlotBrushes[1][0] = SlowMaColor;
			Plots[0].Width = FastMaWidth;
			Plots[1].Width = SlowMaWidth;
		}

		#endregion

		#region MA computation

		private double SourceValue(CmUltimateMaMtf_Source s)
		{
			switch (s)
			{
				case CmUltimateMaMtf_Source.Open:  return Open[0];
				case CmUltimateMaMtf_Source.High:  return High[0];
				case CmUltimateMaMtf_Source.Low:   return Low[0];
				case CmUltimateMaMtf_Source.HL2:   return (High[0] + Low[0]) / 2.0;
				case CmUltimateMaMtf_Source.HLC3:  return (High[0] + Low[0] + Close[0]) / 3.0;
				case CmUltimateMaMtf_Source.OHLC4: return (Open[0] + High[0] + Low[0] + Close[0]) / 4.0;
				default: return Close[0];
			}
		}

		private double ComputeMa(CmUltimateMaMtf_MAType t, int len, double factor, bool isFast)
		{
			Series<double> src = isFast ? sFastSrc : sSlowSrc;
			switch (t)
			{
				case CmUltimateMaMtf_MAType.SMA:    return (isFast ? smaFast  : smaSlow )[0];
				case CmUltimateMaMtf_MAType.EMA:    return (isFast ? emaFast  : emaSlow )[0];
				case CmUltimateMaMtf_MAType.WMA:    return (isFast ? wmaFast  : wmaSlow )[0];
				case CmUltimateMaMtf_MAType.HullMA: return (isFast ? hmaFast  : hmaSlow )[0];
				case CmUltimateMaMtf_MAType.VWMA:   return (isFast ? vwmaFast : vwmaSlow)[0];
				case CmUltimateMaMtf_MAType.TEMA:   return (isFast ? temaFast : temaSlow)[0];
				case CmUltimateMaMtf_MAType.RMA:    return ComputeRma(src, isFast ? sRmaFast : sRmaSlow, len);
				case CmUltimateMaMtf_MAType.TilsonT3:
					return ComputeT3(src, factor, len, isFast);
			}
			return Close[0];
		}

		// Wilder's RMA = recursive avg with α = 1/n. First-bar seeds with src.
		private double ComputeRma(Series<double> src, Series<double> rma, int len)
		{
			double alpha = 1.0 / Math.Max(1, len);
			double prev  = CurrentBar > 0 && rma.IsValidDataPointAt(CurrentBar - 1) ? rma[1] : src[0];
			double cur   = (1.0 - alpha) * prev + alpha * src[0];
			rma[0] = cur;
			return cur;
		}

		// Tilson T3 — gd applied 3 times to source. Six EMAs total (e1, e2 / e3, e4 / e5, e6).
		private double ComputeT3(Series<double> src, double factor, int len, bool isFast)
		{
			Series<double> e1 = isFast ? sFastE1 : sSlowE1;
			Series<double> e2 = isFast ? sFastE2 : sSlowE2;
			Series<double> e3 = isFast ? sFastE3 : sSlowE3;
			Series<double> e4 = isFast ? sFastE4 : sSlowE4;
			Series<double> e5 = isFast ? sFastE5 : sSlowE5;
			Series<double> e6 = isFast ? sFastE6 : sSlowE6;

			double a = 2.0 / (Math.Max(1, len) + 1);

			e1[0] = CurrentBar == 0 ? src[0] : a * src[0]    + (1 - a) * e1[1];
			e2[0] = CurrentBar == 0 ? e1[0] : a * e1[0]      + (1 - a) * e2[1];
			double gd1 = e1[0] * (1 + factor) - e2[0] * factor;

			e3[0] = CurrentBar == 0 ? gd1 : a * gd1          + (1 - a) * e3[1];
			e4[0] = CurrentBar == 0 ? e3[0] : a * e3[0]      + (1 - a) * e4[1];
			double gd2 = e3[0] * (1 + factor) - e4[0] * factor;

			e5[0] = CurrentBar == 0 ? gd2 : a * gd2          + (1 - a) * e5[1];
			e6[0] = CurrentBar == 0 ? e5[0] : a * e5[0]      + (1 - a) * e6[1];
			double gd3 = e5[0] * (1 + factor) - e6[0] * factor;

			return gd3;
		}

		#endregion

		#region SharpDX fill

		public override void OnRenderTargetChanged()
		{
			try
			{
				ReleaseRenderResources();
				if (RenderTarget == null) return;

				Color bullC = (BullishColor as SolidColorBrush)?.Color ?? Colors.Green;
				Color bearC = (BearishColor as SolidColorBrush)?.Color ?? Colors.Red;
				Color neutC = (FastMaColor  as SolidColorBrush)?.Color ?? Colors.DodgerBlue;

				float a = 1.0f - FillTransparency / 100f;
				dxFillBull    = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, ToColor4(bullC, a));
				dxFillBear    = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, ToColor4(bearC, a));
				dxFillNeutral = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, ToColor4(neutC, a));
			}
			catch
			{
				// Suppress during teardown.
			}
		}

		private void ReleaseRenderResources()
		{
			void D(ref SharpDX.Direct2D1.SolidColorBrush bx) { if (bx != null) { bx.Dispose(); bx = null; } }
			D(ref dxFillBull); D(ref dxFillBear); D(ref dxFillNeutral);
		}

		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			base.OnRender(chartControl, chartScale);
			if (!ShowFill || !ShowSlowMa) return;
			if (RenderTarget == null) return;
			if (!IsVisible || IsInHitTest) return;
			if (ChartBars == null) return;

			int fromIdx = Math.Max(ChartBars.FromIndex, 0);
			int toIdx   = Math.Min(ChartBars.ToIndex,   CurrentBar);

			for (int j = fromIdx; j < toIdx; j++)
			{
				if (!Values[0].IsValidDataPointAt(j) || !Values[0].IsValidDataPointAt(j + 1)) continue;
				if (!Values[1].IsValidDataPointAt(j) || !Values[1].IsValidDataPointAt(j + 1)) continue;

				double f1 = Values[0].GetValueAt(j),     f2 = Values[0].GetValueAt(j + 1);
				double s1 = Values[1].GetValueAt(j),     s2 = Values[1].GetValueAt(j + 1);

				SharpDX.Direct2D1.Brush brush = dxFillNeutral;
				if (ShowFastVsSlowColor) brush = f1 >= s1 ? dxFillBull : dxFillBear;
				else if (ShowFastSlopeColor && j >= FastSlopeSmooth)
				{
					double fPrev = Values[0].GetValueAt(j - FastSlopeSmooth);
					brush = f1 >= fPrev ? dxFillBull : dxFillBear;
				}
				if (brush == null) continue;

				DrawTrapezoid(chartControl, chartScale, j, f1, f2, s1, s2, brush);
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

		private static SharpDX.Color4 ToColor4(Color c, float alpha)
		{
			return new SharpDX.Color4(c.R / 255f, c.G / 255f, c.B / 255f, alpha);
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
		private indTradingView.CmUltimateMaMtf[] cacheCmUltimateMaMtf;
		public indTradingView.CmUltimateMaMtf CmUltimateMaMtf(int fastMaLength, CmUltimateMaMtf_Source fastMaSource, CmUltimateMaMtf_MAType fastMaType, double fastT3Factor, int fastMaWidth, bool showSlowMa, int slowMaLength, CmUltimateMaMtf_Source slowMaSource, CmUltimateMaMtf_MAType slowMaType, double slowT3Factor, int slowMaWidth, bool showFastSlopeColor, int fastSlopeSmooth, bool showFastVsSlowColor, bool showFill, int fillTransparency)
		{
			return CmUltimateMaMtf(Input, fastMaLength, fastMaSource, fastMaType, fastT3Factor, fastMaWidth, showSlowMa, slowMaLength, slowMaSource, slowMaType, slowT3Factor, slowMaWidth, showFastSlopeColor, fastSlopeSmooth, showFastVsSlowColor, showFill, fillTransparency);
		}

		public indTradingView.CmUltimateMaMtf CmUltimateMaMtf(ISeries<double> input, int fastMaLength, CmUltimateMaMtf_Source fastMaSource, CmUltimateMaMtf_MAType fastMaType, double fastT3Factor, int fastMaWidth, bool showSlowMa, int slowMaLength, CmUltimateMaMtf_Source slowMaSource, CmUltimateMaMtf_MAType slowMaType, double slowT3Factor, int slowMaWidth, bool showFastSlopeColor, int fastSlopeSmooth, bool showFastVsSlowColor, bool showFill, int fillTransparency)
		{
			if (cacheCmUltimateMaMtf != null)
				for (int idx = 0; idx < cacheCmUltimateMaMtf.Length; idx++)
					if (cacheCmUltimateMaMtf[idx] != null && cacheCmUltimateMaMtf[idx].FastMaLength == fastMaLength && cacheCmUltimateMaMtf[idx].FastMaSource == fastMaSource && cacheCmUltimateMaMtf[idx].FastMaType == fastMaType && cacheCmUltimateMaMtf[idx].FastT3Factor == fastT3Factor && cacheCmUltimateMaMtf[idx].FastMaWidth == fastMaWidth && cacheCmUltimateMaMtf[idx].ShowSlowMa == showSlowMa && cacheCmUltimateMaMtf[idx].SlowMaLength == slowMaLength && cacheCmUltimateMaMtf[idx].SlowMaSource == slowMaSource && cacheCmUltimateMaMtf[idx].SlowMaType == slowMaType && cacheCmUltimateMaMtf[idx].SlowT3Factor == slowT3Factor && cacheCmUltimateMaMtf[idx].SlowMaWidth == slowMaWidth && cacheCmUltimateMaMtf[idx].ShowFastSlopeColor == showFastSlopeColor && cacheCmUltimateMaMtf[idx].FastSlopeSmooth == fastSlopeSmooth && cacheCmUltimateMaMtf[idx].ShowFastVsSlowColor == showFastVsSlowColor && cacheCmUltimateMaMtf[idx].ShowFill == showFill && cacheCmUltimateMaMtf[idx].FillTransparency == fillTransparency && cacheCmUltimateMaMtf[idx].EqualsInput(input))
						return cacheCmUltimateMaMtf[idx];
			return CacheIndicator<indTradingView.CmUltimateMaMtf>(new indTradingView.CmUltimateMaMtf(){ FastMaLength = fastMaLength, FastMaSource = fastMaSource, FastMaType = fastMaType, FastT3Factor = fastT3Factor, FastMaWidth = fastMaWidth, ShowSlowMa = showSlowMa, SlowMaLength = slowMaLength, SlowMaSource = slowMaSource, SlowMaType = slowMaType, SlowT3Factor = slowT3Factor, SlowMaWidth = slowMaWidth, ShowFastSlopeColor = showFastSlopeColor, FastSlopeSmooth = fastSlopeSmooth, ShowFastVsSlowColor = showFastVsSlowColor, ShowFill = showFill, FillTransparency = fillTransparency }, input, ref cacheCmUltimateMaMtf);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.CmUltimateMaMtf CmUltimateMaMtf(int fastMaLength, CmUltimateMaMtf_Source fastMaSource, CmUltimateMaMtf_MAType fastMaType, double fastT3Factor, int fastMaWidth, bool showSlowMa, int slowMaLength, CmUltimateMaMtf_Source slowMaSource, CmUltimateMaMtf_MAType slowMaType, double slowT3Factor, int slowMaWidth, bool showFastSlopeColor, int fastSlopeSmooth, bool showFastVsSlowColor, bool showFill, int fillTransparency)
		{
			return indicator.CmUltimateMaMtf(Input, fastMaLength, fastMaSource, fastMaType, fastT3Factor, fastMaWidth, showSlowMa, slowMaLength, slowMaSource, slowMaType, slowT3Factor, slowMaWidth, showFastSlopeColor, fastSlopeSmooth, showFastVsSlowColor, showFill, fillTransparency);
		}

		public Indicators.indTradingView.CmUltimateMaMtf CmUltimateMaMtf(ISeries<double> input , int fastMaLength, CmUltimateMaMtf_Source fastMaSource, CmUltimateMaMtf_MAType fastMaType, double fastT3Factor, int fastMaWidth, bool showSlowMa, int slowMaLength, CmUltimateMaMtf_Source slowMaSource, CmUltimateMaMtf_MAType slowMaType, double slowT3Factor, int slowMaWidth, bool showFastSlopeColor, int fastSlopeSmooth, bool showFastVsSlowColor, bool showFill, int fillTransparency)
		{
			return indicator.CmUltimateMaMtf(input, fastMaLength, fastMaSource, fastMaType, fastT3Factor, fastMaWidth, showSlowMa, slowMaLength, slowMaSource, slowMaType, slowT3Factor, slowMaWidth, showFastSlopeColor, fastSlopeSmooth, showFastVsSlowColor, showFill, fillTransparency);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.CmUltimateMaMtf CmUltimateMaMtf(int fastMaLength, CmUltimateMaMtf_Source fastMaSource, CmUltimateMaMtf_MAType fastMaType, double fastT3Factor, int fastMaWidth, bool showSlowMa, int slowMaLength, CmUltimateMaMtf_Source slowMaSource, CmUltimateMaMtf_MAType slowMaType, double slowT3Factor, int slowMaWidth, bool showFastSlopeColor, int fastSlopeSmooth, bool showFastVsSlowColor, bool showFill, int fillTransparency)
		{
			return indicator.CmUltimateMaMtf(Input, fastMaLength, fastMaSource, fastMaType, fastT3Factor, fastMaWidth, showSlowMa, slowMaLength, slowMaSource, slowMaType, slowT3Factor, slowMaWidth, showFastSlopeColor, fastSlopeSmooth, showFastVsSlowColor, showFill, fillTransparency);
		}

		public Indicators.indTradingView.CmUltimateMaMtf CmUltimateMaMtf(ISeries<double> input , int fastMaLength, CmUltimateMaMtf_Source fastMaSource, CmUltimateMaMtf_MAType fastMaType, double fastT3Factor, int fastMaWidth, bool showSlowMa, int slowMaLength, CmUltimateMaMtf_Source slowMaSource, CmUltimateMaMtf_MAType slowMaType, double slowT3Factor, int slowMaWidth, bool showFastSlopeColor, int fastSlopeSmooth, bool showFastVsSlowColor, bool showFill, int fillTransparency)
		{
			return indicator.CmUltimateMaMtf(input, fastMaLength, fastMaSource, fastMaType, fastT3Factor, fastMaWidth, showSlowMa, slowMaLength, slowMaSource, slowMaType, slowT3Factor, slowMaWidth, showFastSlopeColor, fastSlopeSmooth, showFastVsSlowColor, showFill, fillTransparency);
		}
	}
}

#endregion
