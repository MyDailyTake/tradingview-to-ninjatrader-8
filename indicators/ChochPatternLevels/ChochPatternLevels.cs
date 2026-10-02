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

// NT8 Version of Choch Pattern Levels [BigBeluga]
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the
// Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License (CC BY-NC-SA 4.0).
// The original Pine Script™ code is by BigBeluga and can be found at: https://www.tradingview.com/script/WVwxrsY3-Choch-Pattern-Levels-BigBeluga/
// Adaptation for NinjaTrader by jack@mydailytake.com
// © 2026 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the CC BY-NC-SA 4.0 license. Full license details at https://creativecommons.org/licenses/by-nc-sa/4.0/
// The use of BigBeluga name or its adapted code in this work does not imply endorsement by the original authors.
//
// Notes:
//   Change-of-Character (CHOCH) structure-break levels:
//     • Pivot scanner — ta.pivothigh(L,L) / ta.pivotlow(L,L) tracks the most recent confirmed
//       pivot high (ph) and pivot low (pl).
//     • Bullish CHOCH — `crossover(high, ph)` while trend is down. Build a triangle from the
//       broken pivot high, the breakout bar's high, and the LOWEST low between them; draw a
//       horizontal line at that lowest low extending right.
//     • Bearish CHOCH — `crossunder(low, pl)` while trend is up. Mirror geometry from the broken
//       pivot low + breakout bar's low + the HIGHEST high between them.
//     • Per-zone delta volume label — sum of (close>open ? +volume : -volume) across the
//       breakout window, displayed above (bullish) or below (bearish) the triangle.
//     • Lifecycle — each zone's level extends right every bar; deleted when the candle BODY
//       crosses the level (current or previous bar), or when the active count exceeds Amount.
//
//   Documented scope decisions:
//     • Pine `polyline` triangle fill reproduced via SharpDX OnRender PathGeometry per zone.
//     • Pine `label.style_circle` reproduced via SharpDX FillEllipse at the three triangle vertices.
//     • Tag suffix `_<phBar>_<curBar>` keeps each zone uniquely identifiable across redraws.

#region Enums ChochPatternLevels

