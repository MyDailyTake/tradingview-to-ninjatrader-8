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

// NT8 Version of Smart Money Volume Index
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the Mozilla Public License 2.0 (MPL 2.0).
// The original Pine Script™ code is by AlgoAlpha and can be found at: https://www.tradingview.com/script/WBJhew74-Smart-Money-Volume-Index-AlgoAlpha/
// Adaptation for NinjaTrader by jack@mydailytake.com
// Write-up + downloads: https://mydailytake.com/smart-money-volume-index-algoalpha-ninjatrader-8/
// Source + all conversions: https://github.com/MyDailyTake/tradingview-to-ninjatrader-8
// © 2026 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the Mozilla Public License 2.0. Full license details at https://mozilla.org/MPL/2.0/
// The use of AlgoAlpha name or its adapted code in this work does not imply endorsement by the original authors.
//
// Notes:
//   PVI (positive volume index) and NVI (negative volume index) are detrended by subtracting their
//   255-EMA, run through RSI, and combined into buy / sell ratios. Sums over IndexPeriod are
//   normalized to a peak over NormalizationPeriod, producing IndexBuy / IndexSell in [0..1] and a
//   NetIndex in [-1..1].
//
//   Display modes:
//     Compare — Buy and Sell interest plotted as separate lines, each filled toward zero.
//     Net     — Single net oscillator, filled with bull / bear color above / below zero.
//
//   Net-mode columns render as a single per-bar SharpDX gradient (heatmap-fade visual) rather than
//   five stacked semi-transparent plots — same effect, no plot proliferation.
//
//   Price-panel candles are recolored to match the active interest state (BarBrushes from a sub-panel
//   indicator). The redundant price-tracking line is omitted — chart candles already show close.
//
//   Non-repainting. Public Series outputs: IndexBuy, IndexSell, NetIndex.

#region Enums SmartMoneyVolumeIndex

public enum SmartMoneyVolumeIndex_Mode
{
	Compare,
	Net
}

