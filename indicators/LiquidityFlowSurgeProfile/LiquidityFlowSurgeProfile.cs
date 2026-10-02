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

// NT8 Version of Liquidity Flow Surge Profile [ChartPrime]
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the Mozilla Public License 2.0 (MPL 2.0).
// The original Pine Script™ code is by ChartPrime and can be found at: https://www.tradingview.com/script/EQSY3xU0-Liquidity-Flow-Surge-Profile-ChartPrime/
// Adaptation for NinjaTrader by jack@mydailytake.com
// © 2026 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the Mozilla Public License 2.0. Full license details at https://mozilla.org/MPL/2.0/
// The use of ChartPrime name or its adapted code in this work does not imply endorsement by the original authors.
//
// Notes:
//   25-bin volume distribution rendered as a thermal heatmap on the price panel:
//     • Range = highest-high / lowest-low across the lookback (Short=150, Medium=350, Long=700 bars).
//     • Each bar's volume gets attributed to the bin its close falls into.
//     • Bin opacity = volume / max-bin-volume, with a sensitivity floor of 0.5 (low-vol bins fade out).
//     • The current bar's "near" bin is rendered transparent so the candle isn't covered by the heatmap.
//
//   Liquidation surge engine fires when the current bar's volume relative to its 500-bar high exceeds
//   0.4 (40% of recent peak):
//     • Bull bar (close > open) → "Short Liquidation" label above the high.
//     • Bear bar → "Long Liquidation" label below the low.
//     • Label size scales with relative-volume bucket; opacity scales with the same.
//
//   Documented scope decisions:
//     • Pine 25-plot fill stack reproduced via SharpDX OnRender FillRectangle (one rectangle per visible
//       bar per bin, color-coded by intensity).
//     • Pine "dashboard" 2×2 table rendered via SharpDX HUD top-right.

#region Enums LiquidityFlowSurgeProfile

public enum LiquidityFlowSurgeProfile_Depth
{
	Short,
	Medium,
	Long
}

