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

// NT8 Version of Volume Whale Zones [ChartPrime]
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the Mozilla Public License 2.0 (MPL 2.0).
// The original Pine Script™ code is by ChartPrime and can be found at: https://www.tradingview.com/script/ldmdnX5K-Volume-Whale-Zones-ChartPrime/
// Adaptation for NinjaTrader by jack@mydailytake.com
// © 2026 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the Mozilla Public License 2.0. Full license details at https://mozilla.org/MPL/2.0/
// The use of ChartPrime name or its adapted code in this work does not imply endorsement by the original authors.
//
// Notes:
//   Three-stage smoothed Point of Control (POC):
//     • Range engine — tracks the highest-high / lowest-low across the lookback window. The range only
//       expands; once the band-extreme is hit, it locks until a new high or low is set.
//     • 50-bin volume profile — distributes Volume[i] for each of the last `Lookback` bars into the
//       price bin its close belongs to. The bin with the most volume is the raw POC.
//     • Three-stage low-pass:
//         hotZone0 = EMA(clamp(rawPOC, ±2×ATR), 10)            (volatility clamp + EMA)
//         hotZone1 = hotZone0 if |hotZone0 − hotZone1| > 2×ATR (snap on big moves only)
//         hotZone2 = hotZone1 if |hotZone1 − hotZone2| > 2×ATR (final stable zone)
//
//   Documented scope decisions:
//     • Pine `plot.style_stepline` reproduced via NT8 `PlotStyle.Square` (NT8 has no stepline; Square
//       at small width approximates the staircase effect closer than Line).
//     • Pine 21-cell deviation oscillator table (bottom-center HUD) rendered via SharpDX OnRender —
//       cells track close-vs-hotZone deviation in ATR units, clamped to ±4.
//     • Pine `show_last = 4500` is a TV-only render hint — the NT8 plot honours its own BarsRequiredToPlot.

#region Enums VolumeWhaleZones