public enum ChochPatternLevels_TextSize
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
	[Gui.CategoryOrder("Parameters",	10100)]
	[Gui.CategoryOrder("Delta Volume",	10200)]
	[Gui.CategoryOrder("Colors",		10300)]
	#endregion

	public class ChochPatternLevels : Indicator
	{
		#region indInfo

		private string indName        = "Choch Pattern Levels [BigBeluga]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by BigBeluga can be found here: https://www.tradingview.com/script/WVwxrsY3-Choch-Pattern-Levels-BigBeluga/";

		#endregion

		#region Properties

		[NinjaScriptProperty]
		[Range(2, int.MaxValue)]
		[Display(Order = 1, GroupName = "Parameters", Name = "Length",
			Description = "Pivot left/right window. Higher = fewer / cleaner pivots, lower = more reactive.")]
		public int Length { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 2, GroupName = "Parameters", Name = "Amount of Patterns",
			Description = "Maximum number of active CHOCH zones held in memory. Oldest is dropped when this is exceeded.")]
		public int Amount { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 1, GroupName = "Delta Volume", Name = "Show Delta Volume",
			Description = "Render the cumulative delta volume label across each CHOCH window.")]
		public bool ShowDelta { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 2, GroupName = "Delta Volume", Name = "Delta Text Size",
			Description = "Font size for the delta volume label.")]
		public ChochPatternLevels_TextSize DeltaTextSize { get; set; }

		[XmlIgnore]
		[Display(Order = 1, GroupName = "Colors", Name = "ChoCh Up Color",
			Description = "Color for bullish CHOCH triangles + level lines.")]
		public Brush ChochUpColor { get; set; }
			[Browsable(false)]
			public string ChochUpColorSerialize
			{
				get { return Serialize.BrushToString(ChochUpColor); }
				set { ChochUpColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore]
		[Display(Order = 2, GroupName = "Colors", Name = "ChoCh Down Color",
			Description = "Color for bearish CHOCH triangles + level lines.")]
		public Brush ChochDnColor { get; set; }
			[Browsable(false)]
			public string ChochDnColorSerialize
			{
				get { return Serialize.BrushToString(ChochDnColor); }
				set { ChochDnColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		#endregion

		#region Variables

		private class Zone
		{
			public bool   IsBullish;
			public int    PhBar;     public double PhPrice;
			public int    CurBar;    public double CurPrice;
			public int    ExtBar;    public double ExtPrice;	// lowest low for bullish, highest high for bearish
			public double Level;
			public double Delta;
			public bool   ToDelete;
		}

		private List<Zone> activeZones = new List<Zone>();

		// Latest confirmed pivot trackers.
		private double phPrice = double.NaN;
		private int    phBar   = -1;
		private double plPrice = double.NaN;
		private int    plBar   = -1;
		private bool   trendUp = false;

		// SharpDX brushes.
		private SharpDX.Direct2D1.SolidColorBrush dxUpFill, dxUpLine, dxUpDot, dxUpLabel;
		private SharpDX.Direct2D1.SolidColorBrush dxDnFill, dxDnLine, dxDnDot, dxDnLabel;
		private SharpDX.Direct2D1.SolidColorBrush dxText;

		private SharpDX.DirectWrite.TextFormat tfBody;
		private ChochPatternLevels_TextSize lastTextSize = ChochPatternLevels_TextSize.Normal;
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

				Length			= 10;
				Amount			= 10;
				ShowDelta		= true;
				DeltaTextSize	= ChochPatternLevels_TextSize.Normal;
				ChochUpColor	= Brushes.Lime;
				ChochDnColor	= Brushes.Red;
				EnsureFrozen(ChochUpColor);
				EnsureFrozen(ChochDnColor);
			}
			else if (State == State.DataLoaded)
			{
				activeZones.Clear();
				phPrice = plPrice = double.NaN;
				phBar = plBar = -1;
				trendUp = false;
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
			if (CurrentBar < Length * 2 + 1) return;

			// 1) Pivot detection on bar `Length` ago.
			double? newPh = DetectPivotHigh(Length);
			double? newPl = DetectPivotLow(Length);
			if (newPh.HasValue) { phPrice = newPh.Value; phBar = CurrentBar - Length; }
			if (newPl.HasValue) { plPrice = newPl.Value; plBar = CurrentBar - Length; }

			// 2) Bullish CHOCH — high crosses above the most recent pivot high while trend is down.
			if (!double.IsNaN(phPrice) && !trendUp)
			{
				bool crossedUp = High[0] > phPrice && High[1] <= phPrice;
				if (crossedUp)
				{
					// Find lowest low between phBar and current bar.
					int barsBack = CurrentBar - phBar;
					double lowest = Low[0];
					int    lowestBar = CurrentBar;
					double delta  = 0.0;
					for (int i = 0; i <= barsBack && i < CurrentBar + 1; i++)
					{
						if (Low[i] < lowest) { lowest = Low[i]; lowestBar = CurrentBar - i; }
						delta += Close[i] > Open[i] ? Volume[i] : -Volume[i];
					}

					Zone z = new Zone
					{
						IsBullish = true,
						PhBar     = phBar,    PhPrice  = phPrice,
						CurBar    = CurrentBar, CurPrice = High[0],
						ExtBar    = lowestBar, ExtPrice = lowest,
						Level     = lowest,
						Delta     = delta
					};
					activeZones.Add(z);
					trendUp = true;
				}
			}

			// 3) Bearish CHOCH — low crosses below the most recent pivot low while trend is up.
			if (!double.IsNaN(plPrice) && trendUp)
			{
				bool crossedDn = Low[0] < plPrice && Low[1] >= plPrice;
				if (crossedDn)
				{
					int barsBack = CurrentBar - plBar;
					double highest = High[0];
					int    highestBar = CurrentBar;
					double delta  = 0.0;
					for (int i = 0; i <= barsBack && i < CurrentBar + 1; i++)
					{
						if (High[i] > highest) { highest = High[i]; highestBar = CurrentBar - i; }
						delta += Close[i] > Open[i] ? Volume[i] : -Volume[i];
					}

					Zone z = new Zone
					{
						IsBullish = false,
						PhBar     = plBar,    PhPrice  = plPrice,
						CurBar    = CurrentBar, CurPrice = Low[0],
						ExtBar    = highestBar, ExtPrice = highest,
						Level     = highest,
						Delta     = delta
					};
					activeZones.Add(z);
					trendUp = false;
				}
			}

			// 4) Mark zones for deletion when the candle BODY crosses the level (current or previous bar).
			double bodyHi  = Math.Max(Close[0], Open[0]);
			double bodyLo  = Math.Min(Close[0], Open[0]);
			double bodyHi1 = CurrentBar >= 1 ? Math.Max(Close[1], Open[1]) : bodyHi;
			double bodyLo1 = CurrentBar >= 1 ? Math.Min(Close[1], Open[1]) : bodyLo;

			for (int i = 0; i < activeZones.Count; i++)
			{
				Zone z = activeZones[i];
				bool crossedNow  = bodyHi  > z.Level && bodyLo  < z.Level;
				bool crossedPrev = bodyHi1 > z.Level && bodyLo1 < z.Level;
				if (crossedNow || crossedPrev) z.ToDelete = true;
			}
			activeZones.RemoveAll(z => z.ToDelete);

			// 5) Cap active zones at Amount — drop oldest first.
			while (activeZones.Count > Amount) activeZones.RemoveAt(0);
		}

		#endregion

		#region Pivot helpers

		private double? DetectPivotHigh(int len)
		{
			if (CurrentBar < 2 * len) return null;
			double cand = High[len];
			for (int i = 0; i < 2 * len + 1; i++)
			{
				if (i == len) continue;
				if (High[i] >= cand) return null;
			}
			return cand;
		}

		private double? DetectPivotLow(int len)
		{
			if (CurrentBar < 2 * len) return null;
			double cand = Low[len];
			for (int i = 0; i < 2 * len + 1; i++)
			{
				if (i == len) continue;
				if (Low[i] <= cand) return null;
			}
			return cand;
		}

		#endregion

		#region SharpDX render

		public override void OnRenderTargetChanged()
		{
			try
			{
				ReleaseRenderResources();
				if (RenderTarget == null) return;

				var upScb = ChochUpColor as SolidColorBrush;
				var dnScb = ChochDnColor as SolidColorBrush;
				Color upC = upScb != null ? upScb.Color : Colors.Lime;
				Color dnC = dnScb != null ? dnScb.Color : Colors.Red;

				dxUpFill  = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, ToColor4(upC, 0.15f));
				dxUpLine  = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, ToColor4(upC, 0.95f));
				dxUpDot   = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, ToColor4(upC, 0.70f));
				dxUpLabel = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, ToColor4(upC, 1.00f));

				dxDnFill  = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, ToColor4(dnC, 0.20f));
				dxDnLine  = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, ToColor4(dnC, 0.95f));
				dxDnDot   = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, ToColor4(dnC, 0.70f));
				dxDnLabel = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, ToColor4(dnC, 1.00f));

				dxText = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color4(0.95f, 0.95f, 0.95f, 1f));
			}
			catch
			{
				// Suppress during chart teardown.
			}
		}

		private void ReleaseRenderResources()
		{
			void D(ref SharpDX.Direct2D1.SolidColorBrush bx) { if (bx != null) { bx.Dispose(); bx = null; } }
			D(ref dxUpFill); D(ref dxUpLine); D(ref dxUpDot); D(ref dxUpLabel);
			D(ref dxDnFill); D(ref dxDnLine); D(ref dxDnDot); D(ref dxDnLabel);
			D(ref dxText);
		}

		private void EnsureTextFormats()
		{
			if (dashFormatsBuilt && lastTextSize == DeltaTextSize) return;
			DisposeTextFormats();
			tfBody = new SharpDX.DirectWrite.TextFormat(Core.Globals.DirectWriteFactory, "Arial",
				SharpDX.DirectWrite.FontWeight.Normal, SharpDX.DirectWrite.FontStyle.Normal, FontPx());
			lastTextSize = DeltaTextSize;
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
			if (activeZones == null || activeZones.Count == 0) return;

			EnsureTextFormats();

			float rightX = chartControl.GetXByBarIndex(ChartBars, Math.Min(ChartBars.ToIndex, CurrentBar) + 5);

			foreach (Zone z in activeZones)
			{
				var fill   = z.IsBullish ? dxUpFill  : dxDnFill;
				var line   = z.IsBullish ? dxUpLine  : dxDnLine;
				var dot    = z.IsBullish ? dxUpDot   : dxDnDot;

				float xPh   = chartControl.GetXByBarIndex(ChartBars, z.PhBar);
				float yPh   = (float)chartScale.GetYByValue(z.PhPrice);
				float xCur  = chartControl.GetXByBarIndex(ChartBars, z.CurBar);
				float yCur  = (float)chartScale.GetYByValue(z.CurPrice);
				float xExt  = chartControl.GetXByBarIndex(ChartBars, z.ExtBar);
				float yExt  = (float)chartScale.GetYByValue(z.ExtPrice);
				float yLvl  = (float)chartScale.GetYByValue(z.Level);

				// 1) Triangle fill (PhBar→CurBar→ExtBar→close).
				DrawFilledTriangle(xPh, yPh, xCur, yCur, xExt, yExt, fill, line);

				// 2) Horizontal level line from ExtBar to right edge (CurBar + 5).
				RenderTarget.DrawLine(new SharpDX.Vector2(xExt, yLvl), new SharpDX.Vector2(rightX, yLvl), line, 3f);

				// 3) Circles at Ph, Cur, Ext.
				FillCircle(xPh,  yPh,  4f, dot);
				FillCircle(xCur, yCur, 4f, dot);
				FillCircle(xExt, yExt, 4f, dot);

				// 4) Open-circle marker at right edge of level line.
				if (tfBody != null)
				{
					using (var layout = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, "◯", tfBody, 24f, 24f))
					{
						layout.TextAlignment = SharpDX.DirectWrite.TextAlignment.Center;
						layout.ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center;
						RenderTarget.DrawTextLayout(new SharpDX.Vector2(rightX - 12f, yLvl - 12f), layout, line, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
					}

					// 5) Delta volume label (centered above bullish triangle, below bearish).
					if (ShowDelta)
					{
						string deltaTxt = (z.Delta > 0 ? "+" : "") + FormatVolume(z.Delta);
						float xMid = (xPh + xCur) / 2f;
						float yMid = z.IsBullish
							? (float)chartScale.GetYByValue((z.PhPrice + z.ExtPrice) / 2 + (z.PhPrice - z.ExtPrice) * 0.10)
							: (float)chartScale.GetYByValue((z.PhPrice + z.ExtPrice) / 2 - (z.ExtPrice - z.PhPrice) * 0.20);
						float lblW = 80f, lblH = 18f;
						using (var lay = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, deltaTxt, tfBody, lblW, lblH))
						{
							lay.TextAlignment = SharpDX.DirectWrite.TextAlignment.Center;
							var deltaBrush = z.Delta > 0 ? dxUpLabel : dxDnLabel;
							RenderTarget.DrawTextLayout(new SharpDX.Vector2(xMid - lblW / 2f, yMid - lblH / 2f), lay, deltaBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
						}
					}
				}
			}
		}

		private void DrawFilledTriangle(float x1, float y1, float x2, float y2, float x3, float y3,
			SharpDX.Direct2D1.Brush fill, SharpDX.Direct2D1.Brush stroke)
		{
			SharpDX.Direct2D1.PathGeometry geom = null;
			SharpDX.Direct2D1.GeometrySink  sink = null;
			try
			{
				geom = new SharpDX.Direct2D1.PathGeometry(Core.Globals.D2DFactory);
				sink = geom.Open();
				sink.SetFillMode(SharpDX.Direct2D1.FillMode.Winding);
				sink.BeginFigure(new SharpDX.Vector2(x1, y1), SharpDX.Direct2D1.FigureBegin.Filled);
				sink.AddLine(new SharpDX.Vector2(x2, y2));
				sink.AddLine(new SharpDX.Vector2(x3, y3));
				sink.EndFigure(SharpDX.Direct2D1.FigureEnd.Closed);
				sink.Close();
				RenderTarget.FillGeometry(geom, fill);
				RenderTarget.DrawGeometry(geom, stroke, 2f);
			}
			finally
			{
				if (sink != null) sink.Dispose();
				if (geom != null) geom.Dispose();
			}
		}

		private void FillCircle(float cx, float cy, float r, SharpDX.Direct2D1.Brush brush)
		{
			RenderTarget.FillEllipse(new SharpDX.Direct2D1.Ellipse(new SharpDX.Vector2(cx, cy), r, r), brush);
		}

		#endregion

		#region Helpers

		private float FontPx()
		{
			switch (DeltaTextSize)
			{
				case ChochPatternLevels_TextSize.Tiny:   return 9f;
				case ChochPatternLevels_TextSize.Small:  return 11f;
				case ChochPatternLevels_TextSize.Normal: return 13f;
				case ChochPatternLevels_TextSize.Large:  return 16f;
				case ChochPatternLevels_TextSize.Huge:   return 20f;
			}
			return 13f;
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
		private indTradingView.ChochPatternLevels[] cacheChochPatternLevels;
		public indTradingView.ChochPatternLevels ChochPatternLevels(int length, int amount, bool showDelta, ChochPatternLevels_TextSize deltaTextSize)
		{
			return ChochPatternLevels(Input, length, amount, showDelta, deltaTextSize);
		}

		public indTradingView.ChochPatternLevels ChochPatternLevels(ISeries<double> input, int length, int amount, bool showDelta, ChochPatternLevels_TextSize deltaTextSize)
		{
			if (cacheChochPatternLevels != null)
				for (int idx = 0; idx < cacheChochPatternLevels.Length; idx++)
					if (cacheChochPatternLevels[idx] != null && cacheChochPatternLevels[idx].Length == length && cacheChochPatternLevels[idx].Amount == amount && cacheChochPatternLevels[idx].ShowDelta == showDelta && cacheChochPatternLevels[idx].DeltaTextSize == deltaTextSize && cacheChochPatternLevels[idx].EqualsInput(input))
						return cacheChochPatternLevels[idx];
			return CacheIndicator<indTradingView.ChochPatternLevels>(new indTradingView.ChochPatternLevels(){ Length = length, Amount = amount, ShowDelta = showDelta, DeltaTextSize = deltaTextSize }, input, ref cacheChochPatternLevels);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.ChochPatternLevels ChochPatternLevels(int length, int amount, bool showDelta, ChochPatternLevels_TextSize deltaTextSize)
		{
			return indicator.ChochPatternLevels(Input, length, amount, showDelta, deltaTextSize);
		}

		public Indicators.indTradingView.ChochPatternLevels ChochPatternLevels(ISeries<double> input , int length, int amount, bool showDelta, ChochPatternLevels_TextSize deltaTextSize)
		{
			return indicator.ChochPatternLevels(input, length, amount, showDelta, deltaTextSize);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.ChochPatternLevels ChochPatternLevels(int length, int amount, bool showDelta, ChochPatternLevels_TextSize deltaTextSize)
		{
			return indicator.ChochPatternLevels(Input, length, amount, showDelta, deltaTextSize);
		}

		public Indicators.indTradingView.ChochPatternLevels ChochPatternLevels(ISeries<double> input , int length, int amount, bool showDelta, ChochPatternLevels_TextSize deltaTextSize)
		{
			return indicator.ChochPatternLevels(input, length, amount, showDelta, deltaTextSize);
		}
	}
}

#endregion