public enum LiquidityFlowSurgeProfile_TextSize
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
	[Gui.CategoryOrder("Profile",			10100)]
	[Gui.CategoryOrder("Liquidation Bubbles",10200)]
	[Gui.CategoryOrder("Dashboard",			10300)]
	#endregion

	public class LiquidityFlowSurgeProfile : Indicator
	{
		#region indInfo

		private string indName        = "Liquidity Flow Surge Profile [ChartPrime]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by ChartPrime can be found here: https://www.tradingview.com/script/EQSY3xU0-Liquidity-Flow-Surge-Profile-ChartPrime/";

		#endregion

		#region Properties

		[NinjaScriptProperty]
		[Display(Order = 1, GroupName = "Profile", Name = "Profile Depth",
			Description = "Lookback used for the volume profile (Short=150, Medium=350, Long=700).")]
		public LiquidityFlowSurgeProfile_Depth ProfileDepth { get; set; }

		[XmlIgnore]
		[Display(Order = 2, GroupName = "Profile", Name = "Structural Liquidity Color",
			Description = "Base color for the profile bins (intensity scales with volume share).")]
		public Brush ProfileColor { get; set; }
			[Browsable(false)]
			public string ProfileColorSerialize
			{
				get { return Serialize.BrushToString(ProfileColor); }
				set { ProfileColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[NinjaScriptProperty]
		[Display(Order = 1, GroupName = "Liquidation Bubbles", Name = "Show Bubbles",
			Description = "Render volume-surge labels above / below candles.")]
		public bool ShowBubbles { get; set; }

		[XmlIgnore]
		[Display(Order = 2, GroupName = "Liquidation Bubbles", Name = "Short Liquidation Color",
			Description = "Color for surges on bull bars (above the high).")]
		public Brush UpperBubbleColor { get; set; }
			[Browsable(false)]
			public string UpperBubbleColorSerialize
			{
				get { return Serialize.BrushToString(UpperBubbleColor); }
				set { UpperBubbleColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore]
		[Display(Order = 3, GroupName = "Liquidation Bubbles", Name = "Long Liquidation Color",
			Description = "Color for surges on bear bars (below the low).")]
		public Brush LowerBubbleColor { get; set; }
			[Browsable(false)]
			public string LowerBubbleColorSerialize
			{
				get { return Serialize.BrushToString(LowerBubbleColor); }
				set { LowerBubbleColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[NinjaScriptProperty]
		[Display(Order = 1, GroupName = "Dashboard", Name = "Show Dashboard",
			Description = "Render the latest-liquidation HUD (top-right).")]
		public bool ShowDashboard { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 2, GroupName = "Dashboard", Name = "Label Text Size",
			Description = "Text size for the dashboard HUD (bubble labels scale separately based on relative volume).")]
		public LiquidityFlowSurgeProfile_TextSize LabelTextSize { get; set; }

		#endregion

		#region Variables

		private const int Bins = 25;
		private const double ThermalSensitivity = 0.5;

		private MAX maxV500;

		private double[] binVolumes = new double[Bins];
		private double[] binLevels  = new double[Bins];
		private double   priceHigh, priceLow, binHeight;
		private int      pocIndex;
		private double   maxVolFound;

		// Per-bar profile snapshots — required so the heatmap flows with history instead of
		// painting the latest bar's profile across the entire visible window.
		private Series<double>		sPriceLow;
		private Series<double>		sPriceHigh;
		private Series<double>[]	sBinIntensity;	// length = Bins; each holds binVolumes[i] / maxVolFound for that bar

		// Last liquidation tracker (for dashboard).
		private string	lastLiqType  = "None";
		private double	lastLiqVol   = 0.0;
		private bool	lastLiqWasShort = false;	// drives dashboard color (true = upper bubble color)

		// SharpDX brushes — built once per render-target lifecycle.
		private SharpDX.Direct2D1.SolidColorBrush dxBg, dxFg, dxText, dxFrame;
		// Quantized-alpha palette for the heatmap (21 buckets, alpha = idx * 0.05).
		// Pre-built so per-bar rendering doesn't allocate ~12.5k brushes per frame.
		private SharpDX.Direct2D1.SolidColorBrush[]	dxHeatBrushes;
		private System.Windows.Media.Color			cachedHeatColor;

		private SharpDX.DirectWrite.TextFormat tfBody;
		private LiquidityFlowSurgeProfile_TextSize lastTextSize = LiquidityFlowSurgeProfile_TextSize.Normal;
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
				DisplayInDataBox			= false;
				DrawOnPricePanel			= true;
				DrawHorizontalGridLines		= true;
				DrawVerticalGridLines		= true;
				PaintPriceMarkers			= false;
				ScaleJustification			= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				IsSuspendedWhileInactive	= true;

				ProfileDepth     = LiquidityFlowSurgeProfile_Depth.Long;
				ProfileColor     = new SolidColorBrush(Color.FromRgb(0x6B, 0x7B, 0x8F));
				ShowBubbles      = true;
				UpperBubbleColor = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0xBB));
				LowerBubbleColor = new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x55));
				ShowDashboard    = true;
				LabelTextSize    = LiquidityFlowSurgeProfile_TextSize.Normal;

				EnsureFrozen(ProfileColor);
				EnsureFrozen(UpperBubbleColor);
				EnsureFrozen(LowerBubbleColor);
			}
			else if (State == State.DataLoaded)
			{
				maxV500 = MAX(Volume, 500);

				sPriceLow     = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sPriceHigh    = new Series<double>(this, MaximumBarsLookBack.Infinite);
				sBinIntensity = new Series<double>[Bins];
				for (int i = 0; i < Bins; i++)
					sBinIntensity[i] = new Series<double>(this, MaximumBarsLookBack.Infinite);
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
			int lookback = ProfileDepth == LiquidityFlowSurgeProfile_Depth.Short ? 150 :
			               ProfileDepth == LiquidityFlowSurgeProfile_Depth.Medium ? 350 : 700;

			if (CurrentBar < lookback) return;

			// Range and bin levels.
			priceHigh = double.MinValue;
			priceLow  = double.MaxValue;
			for (int i = 0; i < lookback; i++)
			{
				if (High[i] > priceHigh) priceHigh = High[i];
				if (Low [i] < priceLow ) priceLow  = Low [i];
			}
			binHeight = (priceHigh - priceLow) / Bins;

			Array.Clear(binVolumes, 0, Bins);
			for (int i = 0; i < Bins; i++) binLevels[i] = priceLow + binHeight * i;

			for (int i = 0; i < lookback; i++)
			{
				int idx = (int)((Close[i] - priceLow) / binHeight);
				if (idx < 0) idx = 0;
				else if (idx >= Bins) idx = Bins - 1;
				binVolumes[idx] += Volume[i];
			}

			pocIndex    = 0;
			maxVolFound = 0;
			for (int i = 0; i < Bins; i++)
				if (binVolumes[i] > maxVolFound) { maxVolFound = binVolumes[i]; pocIndex = i; }

			// Persist per-bar profile snapshot so OnRender can walk visible bars and draw each
			// bar's slice (gives the heatmap historical depth instead of a single static overlay).
			sPriceLow [0] = priceLow;
			sPriceHigh[0] = priceHigh;
			double inv = maxVolFound > 0 ? 1.0 / maxVolFound : 0.0;
			for (int i = 0; i < Bins; i++)
				sBinIntensity[i][0] = binVolumes[i] * inv;

			// Liquidation bubble engine — relative volume vs. 500-bar high.
			double v500   = maxV500[0];
			double relVol = v500 > 0 ? Volume[0] / v500 : 0;
			bool   isSurge = ShowBubbles && relVol >= 0.4;

			if (isSurge)
			{
				bool priceIsBull = Close[0] > Open[0];
				double atrV = ATR(14)[0];
				double yLoc = priceIsBull ? High[0] + atrV * 0.3 : Low[0] - atrV * 0.3;
				Brush bubbleBrush = priceIsBull ? UpperBubbleColor : LowerBubbleColor;

				lastLiqType    = priceIsBull ? "Short Liquidation" : "Long Liquidation";
				lastLiqVol     = Volume[0];
				lastLiqWasShort = priceIsBull;

				int alpha = Math.Max(20, 100 - (int)(relVol * 100));
				int fontSize = relVol < 0.6 ? 10 : relVol < 0.7 ? 12 : relVol < 0.8 ? 14 : 18;
				Brush areaBrush = WithAlphaPct(bubbleBrush, alpha);

				Draw.Text(this, "lfsBubble" + CurrentBar, false, FormatVolume(Volume[0]), 0, yLoc, 0, Brushes.WhiteSmoke,
					new SimpleFont("Arial", fontSize), TextAlignment.Center, areaBrush, areaBrush, 100);
			}
		}

		#endregion

		#region SharpDX rendering

		public override void OnRenderTargetChanged()
		{
			try
			{
				ReleaseRenderResources();
				if (RenderTarget == null) return;

				dxBg    = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color4(0.10f, 0.10f, 0.13f, 0.85f));
				dxFg    = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color4(0.95f, 0.95f, 0.95f, 1f));
				dxText  = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color4(0.95f, 0.95f, 0.95f, 1f));
				dxFrame = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color4(0.6f, 0.6f, 0.6f, 0.6f));
			}
			catch
			{
				// Suppress during chart teardown.
			}
		}

		private void ReleaseRenderResources()
		{
			void D(ref SharpDX.Direct2D1.SolidColorBrush bx) { if (bx != null) { bx.Dispose(); bx = null; } }
			D(ref dxBg); D(ref dxFg); D(ref dxText); D(ref dxFrame);
			DisposeHeatBrushes();
		}

		// Build/refresh the 21-bucket heatmap palette. Rebuilds only when ProfileColor changes
		// or the render target was rebuilt — otherwise reused across all OnRender calls.
		private void EnsureHeatBrushes(System.Windows.Media.Color c)
		{
			if (dxHeatBrushes != null && cachedHeatColor == c) return;
			DisposeHeatBrushes();
			dxHeatBrushes = new SharpDX.Direct2D1.SolidColorBrush[21];
			for (int i = 0; i <= 20; i++)
			{
				float a = i * 0.05f;
				dxHeatBrushes[i] = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
					new SharpDX.Color4(c.R / 255f, c.G / 255f, c.B / 255f, a));
			}
			cachedHeatColor = c;
		}

		private void DisposeHeatBrushes()
		{
			if (dxHeatBrushes == null) return;
			for (int i = 0; i < dxHeatBrushes.Length; i++)
				if (dxHeatBrushes[i] != null) { dxHeatBrushes[i].Dispose(); dxHeatBrushes[i] = null; }
			dxHeatBrushes = null;
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
			if (binHeight <= 0)			return;
			if (CurrentBar <= 0)		return;

			// Per-bar thermal profile: walk visible bars, look up each bar's stored snapshot,
			// and draw bar-width slices. Pine's plot()/fill() stack renders per-bar implicitly;
			// this loop is the NT8 equivalent.
			var profileScb = ProfileColor as SolidColorBrush;
			Color baseC = profileScb != null ? profileScb.Color : Colors.Gray;
			EnsureHeatBrushes(baseC);
			if (dxHeatBrushes == null || sPriceLow == null) return;

			int fromIdx = Math.Max(ChartBars.FromIndex, 0);
			int toIdx   = Math.Min(ChartBars.ToIndex,   CurrentBar);

			SharpDX.Direct2D1.AntialiasMode prevAA = RenderTarget.AntialiasMode;
			RenderTarget.AntialiasMode = SharpDX.Direct2D1.AntialiasMode.Aliased;
			try
			{
			for (int j = fromIdx; j <= toIdx; j++)
			{
				if (!sPriceLow.IsValidDataPointAt(j) || !sPriceHigh.IsValidDataPointAt(j)) continue;
				double pLow  = sPriceLow.GetValueAt(j);
				double pHigh = sPriceHigh.GetValueAt(j);
				if (pHigh <= pLow) continue;
				double bH = (pHigh - pLow) / Bins;

				float xL   = chartControl.GetXByBarIndex(ChartBars, j);
				float xR   = chartControl.GetXByBarIndex(ChartBars, j + 1);
				float barW = xR - xL;
				if (barW <= 0)
				{
					// Last bar — extrapolate from prior bar spacing.
					if (j > fromIdx)
					{
						float xPrev = chartControl.GetXByBarIndex(ChartBars, j - 1);
						barW = xL - xPrev;
						xR   = xL + barW;
					}
					if (barW <= 0) continue;
				}

				for (int i = 0; i < Bins; i++)
				{
					if (!sBinIntensity[i].IsValidDataPointAt(j)) continue;
					double intensity = sBinIntensity[i].GetValueAt(j);
					if (intensity <= ThermalSensitivity) continue;	// fade out low-volume bins

					double bFloor = pLow + bH * i;
					double bCeil  = bFloor + bH;

					// Don't cover the live candle (latest bar only — historical bars render fully).
					if (j == CurrentBar)
					{
						bool isPriceNear = (Close[0] >= bFloor && Close[0] <= bCeil) || (Open[0] >= bFloor && Open[0] <= bCeil);
						if (isPriceNear) continue;
					}

					float alpha01 = (float)((intensity >= 1.0) ? 0.65 : (intensity - ThermalSensitivity) / (1.0 - ThermalSensitivity) * 0.55);
					int paletteIdx = Math.Max(0, Math.Min(20, (int)Math.Round(alpha01 * 20.0)));
					var brush = dxHeatBrushes[paletteIdx];
					if (brush == null) continue;

					float yTop = (float)chartScale.GetYByValue(bCeil);
					float yBot = (float)chartScale.GetYByValue(bFloor);
					RenderTarget.FillRectangle(new SharpDX.RectangleF(xL, yTop, barW, Math.Abs(yBot - yTop)), brush);
				}
			}
			}
			finally { RenderTarget.AntialiasMode = prevAA; }

			// Dashboard.
			if (!ShowDashboard) return;
			EnsureTextFormats();
			if (tfBody == null) return;

			float fontPx = FontPx();
			float padX = 10f, padY = 8f, lineH = fontPx * 1.5f;
			float labelW = fontPx * 11f, valueW = fontPx * 9f;
			float w = labelW + valueW + padX * 2;
			float h = lineH * 2 + padY * 2;

			float x = ChartPanel.X + ChartPanel.W - w - padX;
			float y = ChartPanel.Y + padY;
			RenderTarget.FillRectangle(new SharpDX.RectangleF(x, y, w, h), dxBg);

			SharpDX.Direct2D1.SolidColorBrush liqBrush;
			if (lastLiqType == "None")
				liqBrush = dxFg;
			else
			{
				var bs = (lastLiqWasShort ? UpperBubbleColor : LowerBubbleColor) as SolidColorBrush;
				Color c = bs != null ? bs.Color : Colors.White;
				liqBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
					new SharpDX.Color4(c.R / 255f, c.G / 255f, c.B / 255f, 1f));
			}

			float rowY = y + padY;
			DrawCell("Last Liquidation", dxFg,   x + padX,          rowY, labelW, lineH);
			DrawCell("Volume",            dxFg,   x + padX + labelW, rowY, valueW, lineH);

			rowY += lineH;
			DrawCell(lastLiqType,                       liqBrush, x + padX,          rowY, labelW, lineH);
			DrawCell(FormatVolume(lastLiqVol),          liqBrush, x + padX + labelW, rowY, valueW, lineH);

			if (lastLiqType != "None") liqBrush.Dispose();
		}

		private void DrawCell(string text, SharpDX.Direct2D1.SolidColorBrush brush, float x, float y, float w, float h)
		{
			if (tfBody == null || brush == null || string.IsNullOrEmpty(text)) return;
			using (var layout = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, text, tfBody, w, h))
				RenderTarget.DrawTextLayout(new SharpDX.Vector2(x, y), layout, brush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
		}

		#endregion

		#region Helpers

		private float FontPx()
		{
			switch (LabelTextSize)
			{
				case LiquidityFlowSurgeProfile_TextSize.Tiny:   return 10f;
				case LiquidityFlowSurgeProfile_TextSize.Small:  return 11f;
				case LiquidityFlowSurgeProfile_TextSize.Normal: return 12f;
				case LiquidityFlowSurgeProfile_TextSize.Large:  return 16f;
			}
			return 12f;
		}

		private static string FormatVolume(double v)
		{
			if (v >= 1e9) return (v / 1e9).ToString("0.0") + "B";
			if (v >= 1e6) return (v / 1e6).ToString("0.0") + "M";
			if (v >= 1e3) return (v / 1e3).ToString("0.0") + "K";
			return v.ToString("0");
		}

		// Convert a 0-100 transparency value (0 = opaque, 100 = invisible) to an ARGB byte alpha.
		private static Brush WithAlphaPct(Brush src, int transparency)
		{
			var scb = src as SolidColorBrush;
			Color c = scb != null ? scb.Color : Colors.Gray;
			byte a = (byte)Math.Max(0, Math.Min(255, (100 - transparency) * 255 / 100));
			var b = new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B));
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
		private indTradingView.LiquidityFlowSurgeProfile[] cacheLiquidityFlowSurgeProfile;
		public indTradingView.LiquidityFlowSurgeProfile LiquidityFlowSurgeProfile(LiquidityFlowSurgeProfile_Depth profileDepth, bool showBubbles, bool showDashboard, LiquidityFlowSurgeProfile_TextSize labelTextSize)
		{
			return LiquidityFlowSurgeProfile(Input, profileDepth, showBubbles, showDashboard, labelTextSize);
		}

		public indTradingView.LiquidityFlowSurgeProfile LiquidityFlowSurgeProfile(ISeries<double> input, LiquidityFlowSurgeProfile_Depth profileDepth, bool showBubbles, bool showDashboard, LiquidityFlowSurgeProfile_TextSize labelTextSize)
		{
			if (cacheLiquidityFlowSurgeProfile != null)
				for (int idx = 0; idx < cacheLiquidityFlowSurgeProfile.Length; idx++)
					if (cacheLiquidityFlowSurgeProfile[idx] != null && cacheLiquidityFlowSurgeProfile[idx].ProfileDepth == profileDepth && cacheLiquidityFlowSurgeProfile[idx].ShowBubbles == showBubbles && cacheLiquidityFlowSurgeProfile[idx].ShowDashboard == showDashboard && cacheLiquidityFlowSurgeProfile[idx].LabelTextSize == labelTextSize && cacheLiquidityFlowSurgeProfile[idx].EqualsInput(input))
						return cacheLiquidityFlowSurgeProfile[idx];
			return CacheIndicator<indTradingView.LiquidityFlowSurgeProfile>(new indTradingView.LiquidityFlowSurgeProfile(){ ProfileDepth = profileDepth, ShowBubbles = showBubbles, ShowDashboard = showDashboard, LabelTextSize = labelTextSize }, input, ref cacheLiquidityFlowSurgeProfile);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.LiquidityFlowSurgeProfile LiquidityFlowSurgeProfile(LiquidityFlowSurgeProfile_Depth profileDepth, bool showBubbles, bool showDashboard, LiquidityFlowSurgeProfile_TextSize labelTextSize)
		{
			return indicator.LiquidityFlowSurgeProfile(Input, profileDepth, showBubbles, showDashboard, labelTextSize);
		}

		public Indicators.indTradingView.LiquidityFlowSurgeProfile LiquidityFlowSurgeProfile(ISeries<double> input , LiquidityFlowSurgeProfile_Depth profileDepth, bool showBubbles, bool showDashboard, LiquidityFlowSurgeProfile_TextSize labelTextSize)
		{
			return indicator.LiquidityFlowSurgeProfile(input, profileDepth, showBubbles, showDashboard, labelTextSize);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.LiquidityFlowSurgeProfile LiquidityFlowSurgeProfile(LiquidityFlowSurgeProfile_Depth profileDepth, bool showBubbles, bool showDashboard, LiquidityFlowSurgeProfile_TextSize labelTextSize)
		{
			return indicator.LiquidityFlowSurgeProfile(Input, profileDepth, showBubbles, showDashboard, labelTextSize);
		}

		public Indicators.indTradingView.LiquidityFlowSurgeProfile LiquidityFlowSurgeProfile(ISeries<double> input , LiquidityFlowSurgeProfile_Depth profileDepth, bool showBubbles, bool showDashboard, LiquidityFlowSurgeProfile_TextSize labelTextSize)
		{
			return indicator.LiquidityFlowSurgeProfile(input, profileDepth, showBubbles, showDashboard, labelTextSize);
		}
	}
}

#endregion
