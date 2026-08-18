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

// NT8 Version of Kalman Volume Trend [BigBeluga]
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the
// Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License (CC BY-NC-SA 4.0).
// The original Pine Script™ code is by BigBeluga and can be found at: https://www.tradingview.com/script/eGdeZydE-Kalman-Volume-Trend-BigBeluga/
// Adaptation for NinjaTrader by jack@mydailytake.com
// © 2026 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the CC BY-NC-SA 4.0 license. Full license details at https://creativecommons.org/licenses/by-nc-sa/4.0/
// The use of BigBeluga name or its adapted code in this work does not imply endorsement by the original authors.
//
// Notes:
//   Three layers stacked on the price panel:
//     • Kalman trend line — recursive 1-D Kalman filter on Close (Q = process noise, R = measurement noise);
//       trend flips when close crosses the ±ATR(200)*Mult bands (sticky — opposite band trails).
//     • Volume delta bars — per-bar SharpDX rectangles extending from the trend line outward by
//       ATR(200) * |normalizedDelta|. Normalized delta = (close>open ? +volume : -volume) / highest|.| over 100,
//       scaled to ±2. Color follows delta sign, length follows |delta|.
//     • Cumulative trend dashboard — bottom-right SharpDX HUD with BUY / SELL / DELTA gradient histograms
//       (10 rows each, opacity ladder) plus a TOTAL VOLUME footer. Counters reset at every trend flip.

#region Enums KalmanVolumeTrend

public enum KalmanVolumeTrend_TextSize
{
	Tiny,
	Small,
	Normal,
	Large
}