public enum VolumeWhaleZones_TextSize
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
	[Gui.CategoryOrder("Parameters",	10100)]
	[Gui.CategoryOrder("Colors",		10200)]
	#endregion

	public class VolumeWhaleZones : Indicator
	{
		#region indInfo

		private string indName        = "Volume Whale Zones [ChartPrime]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by ChartPrime can be found here: https://www.tradingview.com/script/ldmdnX5K-Volume-Whale-Zones-ChartPrime/";

		#endregion

		#region Properties

		[NinjaScriptProperty]
		[Range(10, int.MaxValue)]
		[Display(Order = 1, GroupName = "Parameters", Name = "Lookback",
			Description = "Window over which the volume profile + range are computed.")]
		public int Lookback { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 2, GroupName = "Parameters", Name = "Show Hot Zone Band",
			Description = "Render the translucent ATR-extent band around the hot zone line.")]
		public bool ShowHotZone { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 3, GroupName = "Parameters", Name = "Label Text Size",
			Description = "Text size for the deviation oscillator HUD at the bottom of the panel.")]
		public VolumeWhaleZones_TextSize LabelTextSize { get; set; }

		[XmlIgnore]
		[Display(Order = 1, GroupName = "Colors", Name = "Hot Zone Color",
			Description = "Color for the hot zone line + ATR band.")]
		public Brush HotZoneColor { get; set; }
			[Browsable(false)]
			public string HotZoneColorSerialize
			{
				get { return Serialize.BrushToString(HotZoneColor); }
				set { HotZoneColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore]
		[Display(Order = 2, GroupName = "Colors", Name = "Lookback Color",
			Description = "Color for the recent-window hot line overlay.")]
		public Brush LookbackColor { get; set; }
			[Browsable(false)]
			public string LookbackColorSerialize
			{
				get { return Serialize.BrushToString(LookbackColor); }
				set { LookbackColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		#endregion

		#region Public outputs

		[Browsable(false)][XmlIgnore] public Series<double>	HotZone   { get { return Values[0]; } }
		[Browsable(false)][XmlIgnore] public Series<double>	HotZoneUp { get { Update(); return sHotZoneUp; } }
		[Browsable(false)][XmlIgnore] public Series<double>	HotZoneDn { get { Update(); return sHotZoneDn; } }

		#endregion

		#region Variables

		private const int Rows = 50;
		private const int SmoothLen = 10;

		private ATR atr200;
		private ATR atr500;
		private MAX maxAtrInner;	// MAX(atr200, 500)
		private MAX maxAtrOuter;	// MAX(maxAtrInner, 500) — Pine `highest(highest(ATR,500),500)`

		private Series<double>	sHotZone0;
		private Series<double>	sHotZone1;
		private Series<double>	sHotZone2;
		private Series<double>	sHotZoneUp;
		private Series<double>	sHotZoneDn;
		private EMA				ema0;

		// Range tracking — only expand.
		private double	rangeHigh = double.NaN;
		private double	rangeLow  = double.NaN;
		private bool	rangeExpanded;

		// Reusable bin buffer.
		private double[] binVolumes = new double[Rows];

		// SharpDX HUD chrome + zone-band fill brushes.
		private SharpDX.Direct2D1.SolidColorBrush dxFrame;
		private SharpDX.Direct2D1.SolidColorBrush dxText;
		private SharpDX.Direct2D1.SolidColorBrush dxHotCenter;
		private SharpDX.Direct2D1.SolidColorBrush dxLookback;	// orange line overlay for the last `Lookback` bars
		private SharpDX.Direct2D1.SolidColorBrush dxHudBg;		// HUD panel background — constant colour, cached (never per-frame)
		// Per-bar plot brushes for the hot-zone line — two frozen states (visible / faded-out), cached once.
		private Brush pbHotOpaque;
		private Brush pbHotClear;
		// Gradient stops for the band — solid HotZoneColor at edge, transparent at hot line.
		private SharpDX.Direct2D1.GradientStopCollection gsUpperBand;
		private SharpDX.Direct2D1.GradientStopCollection gsLowerBand;
		private SharpDX.DirectWrite.TextFormat tfBody;
		private VolumeWhaleZones_TextSize lastTextSize = VolumeWhaleZones_TextSize.Small;
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

				Lookback      = 200;
				ShowHotZone   = true;
				LabelTextSize = VolumeWhaleZones_TextSize.Small;
				HotZoneColor  = Brushes.Red;
				LookbackColor = new SolidColorBrush(Color.FromRgb(0xFF, 0x7B, 0x00));
				EnsureFrozen(HotZoneColor);
				EnsureFrozen(LookbackColor);

				AddPlot(new Stroke(Brushes.DimGray, DashStyleHelper.Solid, 8f), PlotStyle.Square, "Hot Line");
			}
			else if (State == State.DataLoaded)
			{
				atr200       = ATR(200);
				atr500       = ATR(500);
				maxAtrInner  = MAX(atr200, 500);
				maxAtrOuter  = MAX(maxAtrInner, 500);
				// Need [1] history on hotZone0 / hotZone1 / hotZone2 — Infinite for OnRender deviation oscillator.
				sHotZone0  = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sHotZone1  = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sHotZone2  = new Series<double>(this, MaximumBarsLookBack.Infinite);
				// OnRender consumes Up / Dn across the visible window for the zone-band fill — Infinite.
				sHotZoneUp = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sHotZoneDn = new Series<double>(this, MaximumBarsLookBack.Infinite);
				ema0       = EMA(sHotZone0, SmoothLen);

				// Cache the two per-bar hot-zone line brushes once (visible + faded-out) instead of
				// allocating a fresh brush every bar in OnBarUpdate.
				pbHotOpaque = WithAlpha(HotZoneColor, 127);
				pbHotClear  = WithAlpha(HotZoneColor, 0);
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
			if (CurrentBar < Lookback) return;

			// Range engine — only expand.
			double newHigh = double.MinValue, newLow = double.MaxValue;
			for (int i = 0; i < Lookback; i++)
			{
				if (High[i] > newHigh) newHigh = High[i];
				if (Low [i] < newLow ) newLow  = Low [i];
			}
			if (double.IsNaN(rangeHigh)) { rangeHigh = newHigh; rangeLow = newLow; }
			rangeExpanded = newHigh > rangeHigh || newLow < rangeLow;
			if (rangeExpanded) { rangeHigh = newHigh; rangeLow = newLow; }

			double priceStep = (rangeHigh - rangeLow) / Rows;
			if (priceStep <= 0) return;

			// Volume profile — bin each closed bar's volume into its price bin.
			Array.Clear(binVolumes, 0, Rows);
			for (int i = 0; i < Lookback; i++)
			{
				int idx = (int)Math.Floor((Close[i] - rangeLow) / priceStep);
				if (idx < 0) idx = 0;
				else if (idx >= Rows) idx = Rows - 1;
				binVolumes[idx] += Volume[i];
			}

			// POC.
			int pocIdx = 0;
			double maxVol = 0;
			for (int i = 0; i < Rows; i++)
				if (binVolumes[i] > maxVol) { maxVol = binVolumes[i]; pocIdx = i; }
			double rawPOC = rangeLow + priceStep * (pocIdx + 0.5);

			double atrV = atr200[0];
			double maxJump = atrV * 2;

			// Stage 0 — clamp + EMA.
			double prevH0 = CurrentBar > 0 ? sHotZone0[1] : rawPOC;
			double seedH0 = rangeExpanded ? rawPOC : prevH0;
			double delta = Math.Max(-maxJump, Math.Min(maxJump, rawPOC - seedH0));
			sHotZone0[0] = seedH0 + delta;
			// Apply NT EMA on the series.
			double emaH0 = ema0[0];
			sHotZone0[0] = emaH0;

			// Stage 1 — snap when |H0 − H1| > 2×ATR.
			double prevH1 = CurrentBar > 0 ? sHotZone1[1] : sHotZone0[0];
			sHotZone1[0] = Math.Abs(sHotZone0[0] - prevH1) > maxJump ? sHotZone0[0] : prevH1;

			// Stage 2 — final stable zone.
			double prevH2 = CurrentBar > 0 ? sHotZone2[1] : sHotZone1[0];
			sHotZone2[0] = Math.Abs(sHotZone1[0] - prevH2) > maxJump ? sHotZone1[0] : prevH2;

			Values[0][0] = sHotZone2[0];

			// Per-bar color flow — fade to fully transparent when the zone changes.
			bool changed = CurrentBar > 0 && sHotZone2[0] != sHotZone2[1];
			PlotBrushes[0][0] = changed ? pbHotClear : pbHotOpaque;

			// Band distance — highest-of-highest ATR(200) over the recent 500-bar window-of-windows.
			// This is a "sticky max" that keeps the band wide even when current volatility cools off.
			double dist = maxAtrOuter[0];
			sHotZoneUp[0] = sHotZone2[0] + dist;
			sHotZoneDn[0] = sHotZone2[0] - dist;
		}

		#endregion

		#region SharpDX deviation HUD

		public override void OnRenderTargetChanged()
		{
			try
			{
				ReleaseRenderResources();
				if (RenderTarget == null) return;

				dxFrame    = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color4(0.5f, 0.5f, 0.5f, 0.6f));
				dxText     = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color4(0.95f, 0.95f, 0.95f, 1f));
				dxHudBg    = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color4(0.1f, 0.1f, 0.13f, 0.85f));
				var hzScb  = HotZoneColor as SolidColorBrush;
				System.Windows.Media.Color hc = hzScb != null ? hzScb.Color : Colors.Red;
				dxHotCenter = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
					new SharpDX.Color4(hc.R / 255f, hc.G / 255f, hc.B / 255f, 0.6f));

				// Lookback overlay color — orange by default, drawn over the last `Lookback` bars of the hot line.
				var lbScb  = LookbackColor as SolidColorBrush;
				System.Windows.Media.Color lc = lbScb != null ? lbScb.Color : Colors.Orange;
				dxLookback = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
					new SharpDX.Color4(lc.R / 255f, lc.G / 255f, lc.B / 255f, 1f));

				// Gradient stops — solid at band edge fading to fully transparent at the hot line.
				var solidEdge = new SharpDX.Color4(hc.R / 255f, hc.G / 255f, hc.B / 255f, 0.50f);
				var clearLine = new SharpDX.Color4(hc.R / 255f, hc.G / 255f, hc.B / 255f, 0.00f);
				gsUpperBand = new SharpDX.Direct2D1.GradientStopCollection(RenderTarget, new[]
				{
					new SharpDX.Direct2D1.GradientStop { Position = 0f, Color = solidEdge },	// top  = upper band edge
					new SharpDX.Direct2D1.GradientStop { Position = 1f, Color = clearLine }	// bot  = hot line
				});
				gsLowerBand = new SharpDX.Direct2D1.GradientStopCollection(RenderTarget, new[]
				{
					new SharpDX.Direct2D1.GradientStop { Position = 0f, Color = clearLine },	// top  = hot line
					new SharpDX.Direct2D1.GradientStop { Position = 1f, Color = solidEdge }	// bot  = lower band edge
				});
			}
			catch
			{
				// Suppress during chart teardown.
			}
		}

		private void ReleaseRenderResources()
		{
			void D(ref SharpDX.Direct2D1.SolidColorBrush bx) { if (bx != null) { bx.Dispose(); bx = null; } }
			D(ref dxFrame); D(ref dxText); D(ref dxHotCenter); D(ref dxLookback); D(ref dxHudBg);
			if (gsUpperBand != null) { gsUpperBand.Dispose(); gsUpperBand = null; }
			if (gsLowerBand != null) { gsLowerBand.Dispose(); gsLowerBand = null; }
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

			if (RenderTarget == null)	return;
			if (ChartBars == null)		return;
			if (!IsVisible)				return;
			if (IsInHitTest)			return;
			if (CurrentBar < Lookback)	return;
			if (sHotZone2 == null)		return;

			EnsureTextFormats();
			if (tfBody == null) return;

			// Zone band — gradient fill from hotZone2 fading to hotZoneUp / hotZoneDn edges, plus the
			// orange LookBack-Hot-Line overlay on the most recent `Lookback` bars.
			if (ShowHotZone && gsUpperBand != null && gsLowerBand != null)
				RenderZoneBand(chartControl, chartScale);

			double atrV = atr200[0];
			if (atrV <= 0) return;

			double dev = (Close[0] - sHotZone2[0]) / atrV;
			double devClamped = Math.Max(-4, Math.Min(4, dev));

			int cells = 21;
			int center = cells / 2;
			double scale = center / 4.0;
			int devIndex = center + (int)Math.Round(devClamped * scale);

			float fontPx = FontPx();
			float padX = 8f;
			float cellW = fontPx * 1.5f;
			float cellH = fontPx * 1.3f;
			float w = cellW * cells + padX * 2;
			float h = cellH * 2 + padX * 2 + 16f;	// top header row + cells row

			float x = ChartPanel.X + (ChartPanel.W - w) / 2f;	// bottom-center
			float y = ChartPanel.Y + ChartPanel.H - h - padX;

			RenderTarget.FillRectangle(new SharpDX.RectangleF(x, y, w, h), dxHudBg);

			// Header — "Hot Zone" + total profile volume.
			double profileTotal = 0;
			for (int i = 0; i < Rows; i++) profileTotal += binVolumes[i];
			string header = "Hot Zone: " + FormatVolume(profileTotal);
			using (var layout = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, header, tfBody, w - padX * 2, 14f))
			{
				layout.TextAlignment = SharpDX.DirectWrite.TextAlignment.Center;
				RenderTarget.DrawTextLayout(new SharpDX.Vector2(x + padX, y + 4f), layout, dxText, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
			}

			// 21 deviation cells — close-vs-hotZone in ATR units.
			float cellsRowY = y + 22f;
			for (int col = 0; col < cells; col++)
			{
				float cx = x + padX + col * cellW;
				int distFromCenter = Math.Abs(col - center);
				float intensity = (float)distFromCenter / center;
				bool active = col == devIndex;

				SharpDX.Direct2D1.SolidColorBrush cellBrush;
				if (col == center)
					cellBrush = dxHotCenter;
				else
				{
					var lcScb = (col - center < 0 ? LookbackColor : HotZoneColor) as SolidColorBrush;
					var c     = lcScb != null ? lcScb.Color : Colors.Gray;
					cellBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
						new SharpDX.Color4(c.R / 255f, c.G / 255f, c.B / 255f, intensity * 0.8f));
				}

				RenderTarget.FillRectangle(new SharpDX.RectangleF(cx, cellsRowY, cellW - 1f, cellH), cellBrush);
				RenderTarget.DrawRectangle(new SharpDX.RectangleF(cx, cellsRowY, cellW - 1f, cellH), dxFrame, 0.5f);

				if (active)
				{
					using (var layout = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, "◆", tfBody, cellW, cellH))
					{
						layout.TextAlignment = SharpDX.DirectWrite.TextAlignment.Center;
						RenderTarget.DrawTextLayout(new SharpDX.Vector2(cx, cellsRowY), layout, dxText, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
					}
				}

				if (col != center && cellBrush != dxHotCenter) cellBrush.Dispose();
			}
		}

		// Renders the two-half gradient zone band (fading from edge to hot line) plus the orange
		// LookBack-Hot-Line overlay on the most recent `Lookback` bars.
		private void RenderZoneBand(ChartControl chartControl, ChartScale chartScale)
		{
			int fromIdx = Math.Max(ChartBars.FromIndex, 0);
			int toIdx   = Math.Min(ChartBars.ToIndex,   CurrentBar);
			if (toIdx <= fromIdx) return;

			int lookbackStart = CurrentBar - Lookback;	// bars >= this index get the lookback color overlay

			for (int j = fromIdx; j < toIdx; j++)
			{
				if (!sHotZone2.IsValidDataPointAt(j) || !sHotZone2.IsValidDataPointAt(j + 1)) continue;
				if (!sHotZoneUp.IsValidDataPointAt(j) || !sHotZoneUp.IsValidDataPointAt(j + 1)) continue;
				if (!sHotZoneDn.IsValidDataPointAt(j) || !sHotZoneDn.IsValidDataPointAt(j + 1)) continue;

				double mJ  = sHotZone2.GetValueAt(j);
				double mJ1 = sHotZone2.GetValueAt(j + 1);
				double upJ  = sHotZoneUp.GetValueAt(j);
				double upJ1 = sHotZoneUp.GetValueAt(j + 1);
				double dnJ  = sHotZoneDn.GetValueAt(j);
				double dnJ1 = sHotZoneDn.GetValueAt(j + 1);
				if (mJ <= 0 || mJ1 <= 0) continue;

				DrawGradientTrapezoid(chartControl, chartScale, j, upJ,  upJ1, mJ,  mJ1, gsUpperBand);
				DrawGradientTrapezoid(chartControl, chartScale, j, mJ,   mJ1,  dnJ, dnJ1, gsLowerBand);

				// LookBack hot-line overlay — thick line on the most recent Lookback bars.
				if (j >= lookbackStart && dxLookback != null)
				{
					float xL = chartControl.GetXByBarIndex(ChartBars, j);
					float xR = chartControl.GetXByBarIndex(ChartBars, j + 1);
					float yL = (float)chartScale.GetYByValue(mJ);
					float yR = (float)chartScale.GetYByValue(mJ1);
					RenderTarget.DrawLine(new SharpDX.Vector2(xL, yL), new SharpDX.Vector2(xR, yR), dxLookback, 6f);
				}
			}
		}

		private void DrawGradientTrapezoid(ChartControl chartControl, ChartScale chartScale, int barLeftIdx,
			double topPriceJ, double topPriceJ1, double botPriceJ, double botPriceJ1,
			SharpDX.Direct2D1.GradientStopCollection stops)
		{
			if (stops == null) return;
			float xL = chartControl.GetXByBarIndex(ChartBars, barLeftIdx);
			float xR = chartControl.GetXByBarIndex(ChartBars, barLeftIdx + 1);

			float yTopL = (float)chartScale.GetYByValue(topPriceJ);
			float yTopR = (float)chartScale.GetYByValue(topPriceJ1);
			float yBotL = (float)chartScale.GetYByValue(botPriceJ);
			float yBotR = (float)chartScale.GetYByValue(botPriceJ1);

			if (Math.Abs(yTopL - yBotL) < 0.5f && Math.Abs(yTopR - yBotR) < 0.5f) return;

			// Anchor the gradient to this bar's vertical extent (top of trapezoid → bottom of trapezoid).
			float yTopAvg = (yTopL + yTopR) / 2f;
			float yBotAvg = (yBotL + yBotR) / 2f;
			float xMid    = (xL + xR) / 2f;

			SharpDX.Direct2D1.LinearGradientBrush brush = null;
			SharpDX.Direct2D1.PathGeometry geom = null;
			SharpDX.Direct2D1.GeometrySink sink = null;
			try
			{
				brush = new SharpDX.Direct2D1.LinearGradientBrush(RenderTarget,
					new SharpDX.Direct2D1.LinearGradientBrushProperties
					{
						StartPoint = new SharpDX.Vector2(xMid, yTopAvg),
						EndPoint   = new SharpDX.Vector2(xMid, yBotAvg)
					},
					stops);

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
				if (sink  != null) sink.Dispose();
				if (geom  != null) geom.Dispose();
				if (brush != null) brush.Dispose();
			}
		}

		#endregion

		#region Helpers

		private float FontPx()
		{
			switch (LabelTextSize)
			{
				case VolumeWhaleZones_TextSize.Tiny:   return 9f;
				case VolumeWhaleZones_TextSize.Small:  return 11f;
				case VolumeWhaleZones_TextSize.Normal: return 13f;
				case VolumeWhaleZones_TextSize.Large:  return 16f;
			}
			return 11f;
		}

		private static string FormatVolume(double v)
		{
			if (v >= 1e9) return (v / 1e9).ToString("0.0") + "B";
			if (v >= 1e6) return (v / 1e6).ToString("0.0") + "M";
			if (v >= 1e3) return (v / 1e3).ToString("0.0") + "K";
			return v.ToString("0");
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
		private indTradingView.VolumeWhaleZones[] cacheVolumeWhaleZones;
		public indTradingView.VolumeWhaleZones VolumeWhaleZones(int lookback, bool showHotZone, VolumeWhaleZones_TextSize labelTextSize)
		{
			return VolumeWhaleZones(Input, lookback, showHotZone, labelTextSize);
		}

		public indTradingView.VolumeWhaleZones VolumeWhaleZones(ISeries<double> input, int lookback, bool showHotZone, VolumeWhaleZones_TextSize labelTextSize)
		{
			if (cacheVolumeWhaleZones != null)
				for (int idx = 0; idx < cacheVolumeWhaleZones.Length; idx++)
					if (cacheVolumeWhaleZones[idx] != null && cacheVolumeWhaleZones[idx].Lookback == lookback && cacheVolumeWhaleZones[idx].ShowHotZone == showHotZone && cacheVolumeWhaleZones[idx].LabelTextSize == labelTextSize && cacheVolumeWhaleZones[idx].EqualsInput(input))
						return cacheVolumeWhaleZones[idx];
			return CacheIndicator<indTradingView.VolumeWhaleZones>(new indTradingView.VolumeWhaleZones(){ Lookback = lookback, ShowHotZone = showHotZone, LabelTextSize = labelTextSize }, input, ref cacheVolumeWhaleZones);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.VolumeWhaleZones VolumeWhaleZones(int lookback, bool showHotZone, VolumeWhaleZones_TextSize labelTextSize)
		{
			return indicator.VolumeWhaleZones(Input, lookback, showHotZone, labelTextSize);
		}

		public Indicators.indTradingView.VolumeWhaleZones VolumeWhaleZones(ISeries<double> input , int lookback, bool showHotZone, VolumeWhaleZones_TextSize labelTextSize)
		{
			return indicator.VolumeWhaleZones(input, lookback, showHotZone, labelTextSize);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.VolumeWhaleZones VolumeWhaleZones(int lookback, bool showHotZone, VolumeWhaleZones_TextSize labelTextSize)
		{
			return indicator.VolumeWhaleZones(Input, lookback, showHotZone, labelTextSize);
		}

		public Indicators.indTradingView.VolumeWhaleZones VolumeWhaleZones(ISeries<double> input , int lookback, bool showHotZone, VolumeWhaleZones_TextSize labelTextSize)
		{
			return indicator.VolumeWhaleZones(input, lookback, showHotZone, labelTextSize);
		}
	}
}

#endregion