#endregion

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	#region Categories
	[Gui.CategoryOrder("Settings",	10100)]
	[Gui.CategoryOrder("Colors",	10200)]
	#endregion

	public class SmartMoneyVolumeIndex : Indicator
	{
		#region indInfo

		private string indName        = "Smart Money Volume Index [AlgoAlpha]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by AlgoAlpha can be found here: https://www.tradingview.com/script/WBJhew74-Smart-Money-Volume-Index-AlgoAlpha/";

		#endregion

		#region Properties

		[NinjaScriptProperty]
		[Display(Order = 1, GroupName = "Settings", Name = "Display Mode",
			Description = "Compare = buy / sell interest as separate oscillators. Net = single combined oscillator.")]
		public SmartMoneyVolumeIndex_Mode Mode { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 2, GroupName = "Settings", Name = "Index Period",
			Description = "Bars summed when forming the interest index.")]
		public int IndexPeriod { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 3, GroupName = "Settings", Name = "Volume Flow Period",
			Description = "RSI period applied to the dumb / smart volume flows.")]
		public int VolumeFlowPeriod { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 4, GroupName = "Settings", Name = "Normalization Period",
			Description = "Lookback for the peak used to normalize the index into [0..1].")]
		public int NormalizationPeriod { get; set; }

		[NinjaScriptProperty]
		[Range(0.01, 0.99)]
		[Display(Order = 5, GroupName = "Settings", Name = "High Interest Threshold",
			Description = "Level (0.01–0.99) at which interest is considered 'high' for highlighting and bar coloring.")]
		public double HighInterestThreshold { get; set; }

		[XmlIgnore]
		[Display(Order = 1, GroupName = "Colors", Name = "Up Color",
			Description = "Primary bullish color (Buy Interest line, gradient fills, bar coloring).")]
		public Brush UpColor { get; set; }
			[Browsable(false)]
			public string UpColorSerialize
			{
				get { return Serialize.BrushToString(UpColor); }
				set { UpColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore]
		[Display(Order = 2, GroupName = "Colors", Name = "Secondary Up Color",
			Description = "Secondary bullish accent (used in the Net-mode gradient column).")]
		public Brush SecondaryUpColor { get; set; }
			[Browsable(false)]
			public string SecondaryUpColorSerialize
			{
				get { return Serialize.BrushToString(SecondaryUpColor); }
				set { SecondaryUpColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore]
		[Display(Order = 3, GroupName = "Colors", Name = "Down Color",
			Description = "Primary bearish color (Sell Interest line, gradient fills, bar coloring).")]
		public Brush DownColor { get; set; }
			[Browsable(false)]
			public string DownColorSerialize
			{
				get { return Serialize.BrushToString(DownColor); }
				set { DownColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore]
		[Display(Order = 4, GroupName = "Colors", Name = "Secondary Down Color",
			Description = "Secondary bearish accent (used in the Net-mode gradient column).")]
		public Brush SecondaryDownColor { get; set; }
			[Browsable(false)]
			public string SecondaryDownColorSerialize
			{
				get { return Serialize.BrushToString(SecondaryDownColor); }
				set { SecondaryDownColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		#endregion

		#region Public outputs

		[Browsable(false)][XmlIgnore] public Series<double> IndexBuy   { get { Update(); return sIndexBuy; } }
		[Browsable(false)][XmlIgnore] public Series<double> IndexSell  { get { Update(); return sIndexSell; } }
		[Browsable(false)][XmlIgnore] public Series<double> NetIndex   { get { Update(); return sNetIndex; } }

		#endregion

		#region Variables

		// Volume index series — recursive, default lookback (only [0]/[1] reads).
		private Series<double>	sPvi;
		private Series<double>	sNvi;

		// Detrended series fed into RSI.
		private Series<double>	sDumb;
		private Series<double>	sSmart;

		// Ratio series fed into SUM.
		private Series<double>	sRBuy;
		private Series<double>	sRSell;

		// Max-of-sums series fed into MAX for the peak.
		private Series<double>	sMaxSum;

		// Output series — IndexBuy/Sell/NetIndex are read by OnRender across the visible window.
		private Series<double>	sIndexBuy;	// Infinite — OnRender visible-window read.
		private Series<double>	sIndexSell;	// Infinite — OnRender visible-window read.
		private Series<double>	sNetIndex;	// Infinite — OnRender visible-window read.

		// NT indicators — instantiated as fields in DataLoaded.
		private EMA				emaPvi;
		private EMA				emaNvi;
		private RSI				rsiDumb;
		private RSI				rsiSmart;
		private SUM				sumBuyInd;
		private SUM				sumSellInd;
		private MAX				peakInd;

		// SharpDX gradient resources for the high / low zone fills (chart-fg-tinted bands above thr / below -thr).
		private SharpDX.Direct2D1.GradientStopCollection	zoneStopsActive;
		private SharpDX.Direct2D1.GradientStopCollection	zoneStopsIdle;

		// Top / bottom active flags (last bar's state, consumed in OnRender).
		private bool	topActive;
		private bool	botActive;

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

				Mode					= SmartMoneyVolumeIndex_Mode.Net;
				IndexPeriod				= 25;
				VolumeFlowPeriod		= 14;
				NormalizationPeriod		= 500;
				HighInterestThreshold	= 0.9;

				UpColor				= new SolidColorBrush(Color.FromRgb(0x00, 0xff, 0xbb));
				SecondaryUpColor	= new SolidColorBrush(Color.FromRgb(0x00, 0x84, 0x61));
				DownColor			= new SolidColorBrush(Color.FromRgb(0xff, 0x11, 0x00));
				SecondaryDownColor	= new SolidColorBrush(Color.FromRgb(0x84, 0x09, 0x00));
				EnsureFrozen(UpColor);
				EnsureFrozen(SecondaryUpColor);
				EnsureFrozen(DownColor);
				EnsureFrozen(SecondaryDownColor);

				// Plot strokes default DimGray — per-bar color comes from PlotBrushes when overridden, or the property color when constant.
				AddPlot(new Stroke(Brushes.DimGray, DashStyleHelper.Solid, 2f), PlotStyle.Line, "Buy Interest");
				AddPlot(new Stroke(Brushes.DimGray, DashStyleHelper.Solid, 2f), PlotStyle.Line, "Sell Interest");
				AddPlot(new Stroke(Brushes.DimGray, DashStyleHelper.Solid, 2f), PlotStyle.Line, "Net Buy Line");
				AddPlot(new Stroke(Brushes.DimGray, DashStyleHelper.Solid, 2f), PlotStyle.Line, "Net Sell Line");

				AddLine(new Stroke(Brushes.Gray, DashStyleHelper.Solid, 1f), 0.0, "Zero");
			}
			else if (State == State.DataLoaded)
			{
				sPvi      = new Series<double>(this);
				sNvi      = new Series<double>(this);
				sDumb     = new Series<double>(this);
				sSmart    = new Series<double>(this);
				sRBuy     = new Series<double>(this);
				sRSell    = new Series<double>(this);
				// MAX(sMaxSum, NormalizationPeriod) reads NormalizationPeriod bars back of this series.
				// Default 256 lookback is too small (NormalizationPeriod default is 500) — needs Infinite.
				sMaxSum   = new Series<double>(this, MaximumBarsLookBack.Infinite);

				// OnRender renders gradient columns and reads Index/Net values across the visible window — needs Infinite.
				sIndexBuy  = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sIndexSell = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sNetIndex  = new Series<double>(this, MaximumBarsLookBack.Infinite);

				emaPvi     = EMA(sPvi,   255);
				emaNvi     = EMA(sNvi,   255);
				rsiDumb    = RSI(sDumb,  VolumeFlowPeriod, 1);
				rsiSmart   = RSI(sSmart, VolumeFlowPeriod, 1);
				sumBuyInd  = SUM(sRBuy,  IndexPeriod);
				sumSellInd = SUM(sRSell, IndexPeriod);
				peakInd    = MAX(sMaxSum, NormalizationPeriod);
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
			// Volume indices — accumulate only on the matching volume direction.
			double prevPvi = (CurrentBar > 0) ? sPvi[1] : 1.0;
			double prevNvi = (CurrentBar > 0) ? sNvi[1] : 1.0;

			double pvi = prevPvi;
			double nvi = prevNvi;
			if (CurrentBar > 0 && Close[1] != 0.0)
			{
				if (Volume[0] > Volume[1])
					pvi = prevPvi * Close[0] / Close[1];
				if (Volume[0] < Volume[1])
					nvi = prevNvi * Close[0] / Close[1];
			}
			sPvi[0] = pvi;
			sNvi[0] = nvi;

			// Detrended series.
			sDumb[0]  = pvi - emaPvi[0];
			sSmart[0] = nvi - emaNvi[0];

			// RSI requires its own warmup; gate the RSI-dependent calc once both are valid.
			int rsiWarmup = 255 + VolumeFlowPeriod + 2;
			if (CurrentBar < rsiWarmup)
			{
				sRBuy[0]  = 1.0;
				sRSell[0] = 1.0;
				sMaxSum[0] = 0.0;
				Values[0].Reset();
				Values[1].Reset();
				Values[2].Reset();
				Values[3].Reset();
				return;
			}

			double drsi = rsiDumb[0];
			double srsi = rsiSmart[0];

			double rBuy  = drsi == 0.0          ? 0.0 : srsi / drsi;
			double rSell = (100.0 - drsi) == 0.0 ? 0.0 : (100.0 - srsi) / (100.0 - drsi);
			sRBuy[0]  = rBuy;
			sRSell[0] = rSell;

			double sumsBuy  = sumBuyInd[0];
			double sumsSell = sumSellInd[0];
			sMaxSum[0] = Math.Max(sumsBuy, sumsSell);

			double peak = peakInd[0];
			if (peak == 0.0) peak = 1.0;

			double indexBuy  = sumsBuy  / peak;
			double indexSell = sumsSell / peak;
			double netIndex  = indexBuy - indexSell;

			sIndexBuy[0]  = indexBuy;
			sIndexSell[0] = indexSell;
			sNetIndex[0]  = netIndex;

			double sigBuy  = Mode == SmartMoneyVolumeIndex_Mode.Compare ? indexBuy  : netIndex;
			double sigSell = Mode == SmartMoneyVolumeIndex_Mode.Compare ? indexSell : -netIndex;

			// State flags consumed by OnRender for adaptive zone-fill brightness.
			topActive = sigBuy  > 0.7;
			botActive = sigSell > 0.7;

			// Plot routing per mode.
			if (Mode == SmartMoneyVolumeIndex_Mode.Compare)
			{
				Values[0][0]      = indexBuy;
				Values[1][0]      = -indexSell;
				PlotBrushes[0][0] = UpColor;
				PlotBrushes[1][0] = DownColor;
				Values[2].Reset();
				Values[3].Reset();
			}
			else
			{
				Values[0].Reset();
				Values[1].Reset();
				if (netIndex > 0)
				{
					Values[2][0]      = netIndex;
					PlotBrushes[2][0] = UpColor;
					Values[3].Reset();
				}
				else if (netIndex < 0)
				{
					Values[3][0]      = netIndex;
					PlotBrushes[3][0] = DownColor;
					Values[2].Reset();
				}
				else
				{
					Values[2].Reset();
					Values[3].Reset();
				}
			}

			// Bar coloring on the price panel.
			BarBrushes[0]           = ComputeBarBrush(indexBuy, indexSell, netIndex);
			CandleOutlineBrushes[0] = BarBrushes[0];
		}

		private Brush ComputeBarBrush(double indexBuy, double indexSell, double netIndex)
		{
			if (Mode == SmartMoneyVolumeIndex_Mode.Compare)
			{
				if (indexBuy  > HighInterestThreshold) return UpColor;
				if (indexSell > HighInterestThreshold) return DownColor;
				return ChartBgBrush();
			}

			// Net mode — gradient between chart background and accent color, scaled by |netIndex|.
			double abs = Math.Min(1.0, Math.Abs(netIndex));
			Brush  bg  = ChartBgBrush();
			Brush  accent = netIndex > 0 ? UpColor : DownColor;
			return GradientBrush(abs, 0.0, 1.0, bg, accent);
		}

		#endregion

		#region OnRenderTargetChanged + helpers

		public override void OnRenderTargetChanged()
		{
			try
			{
				ReleaseRenderResources();
				if (RenderTarget == null) return;

				BuildZoneStops();
			}
			catch
			{
				// Suppress during chart teardown.
			}
		}

		private void BuildZoneStops()
		{
			DisposeZoneStops();
			if (RenderTarget == null) return;

			// Zone fills tint the panel using a near-fg color (gray); brighter when active.
			SharpDX.Color4 active   = new SharpDX.Color4(0.85f, 0.85f, 0.85f, 0.55f);
			SharpDX.Color4 activeMid = new SharpDX.Color4(0.85f, 0.85f, 0.85f, 0.70f);
			SharpDX.Color4 idle     = new SharpDX.Color4(0.85f, 0.85f, 0.85f, 0.10f);
			SharpDX.Color4 idleMid  = new SharpDX.Color4(0.85f, 0.85f, 0.85f, 0.40f);

			zoneStopsActive = new SharpDX.Direct2D1.GradientStopCollection(RenderTarget, new[]
			{
				new SharpDX.Direct2D1.GradientStop { Position = 0f, Color = activeMid },
				new SharpDX.Direct2D1.GradientStop { Position = 1f, Color = active }
			});
			zoneStopsIdle = new SharpDX.Direct2D1.GradientStopCollection(RenderTarget, new[]
			{
				new SharpDX.Direct2D1.GradientStop { Position = 0f, Color = idleMid },
				new SharpDX.Direct2D1.GradientStop { Position = 1f, Color = idle }
			});
		}

		private void DisposeZoneStops()
		{
			if (zoneStopsActive != null) { zoneStopsActive.Dispose(); zoneStopsActive = null; }
			if (zoneStopsIdle   != null) { zoneStopsIdle.Dispose();   zoneStopsIdle   = null; }
		}

		private void ReleaseRenderResources()
		{
			DisposeZoneStops();
		}

		#endregion

		#region OnRender

		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			base.OnRender(chartControl, chartScale);

			if (RenderTarget == null)	return;
			if (ChartBars == null)		return;
			if (!IsVisible)				return;
			if (IsInHitTest)			return;
			if (sNetIndex == null)		return;

			int fromIdx = Math.Max(ChartBars.FromIndex, 0);
			int toIdx   = Math.Min(ChartBars.ToIndex,   CurrentBar);
			if (toIdx <= fromIdx) return;

			float panelLeft  = ChartPanel.X;
			float panelRight = ChartPanel.X + ChartPanel.W;

			float yAt1   = (float)chartScale.GetYByValue( 1.0);
			float yAtThr = (float)chartScale.GetYByValue( HighInterestThreshold);
			float yAtNTh = (float)chartScale.GetYByValue(-HighInterestThreshold);
			float yAtN1  = (float)chartScale.GetYByValue(-1.0);
			float yAt0   = (float)chartScale.GetYByValue( 0.0);

			// Zone fills — fixed Y extent, full panel width.
			RenderZoneFill(panelLeft, panelRight, yAt1,    yAtThr,  topActive ? zoneStopsActive : zoneStopsIdle, vertical: true);
			RenderZoneFill(panelLeft, panelRight, yAtNTh,  yAtN1,   botActive ? zoneStopsActive : zoneStopsIdle, vertical: false);

			// Per-bar value gradient column (the heatmap fade — replaces the 5 stacked-column plots in the source).
			for (int j = fromIdx; j < toIdx; j++)
			{
				if (Mode == SmartMoneyVolumeIndex_Mode.Compare)
				{
					if (sIndexBuy.IsValidDataPointAt(j) && sIndexBuy.IsValidDataPointAt(j + 1))
					{
						double bJ  = sIndexBuy.GetValueAt(j);
						double bJ1 = sIndexBuy.GetValueAt(j + 1);
						RenderValueColumn(chartControl, chartScale, j, bJ, bJ1, yAt0, UpColor, true);
					}
					if (sIndexSell.IsValidDataPointAt(j) && sIndexSell.IsValidDataPointAt(j + 1))
					{
						double sJ  = sIndexSell.GetValueAt(j);
						double sJ1 = sIndexSell.GetValueAt(j + 1);
						RenderValueColumn(chartControl, chartScale, j, -sJ, -sJ1, yAt0, DownColor, false);
					}
				}
				else
				{
					if (sNetIndex.IsValidDataPointAt(j) && sNetIndex.IsValidDataPointAt(j + 1))
					{
						double nJ  = sNetIndex.GetValueAt(j);
						double nJ1 = sNetIndex.GetValueAt(j + 1);
						bool   pos = (nJ + nJ1) >= 0.0;
						Brush  topAccent = pos ? UpColor      : DownColor;
						Brush  botAccent = pos ? SecondaryUpColor : SecondaryDownColor;
						RenderValueColumn(chartControl, chartScale, j, nJ, nJ1, yAt0, topAccent, pos, alternateBottom: botAccent);
					}
				}
			}
		}

		private void RenderZoneFill(float xL, float xR, float yA, float yB,
			SharpDX.Direct2D1.GradientStopCollection stops, bool vertical)
		{
			if (stops == null) return;
			SharpDX.Direct2D1.LinearGradientBrush brush = null;
			try
			{
				brush = new SharpDX.Direct2D1.LinearGradientBrush(RenderTarget,
					new SharpDX.Direct2D1.LinearGradientBrushProperties
					{
						StartPoint = new SharpDX.Vector2(0f, vertical ? yA : yB),
						EndPoint   = new SharpDX.Vector2(0f, vertical ? yB : yA)
					},
					stops);
				float yTop = Math.Min(yA, yB);
				float yBot = Math.Max(yA, yB);
				RenderTarget.FillRectangle(new SharpDX.RectangleF(xL, yTop, xR - xL, yBot - yTop), brush);
			}
			finally
			{
				if (brush != null) brush.Dispose();
			}
		}

		// Renders one bar's gradient column from value 0 up/down to the bar's value.
		// `topAccent` is the solid color anchored at the bar's value; the bottom of the column fades to bg or to `alternateBottom` if provided.
		private void RenderValueColumn(ChartControl chartControl, ChartScale chartScale, int barLeftIdx,
			double valJ, double valJ1, float yAtZero, Brush topAccent, bool isPositive,
			Brush alternateBottom = null)
		{
			// Skip degenerate bars (value on the wrong side of zero for this column type).
			if (isPositive && valJ <= 0.0 && valJ1 <= 0.0)  return;
			if (!isPositive && valJ >= 0.0 && valJ1 >= 0.0) return;

			float xL = chartControl.GetXByBarIndex(ChartBars, barLeftIdx);
			float xR = chartControl.GetXByBarIndex(ChartBars, barLeftIdx + 1);

			float yJ  = (float)chartScale.GetYByValue(valJ);
			float yJ1 = (float)chartScale.GetYByValue(valJ1);

			// Per-bar gradient — top anchor at the average bar value, bottom anchor at zero.
			float yAvgTop = (yJ + yJ1) * 0.5f;

			SharpDX.Color4 topC = ToColor4(topAccent, 1.0f);
			SharpDX.Color4 botC = alternateBottom != null
				? ToColor4(alternateBottom, 0.30f)
				: new SharpDX.Color4(topC.Red, topC.Green, topC.Blue, 0.0f);

			SharpDX.Direct2D1.GradientStopCollection stops = null;
			SharpDX.Direct2D1.LinearGradientBrush brush = null;
			try
			{
				stops = new SharpDX.Direct2D1.GradientStopCollection(RenderTarget, new[]
				{
					new SharpDX.Direct2D1.GradientStop { Position = 0f, Color = topC },
					new SharpDX.Direct2D1.GradientStop { Position = 1f, Color = botC }
				});
				brush = new SharpDX.Direct2D1.LinearGradientBrush(RenderTarget,
					new SharpDX.Direct2D1.LinearGradientBrushProperties
					{
						StartPoint = new SharpDX.Vector2(0f, yAvgTop),
						EndPoint   = new SharpDX.Vector2(0f, yAtZero)
					},
					stops);

				// Trapezoid: top edge follows the value line, bottom edge clamped to zero line.
				SharpDX.Direct2D1.PathGeometry geom = null;
				SharpDX.Direct2D1.GeometrySink sink = null;
				try
				{
					geom = new SharpDX.Direct2D1.PathGeometry(Core.Globals.D2DFactory);
					sink = geom.Open();
					sink.SetFillMode(SharpDX.Direct2D1.FillMode.Winding);
					sink.BeginFigure(new SharpDX.Vector2(xL, yJ), SharpDX.Direct2D1.FigureBegin.Filled);
					sink.AddLine(new SharpDX.Vector2(xR, yJ1));
					sink.AddLine(new SharpDX.Vector2(xR, yAtZero));
					sink.AddLine(new SharpDX.Vector2(xL, yAtZero));
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
			finally
			{
				if (brush != null) brush.Dispose();
				if (stops != null) stops.Dispose();
			}
		}

		#endregion

		#region Color helpers

		private Brush ChartBgBrush()
		{
			if (ChartControl != null && ChartControl.Properties != null && ChartControl.Properties.ChartBackground is SolidColorBrush scb)
				return scb;
			return Brushes.Black;
		}

		private static Brush GradientBrush(double v, double lo, double hi, Brush a, Brush b)
		{
			double t = (hi == lo) ? 0.0 : (v - lo) / (hi - lo);
			t = Math.Max(0.0, Math.Min(1.0, t));

			var ca = (a as SolidColorBrush)?.Color ?? Colors.Gray;
			var cb = (b as SolidColorBrush)?.Color ?? Colors.Gray;

			byte A = (byte)(ca.A + (cb.A - ca.A) * t);
			byte R = (byte)(ca.R + (cb.R - ca.R) * t);
			byte G = (byte)(ca.G + (cb.G - ca.G) * t);
			byte B = (byte)(ca.B + (cb.B - ca.B) * t);
			return EnsureFrozen(new SolidColorBrush(Color.FromArgb(A, R, G, B)));
		}

		private static SharpDX.Color4 ToColor4(Brush wpf, float alphaScale)
		{
			var scb = wpf as SolidColorBrush;
			System.Windows.Media.Color c = scb != null ? scb.Color : Colors.Gray;
			float wpfA = scb != null ? (float)(scb.Opacity * (c.A / 255f)) : 1f;
			return new SharpDX.Color4(c.R / 255f, c.G / 255f, c.B / 255f,
				Math.Max(0f, Math.Min(1f, alphaScale * wpfA)));
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
		private indTradingView.SmartMoneyVolumeIndex[] cacheSmartMoneyVolumeIndex;
		public indTradingView.SmartMoneyVolumeIndex SmartMoneyVolumeIndex(SmartMoneyVolumeIndex_Mode mode, int indexPeriod, int volumeFlowPeriod, int normalizationPeriod, double highInterestThreshold)
		{
			return SmartMoneyVolumeIndex(Input, mode, indexPeriod, volumeFlowPeriod, normalizationPeriod, highInterestThreshold);
		}

		public indTradingView.SmartMoneyVolumeIndex SmartMoneyVolumeIndex(ISeries<double> input, SmartMoneyVolumeIndex_Mode mode, int indexPeriod, int volumeFlowPeriod, int normalizationPeriod, double highInterestThreshold)
		{
			if (cacheSmartMoneyVolumeIndex != null)
				for (int idx = 0; idx < cacheSmartMoneyVolumeIndex.Length; idx++)
					if (cacheSmartMoneyVolumeIndex[idx] != null && cacheSmartMoneyVolumeIndex[idx].Mode == mode && cacheSmartMoneyVolumeIndex[idx].IndexPeriod == indexPeriod && cacheSmartMoneyVolumeIndex[idx].VolumeFlowPeriod == volumeFlowPeriod && cacheSmartMoneyVolumeIndex[idx].NormalizationPeriod == normalizationPeriod && cacheSmartMoneyVolumeIndex[idx].HighInterestThreshold == highInterestThreshold && cacheSmartMoneyVolumeIndex[idx].EqualsInput(input))
						return cacheSmartMoneyVolumeIndex[idx];
			return CacheIndicator<indTradingView.SmartMoneyVolumeIndex>(new indTradingView.SmartMoneyVolumeIndex(){ Mode = mode, IndexPeriod = indexPeriod, VolumeFlowPeriod = volumeFlowPeriod, NormalizationPeriod = normalizationPeriod, HighInterestThreshold = highInterestThreshold }, input, ref cacheSmartMoneyVolumeIndex);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.SmartMoneyVolumeIndex SmartMoneyVolumeIndex(SmartMoneyVolumeIndex_Mode mode, int indexPeriod, int volumeFlowPeriod, int normalizationPeriod, double highInterestThreshold)
		{
			return indicator.SmartMoneyVolumeIndex(Input, mode, indexPeriod, volumeFlowPeriod, normalizationPeriod, highInterestThreshold);
		}

		public Indicators.indTradingView.SmartMoneyVolumeIndex SmartMoneyVolumeIndex(ISeries<double> input , SmartMoneyVolumeIndex_Mode mode, int indexPeriod, int volumeFlowPeriod, int normalizationPeriod, double highInterestThreshold)
		{
			return indicator.SmartMoneyVolumeIndex(input, mode, indexPeriod, volumeFlowPeriod, normalizationPeriod, highInterestThreshold);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.SmartMoneyVolumeIndex SmartMoneyVolumeIndex(SmartMoneyVolumeIndex_Mode mode, int indexPeriod, int volumeFlowPeriod, int normalizationPeriod, double highInterestThreshold)
		{
			return indicator.SmartMoneyVolumeIndex(Input, mode, indexPeriod, volumeFlowPeriod, normalizationPeriod, highInterestThreshold);
		}

		public Indicators.indTradingView.SmartMoneyVolumeIndex SmartMoneyVolumeIndex(ISeries<double> input , SmartMoneyVolumeIndex_Mode mode, int indexPeriod, int volumeFlowPeriod, int normalizationPeriod, double highInterestThreshold)
		{
			return indicator.SmartMoneyVolumeIndex(input, mode, indexPeriod, volumeFlowPeriod, normalizationPeriod, highInterestThreshold);
		}
	}
}

#endregion