#endregion

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	#region Categories
	[Gui.CategoryOrder("Parameters",		10100)]
	[Gui.CategoryOrder("Volume Extremes",	10200)]
	[Gui.CategoryOrder("Dashboard",			10300)]
	[Gui.CategoryOrder("Colors",			10400)]
	#endregion

	public class KalmanVolumeTrend : Indicator
	{
		#region indInfo

		private string indName        = "Kalman Volume Trend [BigBeluga]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by BigBeluga can be found here: https://www.tradingview.com/script/eGdeZydE-Kalman-Volume-Trend-BigBeluga/";

		#endregion

		#region Properties

		[NinjaScriptProperty]
		[Range(0.0001, double.MaxValue)]
		[Display(Order = 1, GroupName = "Parameters", Name = "Process Noise (Q)",
			Description = "How much the trend state is expected to change. Lower = smoother / slower; higher = tracks price more aggressively.")]
		public double ProcessNoise { get; set; }

		[NinjaScriptProperty]
		[Range(0.01, double.MaxValue)]
		[Display(Order = 2, GroupName = "Parameters", Name = "Measurement Noise (R)",
			Description = "Uncertainty in the price input. Higher = more smoothing (filter trusts its own prediction over new data).")]
		public double MeasurementNoise { get; set; }

		[NinjaScriptProperty]
		[Range(0.1, double.MaxValue)]
		[Display(Order = 3, GroupName = "Parameters", Name = "Band Multiplier",
			Description = "ATR(200) multiplier defining how far price must close beyond the Kalman line to flip the trend.")]
		public double BandMultiplier { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 1, GroupName = "Volume Extremes", Name = "Show Volume Extremes",
			Description = "Mark bars where |normalized delta| > 1.5 with X / value labels.")]
		public bool ShowVolumeExtremes { get; set; }

		[NinjaScriptProperty]
		[Range(0.5, double.MaxValue)]
		[Display(Order = 2, GroupName = "Volume Extremes", Name = "Volume Extreme Threshold",
			Description = "Absolute value of normalized delta above which an extreme is flagged. Pine default = 1.5.")]
		public double VolumeExtremeThreshold { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 1, GroupName = "Dashboard", Name = "Show Dashboard",
			Description = "Render the BUY / SELL / DELTA / TOTAL VOLUME HUD at the bottom-right.")]
		public bool ShowDashboard { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 2, GroupName = "Dashboard", Name = "Label Text Size",
			Description = "Font size for both the dashboard cells and the volume-extreme labels.")]
		public KalmanVolumeTrend_TextSize LabelTextSize { get; set; }

		[XmlIgnore]
		[Display(Order = 1, GroupName = "Colors", Name = "Up Trend Color",
			Description = "Trend line + bull volume color when in an uptrend.")]
		public Brush UpTrendColor { get; set; }
			[Browsable(false)]
			public string UpTrendColorSerialize
			{
				get { return Serialize.BrushToString(UpTrendColor); }
				set { UpTrendColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore]
		[Display(Order = 2, GroupName = "Colors", Name = "Down Trend Color",
			Description = "Trend line + bear volume color when in a downtrend.")]
		public Brush DownTrendColor { get; set; }
			[Browsable(false)]
			public string DownTrendColorSerialize
			{
				get { return Serialize.BrushToString(DownTrendColor); }
				set { DownTrendColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		#endregion

		#region Public outputs

		[Browsable(false)][XmlIgnore] public Series<double>	KFLine    { get { return Values[0]; } }
		[Browsable(false)][XmlIgnore] public Series<bool>	IsUptrend { get { Update(); return sIsUptrend; } }
		[Browsable(false)][XmlIgnore] public Series<double>	Delta     { get { Update(); return sDelta; } }

		#endregion

		#region Variables

		private const int AtrLen        = 200;
		private const int DeltaWindow   = 100;
		private const int RangeMaLen    = 100;
		private const int HistogramRows = 10;

		private ATR atr200;
		private SMA smaRange;	// SMA(High - Low, 100)
		private MAX maxAbsDelta;	// highest |delta_sign| over last 100 bars

		// Helper input series feeding the wrappers above (must be persistent fields, not transient locals).
		private Series<double>	sRange;			// High[0] - Low[0]
		private Series<double>	sAbsDeltaSign;	// |close>open ? +vol : -vol|

		// Per-bar series (Infinite — OnRender walks history).
		private Series<double>	sKfLine;		// Kalman trend line (post band-flip selection)
		private Series<double>	sVolumeBar;		// per-bar volume-bar tip price
		private Series<double>	sAtr2;			// per-bar small offset for stub bar
		private Series<double>	sDelta;			// normalized delta in [-2, 2]
		private Series<double>	sDeltaSign;		// raw signed volume (close>open ? +vol : -vol)
		private Series<int>		sDirection;		// 1 = up, -1 = down (0 = pre-init)
		private Series<bool>	sIsUptrend;
		private Series<bool>	sTrendChanged;	// true on the bar where direction flipped

		// Kalman state.
		private double kalmanX = double.NaN;
		private double kalmanP = 1.0;
		private bool   directionUp = false;

		// Cumulative trend stats (reset at each flip).
		private double cumBuy, cumSell, cumDelta, cumMaxTrendVol;

		// SharpDX brushes.
		private SharpDX.Direct2D1.SolidColorBrush dxUpLight, dxUpDark, dxDnLight, dxDnDark;
		private SharpDX.Direct2D1.SolidColorBrush dxUpFill, dxDnFill;
		private SharpDX.Direct2D1.SolidColorBrush dxBg, dxFrame, dxText, dxGray;
		private SharpDX.Direct2D1.SolidColorBrush[] dxUpLadder, dxDnLadder;	// 10-step opacity ladder per direction

		private SharpDX.DirectWrite.TextFormat tfBody;
		private KalmanVolumeTrend_TextSize lastTextSize = KalmanVolumeTrend_TextSize.Small;
		private bool dashFormatsBuilt = false;

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

				ProcessNoise			= 0.0005;
				MeasurementNoise		= 0.4;
				BandMultiplier			= 2.0;
				ShowVolumeExtremes		= true;
				VolumeExtremeThreshold	= 1.5;
				ShowDashboard			= true;
				LabelTextSize			= KalmanVolumeTrend_TextSize.Small;
				UpTrendColor			= new SolidColorBrush(Color.FromRgb(0x1A, 0x6C, 0xCA));
				DownTrendColor			= new SolidColorBrush(Color.FromRgb(0xCA, 0x1A, 0xAD));
				EnsureFrozen(UpTrendColor);
				EnsureFrozen(DownTrendColor);

				AddPlot(new Stroke(Brushes.DodgerBlue, DashStyleHelper.Solid, 3f), PlotStyle.Line, "KF Trend");
			}
			else if (State == State.DataLoaded)
			{
				atr200        = ATR(AtrLen);
				sRange        = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sAbsDeltaSign = new Series<double>(this, MaximumBarsLookBack.Infinite);
				smaRange      = SMA(sRange, RangeMaLen);
				maxAbsDelta   = MAX(sAbsDeltaSign, DeltaWindow);

				sKfLine       = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sVolumeBar    = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sAtr2         = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sDelta        = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sDeltaSign    = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sDirection    = new Series<int>   (this, MaximumBarsLookBack.Infinite);
				sIsUptrend    = new Series<bool>  (this, MaximumBarsLookBack.Infinite);
				sTrendChanged = new Series<bool>  (this, MaximumBarsLookBack.Infinite);
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
				DisposeTextFormats();
			}
		}

		#endregion

		#region OnBarUpdate

		protected override void OnBarUpdate()
		{
			if (CurrentBar < 1) return;

			// Feed helper series before reading the wrappers built on top of them.
			sAbsDeltaSign[0] = Math.Abs(Close[0] > Open[0] ? Volume[0] : -Volume[0]);
			sRange[0]        = High[0] - Low[0];

			double deltaSign = Close[0] > Open[0] ? Volume[0] : -Volume[0];
			double hi        = maxAbsDelta[0];
			double delta     = hi > 0 ? deltaSign / hi * 2.0 : 0.0;

			double atrV = atr200[0];

			// Kalman recurrence on Close.
			if (double.IsNaN(kalmanX))
			{
				kalmanX = Close[0];
				kalmanP = 1.0;
			}
			kalmanP += ProcessNoise;
			double k = kalmanP / (kalmanP + MeasurementNoise);
			kalmanX += k * (Close[0] - kalmanX);
			kalmanP  = (1.0 - k) * kalmanP;

			double upBand = kalmanX + atrV * BandMultiplier;
			double dnBand = kalmanX - atrV * BandMultiplier;

			// Direction flip: close crosses upper (going up) or lower (going down) band.
			bool prevDir = directionUp;
			if (CurrentBar >= 1)
			{
				if (Close[0] > upBand && Close[1] < upBand) directionUp = true;
				else if (Close[0] < dnBand && Close[1] > dnBand) directionUp = false;
			}
			bool trendChanged = directionUp != prevDir;

			double kfTrendLine = directionUp ? dnBand : upBand;
			Values[0][0]    = kfTrendLine;
			sKfLine[0]      = kfTrendLine;
			sIsUptrend[0]   = directionUp;
			sDirection[0]   = directionUp ? 1 : -1;
			sTrendChanged[0]= trendChanged;
			sDelta[0]       = delta;
			sDeltaSign[0]   = deltaSign;

			// Volume bar tip (kfTrendLine + atrV * |delta| toward direction).
			double volumeBar = kfTrendLine + atrV * Math.Abs(delta) * (directionUp ? 1.0 : -1.0);
			sVolumeBar[0] = volumeBar;
			sAtr2[0]      = smaRange[0] * 0.1 * (directionUp ? 1.0 : -1.0);

			// Trend line color flow (drives the plot — direction state, not delta).
			PlotBrushes[0][0] = directionUp ? UpTrendColor : DownTrendColor;
			if (trendChanged) PlotBrushes[0][0] = Brushes.Transparent;	// Pine plots na on flip bars

			// Cumulative stats reset on flip.
			if (trendChanged || CurrentBar == 1)
			{
				cumBuy = cumSell = cumDelta = 0;
				cumMaxTrendVol = 0.1;
			}
			cumBuy   += deltaSign > 0 ? deltaSign : 0;
			cumSell  += deltaSign < 0 ? -deltaSign : 0;
			cumDelta += deltaSign;
			cumMaxTrendVol = Math.Max(Math.Max(cumBuy, cumSell), Math.Abs(cumDelta));
			if (cumMaxTrendVol < 0.1) cumMaxTrendVol = 0.1;

			// Trend-flip circle marker.
			RemoveDrawObject("kvtFlip" + CurrentBar);
			if (trendChanged && CurrentBar > 1)
			{
				Brush flipColor = directionUp ? UpTrendColor : DownTrendColor;
				Draw.Dot(this, "kvtFlip" + CurrentBar, false, 0, kfTrendLine, flipColor);
			}

			// Volume extreme labels.
			if (ShowVolumeExtremes && Math.Abs(delta) > VolumeExtremeThreshold)
			{
				Brush deltaColor = delta > 0 ? UpTrendColor : DownTrendColor;
				int   fontSize   = FontSizePx() + 2;
				string deltaTxt  = (delta > 0 ? "+" : "") + FormatVolume(deltaSign);
				if (directionUp)
				{
					Draw.Text(this, "kvtVolLbl" + CurrentBar, false, deltaTxt, 0, volumeBar + atrV * 0.3, 0,
						deltaColor, new SimpleFont("Arial", fontSize), TextAlignment.Center, Brushes.Transparent, Brushes.Transparent, 0);
					Draw.Text(this, "kvtMark" + CurrentBar, false, "X", 0, High[0] + atrV * 0.3, 0,
						Brushes.WhiteSmoke, new SimpleFont("Arial", fontSize), TextAlignment.Center, Brushes.Transparent, Brushes.Transparent, 0);
				}
				else
				{
					Draw.Text(this, "kvtVolLbl" + CurrentBar, false, deltaTxt, 0, volumeBar - atrV * 0.3, 0,
						deltaColor, new SimpleFont("Arial", fontSize), TextAlignment.Center, Brushes.Transparent, Brushes.Transparent, 0);
					Draw.Text(this, "kvtMark" + CurrentBar, false, "x", 0, Low[0] - atrV * 0.3, 0,
						Brushes.WhiteSmoke, new SimpleFont("Arial", fontSize), TextAlignment.Center, Brushes.Transparent, Brushes.Transparent, 0);
				}
			}
		}

		#endregion

		#region SharpDX render

		public override void OnRenderTargetChanged()
		{
			try
			{
				ReleaseRenderResources();
				if (RenderTarget == null) return;

				dxBg    = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color4(0.10f, 0.10f, 0.13f, 0.90f));
				dxFrame = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color4(0.6f, 0.6f, 0.6f, 0.5f));
				dxText  = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color4(0.95f, 0.95f, 0.95f, 1f));
				dxGray  = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color4(0.5f, 0.5f, 0.5f, 1f));

				var upScb = UpTrendColor as SolidColorBrush;
				var dnScb = DownTrendColor as SolidColorBrush;
				Color upC  = upScb != null ? upScb.Color : Colors.DodgerBlue;
				Color dnC  = dnScb != null ? dnScb.Color : Colors.MediumVioletRed;

				dxUpLight = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, ToColor4(upC, 0.50f));
				dxUpDark  = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, ToColor4(upC, 1.00f));
				dxDnLight = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, ToColor4(dnC, 0.50f));
				dxDnDark  = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, ToColor4(dnC, 1.00f));
				dxUpFill  = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, ToColor4(upC, 0.10f));
				dxDnFill  = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, ToColor4(dnC, 0.10f));

				// 10-step opacity ladders: row i has alpha = (10 - i) / 10 * 0.9 → fades from solid (top) to faint (bottom).
				dxUpLadder = new SharpDX.Direct2D1.SolidColorBrush[HistogramRows];
				dxDnLadder = new SharpDX.Direct2D1.SolidColorBrush[HistogramRows];
				for (int i = 0; i < HistogramRows; i++)
				{
					float a = (HistogramRows - i) / (float)HistogramRows * 0.9f;
					dxUpLadder[i] = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, ToColor4(upC, a));
					dxDnLadder[i] = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, ToColor4(dnC, a));
				}
			}
			catch
			{
				// Suppress during chart teardown.
			}
		}

		private void ReleaseRenderResources()
		{
			void D(ref SharpDX.Direct2D1.SolidColorBrush bx) { if (bx != null) { bx.Dispose(); bx = null; } }
			D(ref dxBg); D(ref dxFrame); D(ref dxText); D(ref dxGray);
			D(ref dxUpLight); D(ref dxUpDark); D(ref dxDnLight); D(ref dxDnDark);
			D(ref dxUpFill); D(ref dxDnFill);
			DisposeLadder(ref dxUpLadder);
			DisposeLadder(ref dxDnLadder);
		}

		private static void DisposeLadder(ref SharpDX.Direct2D1.SolidColorBrush[] arr)
		{
			if (arr == null) return;
			for (int i = 0; i < arr.Length; i++)
				if (arr[i] != null) { arr[i].Dispose(); arr[i] = null; }
			arr = null;
		}

		private void EnsureTextFormats()
		{
			if (dashFormatsBuilt && lastTextSize == LabelTextSize) return;
			DisposeTextFormats();
			tfBody = new SharpDX.DirectWrite.TextFormat(Core.Globals.DirectWriteFactory, "Arial",
				SharpDX.DirectWrite.FontWeight.Normal, SharpDX.DirectWrite.FontStyle.Normal, FontPx());
			lastTextSize = LabelTextSize;
			dashFormatsBuilt = true;
		}

		private void DisposeTextFormats()
		{
			if (tfBody != null) { tfBody.Dispose(); tfBody = null; }
			dashFormatsBuilt = false;
		}

		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			base.OnRender(chartControl, chartScale);

			if (RenderTarget == null) return;
			if (ChartBars == null)    return;
			if (!IsVisible)           return;
			if (IsInHitTest)          return;
			if (CurrentBar < 1)       return;
			if (sKfLine == null)      return;

			EnsureTextFormats();

			int fromIdx = Math.Max(ChartBars.FromIndex, 0);
			int toIdx   = Math.Min(ChartBars.ToIndex,   CurrentBar);

			// Pass 1 — trend background fill (translucent, kfTrendLine ↔ HL2).
			for (int j = fromIdx; j < toIdx; j++)
			{
				if (!sKfLine.IsValidDataPointAt(j) || !sKfLine.IsValidDataPointAt(j + 1)) continue;
				if (!sDirection.IsValidDataPointAt(j)) continue;

				double kfL = sKfLine.GetValueAt(j);
				double kfR = sKfLine.GetValueAt(j + 1);
				double hlL = (High.GetValueAt(j) + Low.GetValueAt(j)) / 2;
				double hlR = (High.GetValueAt(j + 1) + Low.GetValueAt(j + 1)) / 2;
				int dir = sDirection.GetValueAt(j);
				if (dir == 0) continue;

				var fillBrush = dir > 0 ? dxUpFill : dxDnFill;
				DrawTrapezoid(chartControl, chartScale, j, kfL, kfR, hlL, hlR, fillBrush);
			}

			// Pass 2 — volume bars (per-bar rectangle from kfTrendLine outward by atrV * |delta|).
			for (int j = fromIdx; j <= toIdx; j++)
			{
				if (!sKfLine.IsValidDataPointAt(j) || !sVolumeBar.IsValidDataPointAt(j)) continue;
				if (!sDelta.IsValidDataPointAt(j))  continue;

				double kf  = sKfLine.GetValueAt(j);
				double vb  = sVolumeBar.GetValueAt(j);
				double a2  = sAtr2.GetValueAt(j);
				double dl  = sDelta.GetValueAt(j);

				bool deltaUp = dl > 0;
				var lightBrush = deltaUp ? dxUpLight : dxDnLight;
				var darkBrush  = deltaUp ? dxUpDark  : dxDnDark;

				float xL = chartControl.GetXByBarIndex(ChartBars, j);
				float xR = j + 1 <= ChartBars.ToIndex
					? chartControl.GetXByBarIndex(ChartBars, j + 1)
					: xL + Math.Max(2f, (float)chartControl.BarWidth);
				float barW = Math.Max(1f, (xR - xL) * 0.8f);
				float xCenter = (xL + xR) / 2f - barW / 2f;

				float yKf  = (float)chartScale.GetYByValue(kf + a2);
				float yVB  = (float)chartScale.GetYByValue(vb);
				float yMin = Math.Min(yKf, yVB);
				float yMax = Math.Max(yKf, yVB);
				if (yMax - yMin < 1f) continue;

				// Outer (lighter) envelope from kfTrendLine+atr2 to volumeBar.
				RenderTarget.FillRectangle(new SharpDX.RectangleF(xCenter, yMin, barW, yMax - yMin), lightBrush);

				// Inner (darker) overlay - small core.
				float innerW = Math.Max(1f, barW * 0.5f);
				float innerX = (xL + xR) / 2f - innerW / 2f;
				RenderTarget.FillRectangle(new SharpDX.RectangleF(innerX, yMin, innerW, yMax - yMin), darkBrush);
			}

			if (ShowDashboard) RenderDashboard();
		}

		private void DrawTrapezoid(ChartControl chartControl, ChartScale chartScale, int barLeftIdx,
			double topPriceL, double topPriceR, double botPriceL, double botPriceR,
			SharpDX.Direct2D1.Brush brush)
		{
			float xL = chartControl.GetXByBarIndex(ChartBars, barLeftIdx);
			float xR = chartControl.GetXByBarIndex(ChartBars, barLeftIdx + 1);
			if (xR <= xL) return;

			float yTopL = (float)chartScale.GetYByValue(topPriceL);
			float yTopR = (float)chartScale.GetYByValue(topPriceR);
			float yBotL = (float)chartScale.GetYByValue(botPriceL);
			float yBotR = (float)chartScale.GetYByValue(botPriceR);

			SharpDX.Direct2D1.PathGeometry geom = null;
			SharpDX.Direct2D1.GeometrySink  sink = null;
			try
			{
				geom = new SharpDX.Direct2D1.PathGeometry(Core.Globals.D2DFactory);
				sink = geom.Open();
				sink.SetFillMode(SharpDX.Direct2D1.FillMode.Winding);
				sink.BeginFigure(new SharpDX.Vector2(xL, yTopL), SharpDX.Direct2D1.FigureBegin.Filled);
				sink.AddLine(new SharpDX.Vector2(xR, yTopR));
				sink.AddLine(new SharpDX.Vector2(xR, yBotR));
				sink.AddLine(new SharpDX.Vector2(xL, yBotL));
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

		private void RenderDashboard()
		{
			if (dxUpLadder == null || dxDnLadder == null || tfBody == null) return;

			float fontPx = FontPx();
			float padX   = 6f, padY = 6f;
			float colW   = fontPx * 5.5f;
			float rowH   = fontPx * 1.2f;
			int   cols   = 3;
			float w      = colW * cols + padX * 2;
			float h      = rowH * (HistogramRows + 2) + padY * 2;	// 10 histogram + 1 label + 1 footer

			float x = ChartPanel.X + ChartPanel.W - w - padX;
			float y = ChartPanel.Y + ChartPanel.H - h - padY;

			RenderTarget.FillRectangle(new SharpDX.RectangleF(x, y, w, h), dxBg);
			RenderTarget.DrawRectangle(new SharpDX.RectangleF(x, y, w, h), dxFrame, 1f);

			int buyScore   = (int)Math.Min(HistogramRows, cumBuy   / cumMaxTrendVol * HistogramRows);
			int sellScore  = (int)Math.Min(HistogramRows, cumSell  / cumMaxTrendVol * HistogramRows);
			int deltaScore = (int)Math.Min(HistogramRows, Math.Abs(cumDelta) / cumMaxTrendVol * HistogramRows);
			bool deltaUp   = cumDelta >= 0;

			// Histogram cells (top → bottom = high → low intensity).
			for (int i = 0; i < HistogramRows; i++)
			{
				float cy = y + padY + i * rowH;
				int filledFromTop = HistogramRows - i;	// top row is row 1, bottom row is row 10
				if (filledFromTop <= buyScore)
				{
					RenderTarget.FillRectangle(new SharpDX.RectangleF(x + padX,            cy, colW - 2f, rowH - 2f), dxUpLadder[i]);
				}
				if (filledFromTop <= sellScore)
				{
					RenderTarget.FillRectangle(new SharpDX.RectangleF(x + padX + colW,     cy, colW - 2f, rowH - 2f), dxDnLadder[i]);
				}
				if (filledFromTop <= deltaScore)
				{
					RenderTarget.FillRectangle(new SharpDX.RectangleF(x + padX + colW * 2, cy, colW - 2f, rowH - 2f), deltaUp ? dxUpLadder[i] : dxDnLadder[i]);
				}
			}

			// Header row — BUY / SELL / DELTA labels with totals.
			float headerY = y + padY + HistogramRows * rowH + 2f;
			DrawCell(string.Format("BUY\n{0}", FormatVolume(cumBuy)),       dxText, x + padX,            headerY, colW, rowH * 2);
			DrawCell(string.Format("SELL\n{0}", FormatVolume(cumSell)),     dxText, x + padX + colW,     headerY, colW, rowH * 2);
			DrawCell(string.Format("DELTA\n{0}", FormatVolume(cumDelta)),   dxText, x + padX + colW * 2, headerY, colW, rowH * 2);

			// Footer — TOTAL VOLUME merged across all 3 columns.
			float footerY = headerY + rowH * 1.6f;
			DrawCell("TOTAL VOLUME: " + FormatVolume(cumBuy + cumSell), dxGray, x + padX, footerY, colW * cols, rowH);
		}

		private void DrawCell(string text, SharpDX.Direct2D1.SolidColorBrush brush, float x, float y, float w, float h)
		{
			if (tfBody == null || brush == null || string.IsNullOrEmpty(text)) return;
			using (var layout = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, text, tfBody, w, h))
			{
				layout.TextAlignment = SharpDX.DirectWrite.TextAlignment.Center;
				layout.ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center;
				RenderTarget.DrawTextLayout(new SharpDX.Vector2(x, y), layout, brush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
			}
		}

		#endregion

		#region Helpers

		private float FontPx()
		{
			switch (LabelTextSize)
			{
				case KalmanVolumeTrend_TextSize.Tiny:   return 9f;
				case KalmanVolumeTrend_TextSize.Small:  return 11f;
				case KalmanVolumeTrend_TextSize.Normal: return 13f;
				case KalmanVolumeTrend_TextSize.Large:  return 16f;
			}
			return 11f;
		}

		private int FontSizePx()
		{
			switch (LabelTextSize)
			{
				case KalmanVolumeTrend_TextSize.Tiny:   return 8;
				case KalmanVolumeTrend_TextSize.Small:  return 10;
				case KalmanVolumeTrend_TextSize.Normal: return 12;
				case KalmanVolumeTrend_TextSize.Large:  return 15;
			}
			return 10;
		}

		private static string FormatVolume(double v)
		{
			double a = Math.Abs(v);
			string sign = v < 0 ? "-" : "";
			if (a >= 1e9) return sign + (a / 1e9).ToString("0.0") + "B";
			if (a >= 1e6) return sign + (a / 1e6).ToString("0.0") + "M";
			if (a >= 1e3) return sign + (a / 1e3).ToString("0.0") + "K";
			return sign + a.ToString("0");
		}

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
		private indTradingView.KalmanVolumeTrend[] cacheKalmanVolumeTrend;
		public indTradingView.KalmanVolumeTrend KalmanVolumeTrend(double processNoise, double measurementNoise, double bandMultiplier, bool showVolumeExtremes, double volumeExtremeThreshold, bool showDashboard, KalmanVolumeTrend_TextSize labelTextSize)
		{
			return KalmanVolumeTrend(Input, processNoise, measurementNoise, bandMultiplier, showVolumeExtremes, volumeExtremeThreshold, showDashboard, labelTextSize);
		}

		public indTradingView.KalmanVolumeTrend KalmanVolumeTrend(ISeries<double> input, double processNoise, double measurementNoise, double bandMultiplier, bool showVolumeExtremes, double volumeExtremeThreshold, bool showDashboard, KalmanVolumeTrend_TextSize labelTextSize)
		{
			if (cacheKalmanVolumeTrend != null)
				for (int idx = 0; idx < cacheKalmanVolumeTrend.Length; idx++)
					if (cacheKalmanVolumeTrend[idx] != null && cacheKalmanVolumeTrend[idx].ProcessNoise == processNoise && cacheKalmanVolumeTrend[idx].MeasurementNoise == measurementNoise && cacheKalmanVolumeTrend[idx].BandMultiplier == bandMultiplier && cacheKalmanVolumeTrend[idx].ShowVolumeExtremes == showVolumeExtremes && cacheKalmanVolumeTrend[idx].VolumeExtremeThreshold == volumeExtremeThreshold && cacheKalmanVolumeTrend[idx].ShowDashboard == showDashboard && cacheKalmanVolumeTrend[idx].LabelTextSize == labelTextSize && cacheKalmanVolumeTrend[idx].EqualsInput(input))
						return cacheKalmanVolumeTrend[idx];
			return CacheIndicator<indTradingView.KalmanVolumeTrend>(new indTradingView.KalmanVolumeTrend(){ ProcessNoise = processNoise, MeasurementNoise = measurementNoise, BandMultiplier = bandMultiplier, ShowVolumeExtremes = showVolumeExtremes, VolumeExtremeThreshold = volumeExtremeThreshold, ShowDashboard = showDashboard, LabelTextSize = labelTextSize }, input, ref cacheKalmanVolumeTrend);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.KalmanVolumeTrend KalmanVolumeTrend(double processNoise, double measurementNoise, double bandMultiplier, bool showVolumeExtremes, double volumeExtremeThreshold, bool showDashboard, KalmanVolumeTrend_TextSize labelTextSize)
		{
			return indicator.KalmanVolumeTrend(Input, processNoise, measurementNoise, bandMultiplier, showVolumeExtremes, volumeExtremeThreshold, showDashboard, labelTextSize);
		}

		public Indicators.indTradingView.KalmanVolumeTrend KalmanVolumeTrend(ISeries<double> input , double processNoise, double measurementNoise, double bandMultiplier, bool showVolumeExtremes, double volumeExtremeThreshold, bool showDashboard, KalmanVolumeTrend_TextSize labelTextSize)
		{
			return indicator.KalmanVolumeTrend(input, processNoise, measurementNoise, bandMultiplier, showVolumeExtremes, volumeExtremeThreshold, showDashboard, labelTextSize);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.KalmanVolumeTrend KalmanVolumeTrend(double processNoise, double measurementNoise, double bandMultiplier, bool showVolumeExtremes, double volumeExtremeThreshold, bool showDashboard, KalmanVolumeTrend_TextSize labelTextSize)
		{
			return indicator.KalmanVolumeTrend(Input, processNoise, measurementNoise, bandMultiplier, showVolumeExtremes, volumeExtremeThreshold, showDashboard, labelTextSize);
		}

		public Indicators.indTradingView.KalmanVolumeTrend KalmanVolumeTrend(ISeries<double> input , double processNoise, double measurementNoise, double bandMultiplier, bool showVolumeExtremes, double volumeExtremeThreshold, bool showDashboard, KalmanVolumeTrend_TextSize labelTextSize)
		{
			return indicator.KalmanVolumeTrend(input, processNoise, measurementNoise, bandMultiplier, showVolumeExtremes, volumeExtremeThreshold, showDashboard, labelTextSize);
		}
	}
}

#endregion
