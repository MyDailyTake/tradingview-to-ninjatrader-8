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

// NT8 Version of Money Flow Divergence Zones
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the Mozilla Public License 2.0 (MPL 2.0).
// The original Pine Script™ code is by AlgoAlpha and can be found at: https://www.tradingview.com/script/uAu2PvQM-Money-Flow-Divergence-Zones-AlgoAlpha/
// Adaptation for NinjaTrader by jack@mydailytake.com
// © 2026 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the Mozilla Public License 2.0. Full license details at https://mozilla.org/MPL/2.0/
// The use of AlgoAlpha name or its adapted code in this work does not imply endorsement by the original authors.
//
// Notes:
//   MFI(close, Len) smoothed by HMA(21) becomes the divergence oscillator. Pivot lows / highs on the
//   oscillator are paired with corresponding price extremes; a regular bullish divergence fires when
//   price prints a lower low while the oscillator prints a higher low (and the previous pivot is
//   between 5 and 60 bars away). Mirrored bearish logic.
//
//   Each divergence creates a price-panel "liquidity zone": a `Draw.Rectangle` between the recent
//   wick extreme (edge) and body extreme (base). Zones are removed when:
//     • body or wick (per Sweep Type) crosses the zone edge — optionally requiring two consecutive
//       confirms (Allow Rejection mode), OR
//     • the zone exceeds Max Zone Age bars.
//
//   Markers:
//     • ▲ / ▼ price-panel labels at the divergence pivot bar
//     • ▲ / ▼ zone-touch chars when a candle intersects an active zone with a confirming body
//
//   Documented deviations from source:
//     • Decorative oscillator midline column (style=columns, value=51, histbase=49) is omitted —
//       pure visual sugar with no signal value.
//     • Sub-panel divergence labels are omitted — the oscillator pivot lines + price-panel labels
//       carry the same information without doubling the marker count.
//     • The source's 9 alertconditions are not carried over — set alerts on the public Series
//       outputs below using NinjaTrader's own Alerts window.
//
//   Non-repainting: a divergence is confirmed LookbackRight bars after the actual oscillator pivot.
//   Public Series: BullSignal, BearSignal, BullMitigated, BearMitigated.

#region Enums MoneyFlowDivergenceZones

public enum MoneyFlowDivergenceZones_Mitigate
{
	Body,
	Wick
}

#endregion

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	#region Categories
	[Gui.CategoryOrder("Settings",		10100)]
	[Gui.CategoryOrder("Divergence",	10200)]
	[Gui.CategoryOrder("Management",	10300)]
	[Gui.CategoryOrder("Appearance",	10400)]
	#endregion

	public class MoneyFlowDivergenceZones : Indicator
	{
		#region indInfo

		private string indName        = "Money Flow Divergence Zones [AlgoAlpha]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by AlgoAlpha can be found here: https://www.tradingview.com/script/uAu2PvQM-Money-Flow-Divergence-Zones-AlgoAlpha/";

		#endregion

		#region Properties

		[NinjaScriptProperty]
		[Range(2, int.MaxValue)]
		[Display(Order = 1, GroupName = "Settings", Name = "MFI Length",
			Description = "Lookback for the underlying Money Flow Index.")]
		public int Len { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 1, GroupName = "Divergence", Name = "Pivot Lookback Left",
			Description = "Bars to the left required to confirm an oscillator pivot.")]
		public int LookbackLeft { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 2, GroupName = "Divergence", Name = "Pivot Lookback Right",
			Description = "Bars to the right required to confirm an oscillator pivot.")]
		public int LookbackRight { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 3, GroupName = "Divergence", Name = "Calculate Divergence",
			Description = "Master toggle for divergence detection + zone creation.")]
		public bool CalculateDivergence { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 1, GroupName = "Management", Name = "Max Zone Age",
			Description = "Bars after which a zone auto-expires.")]
		public int ExpiryAge { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 2, GroupName = "Management", Name = "Sweep Type",
			Description = "Body = zone removed when close crosses; Wick = zone removed when high/low crosses.")]
		public MoneyFlowDivergenceZones_Mitigate Mitigate { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 3, GroupName = "Management", Name = "Allow Rejection",
			Description = "When using Body sweep, requires two consecutive closes beyond the zone to remove it.")]
		public bool AllowRejection { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 4, GroupName = "Management", Name = "Show Historical Zones",
			Description = "Keep mitigated / expired zones drawn (faded). Off = removed when mitigated.")]
		public bool ShowHistoricalZones { get; set; }

		[NinjaScriptProperty]
		[Range(6, 48)]
		[Display(Order = 1, GroupName = "Appearance", Name = "Label Font Size",
			Description = "Font size of the ▲ / ▼ divergence and zone-touch labels.")]
		public int LabelFontSize { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 2, GroupName = "Appearance", Name = "Show Divergence Lines",
			Description = "Connect consecutive divergent pivots with a line on the oscillator panel.")]
		public bool ShowDivergenceLines { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 3, GroupName = "Appearance", Name = "Show Dynamic Midline",
			Description = "Translucent band at the 50 midline that brightens as the oscillator deviates from 50.")]
		public bool ShowDynamicMidline { get; set; }

		[XmlIgnore]
		[Display(Order = 10, GroupName = "Appearance", Name = "Bullish Color",
			Description = "Bullish zones, oscillator-up, divergence labels.")]
		public Brush BullColor { get; set; }
			[Browsable(false)]
			public string BullColorSerialize
			{
				get { return Serialize.BrushToString(BullColor); }
				set { BullColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore]
		[Display(Order = 11, GroupName = "Appearance", Name = "Bearish Color",
			Description = "Bearish zones, oscillator-down, divergence labels.")]
		public Brush BearColor { get; set; }
			[Browsable(false)]
			public string BearColorSerialize
			{
				get { return Serialize.BrushToString(BearColor); }
				set { BearColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		#endregion

		#region Public outputs

		[Browsable(false)][XmlIgnore] public Series<double> Mfi             { get { return Values[0]; } }
		[Browsable(false)][XmlIgnore] public Series<double> SmoothedOsc     { get { return Values[1]; } }
		[Browsable(false)][XmlIgnore] public Series<double> SignalLine      { get { return Values[2]; } }
		[Browsable(false)][XmlIgnore] public Series<bool>   BullSignal      { get { Update(); return sBullSignal;     } }
		[Browsable(false)][XmlIgnore] public Series<bool>   BearSignal      { get { Update(); return sBearSignal;     } }
		[Browsable(false)][XmlIgnore] public Series<bool>   BullMitigated   { get { Update(); return sBullMitigated;  } }
		[Browsable(false)][XmlIgnore] public Series<bool>   BearMitigated   { get { Update(); return sBearMitigated;  } }

		#endregion

		#region Variables

		private MFI		mfiInd;
		private HMA		hmaInd;	// HMA over the MFI series

		private Series<double>	sMfi;
		// OnRender renders the dynamic midline using sSmoothed across the visible window — needs Infinite.
		private Series<double>	sSmoothed;

		// SharpDX — midline + divergence-line brushes (rebuilt on color change).
		private SharpDX.Direct2D1.SolidColorBrush dxMidlineBull;
		private SharpDX.Direct2D1.SolidColorBrush dxMidlineBear;
		private SharpDX.Direct2D1.SolidColorBrush dxLineBull;
		private SharpDX.Direct2D1.SolidColorBrush dxLineBear;

		// Tracking divergence-line connection pairs for sub-panel rendering (matches the price-panel `Draw.Line`s).
		private struct DivergenceLine
		{
			public int    PrevPivotBar;
			public int    CurrPivotBar;
			public double PrevOscValue;
			public double CurrOscValue;
			public bool   IsBull;
		}
		private List<DivergenceLine> divergenceLines;

		// Sized text format for divergence + zone-touch labels.
		private SharpDX.DirectWrite.TextFormat tfLabel;
		private int lastLabelSize = -1;

		private Series<bool>	sBullSignal;
		private Series<bool>	sBearSignal;
		private Series<bool>	sBullMitigated;
		private Series<bool>	sBearMitigated;

		// Pivot tracking (on the smoothed oscillator) — keep the last 2 pivots per side for divergence comparison.
		private struct OscPivot
		{
			public int    BarIdx;	// absolute CurrentBar where the pivot was confirmed
			public int    PivotBar;	// absolute bar of the actual pivot (BarIdx - LookbackRight)
			public double OscValue;
			public double PriceLow;	// low at pivot bar (for bull divergence)
			public double PriceHigh;	// high at pivot bar (for bear divergence)
		}
		private List<OscPivot>	pivotLows;
		private List<OscPivot>	pivotHighs;

		// Zones — each contains the bar range + edge / base prices.
		private struct ZoneInfo
		{
			public int    StartBar;
			public int    EndBar;	// dynamic — extends to current bar until mitigated / expired
			public double EdgeY;	// wick extreme
			public double BaseY;	// body extreme
			public bool   IsBull;
			public bool   Active;
			public string Tag;
		}
		private List<ZoneInfo>	bullZones;
		private List<ZoneInfo>	bearZones;

		// Touch state for the current bar.
		private bool		bullInFill;
		private bool		bearInFill;

		// SharpDX brushes — outline only (we use `Draw.Rectangle` 11-arg for the fill).
		// (No SharpDX needed for this conversion — `Draw.Rectangle` handles zones; `Draw.Text` for markers.)

		private const int ZoneExtremeLen = 5;

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
				DrawOnPricePanel			= true;	// Zones + divergence labels render on the price panel.
				DrawHorizontalGridLines		= true;
				DrawVerticalGridLines		= true;
				PaintPriceMarkers			= true;
				ScaleJustification			= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				IsSuspendedWhileInactive	= true;

				Len						= 14;
				LookbackLeft			= 14;
				LookbackRight			= 5;
				CalculateDivergence		= true;
				ExpiryAge				= 1000;
				Mitigate				= MoneyFlowDivergenceZones_Mitigate.Body;
				AllowRejection			= false;
				ShowHistoricalZones		= false;
				LabelFontSize			= 14;
				ShowDivergenceLines		= true;
				ShowDynamicMidline		= true;

				BullColor = new SolidColorBrush(Color.FromRgb(0x00, 0xff, 0xbb));
				BearColor = new SolidColorBrush(Color.FromRgb(0xff, 0x11, 0x00));
				EnsureFrozen(BullColor);
				EnsureFrozen(BearColor);

				// MFI plot — solid translucent gray (~30% opacity).
				AddPlot(new Stroke(new SolidColorBrush(Color.FromArgb(76, 192, 192, 192)), DashStyleHelper.Solid, 1f), PlotStyle.Line, "MFI");
				// Smoothed oscillator — DimGray default; per-bar color via PlotBrushes (up = bull, down = bear).
				AddPlot(new Stroke(Brushes.DimGray, DashStyleHelper.Solid, 1f), PlotStyle.Line, "Smoothed Oscillator");
				// Signal line (smoothed[1]) — translucent default.
				AddPlot(new Stroke(new SolidColorBrush(Color.FromArgb(60, 192, 192, 192)), DashStyleHelper.Solid, 1f), PlotStyle.Line, "Signal Line");

				AddLine(new Stroke(Brushes.Gray, DashStyleHelper.Dot, 1f), 50.0, "Mid");
				AddLine(new Stroke(new SolidColorBrush(Color.FromArgb(96, 192, 192, 192)), DashStyleHelper.Solid, 1f), 80.0, "Overbought");
				AddLine(new Stroke(new SolidColorBrush(Color.FromArgb(96, 192, 192, 192)), DashStyleHelper.Solid, 1f), 20.0, "Oversold");
			}
			else if (State == State.DataLoaded)
			{
				mfiInd = MFI(Len);
				sMfi      = new Series<double>(this);
				// OnRender reads this across the visible window for the dynamic midline — needs Infinite.
				sSmoothed = new Series<double>(this, MaximumBarsLookBack.Infinite);
				hmaInd    = HMA(sMfi, 21);

				sBullSignal    = new Series<bool>(this);
				sBearSignal    = new Series<bool>(this);
				sBullMitigated = new Series<bool>(this);
				sBearMitigated = new Series<bool>(this);

				pivotLows        = new List<OscPivot>();
				pivotHighs       = new List<OscPivot>();
				bullZones        = new List<ZoneInfo>();
				bearZones        = new List<ZoneInfo>();
				divergenceLines  = new List<DivergenceLine>();
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
			sBullSignal[0]    = false;
			sBearSignal[0]    = false;
			sBullMitigated[0] = false;
			sBearMitigated[0] = false;
			bullInFill        = false;
			bearInFill        = false;

			// Oscillator math.
			sMfi[0] = mfiInd[0];

			int needed = Math.Max(Len + 21, LookbackLeft + LookbackRight + 5);
			if (CurrentBar < needed) return;

			double smoothed = hmaInd[0];
			sSmoothed[0]    = smoothed;

			Values[0][0] = sMfi[0];
			Values[1][0] = smoothed;
			// Signal line is the smoothed oscillator one bar back.
			Values[2][0] = (CurrentBar > 0) ? sSmoothed[1] : smoothed;

			// Per-bar oscillator color — up vs prior smoothed.
			double prevSmoothed = (CurrentBar > 0) ? sSmoothed[1] : smoothed;
			Brush oscBrush = smoothed > prevSmoothed ? BullColor : BearColor;
			PlotBrushes[1][0] = oscBrush;

			// Pivot detection on the smoothed oscillator at LookbackRight bars ago.
			if (CalculateDivergence && CurrentBar >= LookbackLeft + LookbackRight)
			{
				DetectOscPivot(true);
				DetectOscPivot(false);

				bool bullCond = CheckBullDivergence();
				bool bearCond = CheckBearDivergence();
				sBullSignal[0] = bullCond;
				sBearSignal[0] = bearCond;

				if (bullCond) CreateZone(true);
				if (bearCond) CreateZone(false);

				if (bullCond)
				{
					int pivotBar = CurrentBar - LookbackRight;
					int barsAgo  = LookbackRight;
					DrawSizedText("mfdBull" + pivotBar, "▲", barsAgo, Low[barsAgo] - TickSize * 4, BullColor);

					if (ShowDivergenceLines && pivotLows.Count >= 2)
					{
						OscPivot curr = pivotLows[pivotLows.Count - 1];
						OscPivot prev = pivotLows[pivotLows.Count - 2];
						int currBarsAgo = CurrentBar - curr.PivotBar;
						int prevBarsAgo = CurrentBar - prev.PivotBar;
						// Price-panel line — connects the two pivot lows.
						Draw.Line(this, "mfdBullLn" + curr.PivotBar, false,
							prevBarsAgo, prev.PriceLow, currBarsAgo, curr.PriceLow,
							BullColor, DashStyleHelper.Solid, 2);
						// Sub-panel line — connects the same pivots' oscillator values (rendered via SharpDX).
						divergenceLines.Add(new DivergenceLine
						{
							PrevPivotBar  = prev.PivotBar,
							CurrPivotBar  = curr.PivotBar,
							PrevOscValue  = prev.OscValue,
							CurrOscValue  = curr.OscValue,
							IsBull        = true
						});
					}
				}
				if (bearCond)
				{
					int pivotBar = CurrentBar - LookbackRight;
					int barsAgo  = LookbackRight;
					DrawSizedText("mfdBear" + pivotBar, "▼", barsAgo, High[barsAgo] + TickSize * 4, BearColor);

					if (ShowDivergenceLines && pivotHighs.Count >= 2)
					{
						OscPivot curr = pivotHighs[pivotHighs.Count - 1];
						OscPivot prev = pivotHighs[pivotHighs.Count - 2];
						int currBarsAgo = CurrentBar - curr.PivotBar;
						int prevBarsAgo = CurrentBar - prev.PivotBar;
						Draw.Line(this, "mfdBearLn" + curr.PivotBar, false,
							prevBarsAgo, prev.PriceHigh, currBarsAgo, curr.PriceHigh,
							BearColor, DashStyleHelper.Solid, 2);
						divergenceLines.Add(new DivergenceLine
						{
							PrevPivotBar  = prev.PivotBar,
							CurrPivotBar  = curr.PivotBar,
							PrevOscValue  = prev.OscValue,
							CurrOscValue  = curr.OscValue,
							IsBull        = false
						});
					}
				}
			}

			// Manage existing zones — extend, mitigate, expire.
			ManageZones(true);
			ManageZones(false);

			// Touch markers — ▲ on a bullish-body bar inside a bull zone, ▼ for bearish.
			if (bullInFill && Close[0] > Open[0])
				DrawSizedText("mfdTchU" + CurrentBar, "▲", 0, Low[0] - TickSize * 4, BullColor);
			if (bearInFill && Close[0] < Open[0])
				DrawSizedText("mfdTchD" + CurrentBar, "▼", 0, High[0] + TickSize * 4, BearColor);
		}

		#endregion

		#region Pivot detection

		private void DetectOscPivot(bool isHigh)
		{
			// Pivot at index LookbackRight — must be the strict max / min of the centered window.
			int pivotIdx = LookbackRight;
			double pivotVal = sSmoothed[pivotIdx];

			// Right-side comparison (LookbackRight bars to the right are bars [0 .. LookbackRight-1]).
			for (int i = 0; i < LookbackRight; i++)
			{
				double cmp = sSmoothed[i];
				if (isHigh ? cmp >= pivotVal : cmp <= pivotVal) return;
			}
			// Left-side comparison (LookbackLeft bars to the left).
			for (int i = pivotIdx + 1; i <= pivotIdx + LookbackLeft; i++)
			{
				double cmp = sSmoothed[i];
				if (isHigh ? cmp >= pivotVal : cmp <= pivotVal) return;
			}

			OscPivot p = new OscPivot
			{
				BarIdx    = CurrentBar,
				PivotBar  = CurrentBar - pivotIdx,
				OscValue  = pivotVal,
				PriceLow  = Low[pivotIdx],
				PriceHigh = High[pivotIdx]
			};

			if (isHigh)
			{
				pivotHighs.Add(p);
				while (pivotHighs.Count > 8) pivotHighs.RemoveAt(0);	// keep last few
			}
			else
			{
				pivotLows.Add(p);
				while (pivotLows.Count > 8) pivotLows.RemoveAt(0);
			}
		}

		private bool CheckBullDivergence()
		{
			if (pivotLows.Count < 2) return false;
			OscPivot curr = pivotLows[pivotLows.Count - 1];
			OscPivot prev = pivotLows[pivotLows.Count - 2];

			// Only fire on the bar where the new pivot was just confirmed.
			if (curr.BarIdx != CurrentBar) return false;

			int gap = curr.PivotBar - prev.PivotBar;
			if (gap < 5 || gap > 60) return false;	// _inRange semantics

			// Regular bullish divergence — price prints lower low while oscillator prints higher low.
			return curr.PriceLow < prev.PriceLow && curr.OscValue > prev.OscValue;
		}

		private bool CheckBearDivergence()
		{
			if (pivotHighs.Count < 2) return false;
			OscPivot curr = pivotHighs[pivotHighs.Count - 1];
			OscPivot prev = pivotHighs[pivotHighs.Count - 2];

			if (curr.BarIdx != CurrentBar) return false;

			int gap = curr.PivotBar - prev.PivotBar;
			if (gap < 5 || gap > 60) return false;

			return curr.PriceHigh > prev.PriceHigh && curr.OscValue < prev.OscValue;
		}

		#endregion

		#region Zone creation + management

		private void CreateZone(bool isBull)
		{
			int barsAgo = LookbackRight;

			// Wick extreme (edge) — most extreme low / high over the last ZoneExtremeLen bars at the pivot.
			double edge = isBull ? Low[barsAgo] : High[barsAgo];
			// Body extreme (base) — most extreme min(open,close) / max(open,close) at the pivot.
			double bodyBase = isBull ? Math.Min(Open[barsAgo], Close[barsAgo]) : Math.Max(Open[barsAgo], Close[barsAgo]);

			for (int i = barsAgo + 1; i < barsAgo + ZoneExtremeLen && i < CurrentBar; i++)
			{
				if (isBull)
				{
					if (Low[i]  < edge)     edge     = Low[i];
					double bodyLow = Math.Min(Open[i], Close[i]);
					if (bodyLow < bodyBase) bodyBase = bodyLow;
				}
				else
				{
					if (High[i] > edge)     edge     = High[i];
					double bodyHigh = Math.Max(Open[i], Close[i]);
					if (bodyHigh > bodyBase) bodyBase = bodyHigh;
				}
			}

			int startBar = CurrentBar - LookbackRight;
			ZoneInfo zone = new ZoneInfo
			{
				StartBar = startBar,
				EndBar   = CurrentBar,
				EdgeY    = edge,
				BaseY    = bodyBase,
				IsBull   = isBull,
				Active   = true,
				Tag      = (isBull ? "mfdZBull" : "mfdZBear") + startBar
			};

			(isBull ? bullZones : bearZones).Add(zone);
			DrawZone(zone);
		}

		private void DrawZone(ZoneInfo zone)
		{
			Brush outline = zone.IsBull ? BullColor : BearColor;
			Brush areaB   = zone.IsBull ? BullColor : BearColor;

			int startBarsAgo = CurrentBar - zone.StartBar;
			int endBarsAgo   = CurrentBar - zone.EndBar;
			// Active zones use full opacity (~18); historical (mitigated/expired) zones fade to ~6.
			int areaOpacity = zone.Active ? 18 : 6;
			Draw.Rectangle(this, zone.Tag, false, startBarsAgo, zone.EdgeY, endBarsAgo, zone.BaseY, outline, areaB, areaOpacity);
		}

		private void ManageZones(bool isBull)
		{
			List<ZoneInfo> zones = isBull ? bullZones : bearZones;

			for (int i = zones.Count - 1; i >= 0; i--)
			{
				ZoneInfo z = zones[i];
				if (!z.Active) continue;

				int age = CurrentBar - z.StartBar;
				bool expired = age > ExpiryAge;

				double sweepPrice;
				double sweepPricePrev;
				if (Mitigate == MoneyFlowDivergenceZones_Mitigate.Body)
				{
					sweepPrice     = Close[0];
					sweepPricePrev = CurrentBar > 0 ? Close[1] : Close[0];
				}
				else
				{
					sweepPrice     = isBull ? Low[0] : High[0];
					sweepPricePrev = CurrentBar > 0 ? (isBull ? Low[1] : High[1]) : sweepPrice;
				}

				bool removeByMitigation;
				if (AllowRejection && Mitigate == MoneyFlowDivergenceZones_Mitigate.Body)
				{
					removeByMitigation = isBull
						? sweepPrice < z.EdgeY && sweepPricePrev < z.EdgeY
						: sweepPrice > z.EdgeY && sweepPricePrev > z.EdgeY;
				}
				else
				{
					removeByMitigation = isBull
						? sweepPrice < z.EdgeY
						: sweepPrice > z.EdgeY;
				}

				if (removeByMitigation || expired)
				{
					z.Active = false;
					zones[i] = z;
					if (isBull) sBullMitigated[0] = true; else sBearMitigated[0] = true;

					if (ShowHistoricalZones)
						DrawZone(z);	// faded re-draw — kept on chart for historical reference
					else
						RemoveDrawObject(z.Tag);
				}
				else
				{
					// Touch detection — current bar's range intersects the zone.
					double zoneTop = Math.Max(z.EdgeY, z.BaseY);
					double zoneBot = Math.Min(z.EdgeY, z.BaseY);
					if (High[0] >= zoneBot && Low[0] <= zoneTop)
					{
						if (isBull) bullInFill = true; else bearInFill = true;
					}

					// Extend the zone to current bar.
					z.EndBar = CurrentBar;
					zones[i] = z;
					DrawZone(z);
				}
			}
		}

		#endregion

		#region SharpDX — midline + label format

		public override void OnRenderTargetChanged()
		{
			try
			{
				ReleaseRenderResources();
				if (RenderTarget == null) return;

				// Midline brushes — semi-translucent at full strength; per-bar alpha modulated in OnRender.
				dxMidlineBull = MakeDXBrush(BullColor, 0.6f);
				dxMidlineBear = MakeDXBrush(BearColor, 0.6f);

				// Divergence-line brushes (sub-panel).
				dxLineBull = MakeDXBrush(BullColor, 1.0f);
				dxLineBear = MakeDXBrush(BearColor, 1.0f);

				BuildLabelTextFormat();
			}
			catch
			{
				// Suppress during chart teardown.
			}
		}

		private void BuildLabelTextFormat()
		{
			if (tfLabel != null) { tfLabel.Dispose(); tfLabel = null; }
			tfLabel = new SharpDX.DirectWrite.TextFormat(Core.Globals.DirectWriteFactory, "Arial",
				SharpDX.DirectWrite.FontWeight.Bold, SharpDX.DirectWrite.FontStyle.Normal, LabelFontSize)
			{
				TextAlignment      = SharpDX.DirectWrite.TextAlignment.Center,
				ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center
			};
			lastLabelSize = LabelFontSize;
		}

		private void ReleaseRenderResources()
		{
			if (dxMidlineBull != null) { dxMidlineBull.Dispose(); dxMidlineBull = null; }
			if (dxMidlineBear != null) { dxMidlineBear.Dispose(); dxMidlineBear = null; }
			if (dxLineBull    != null) { dxLineBull.Dispose();    dxLineBull    = null; }
			if (dxLineBear    != null) { dxLineBear.Dispose();    dxLineBear    = null; }
			if (tfLabel       != null) { tfLabel.Dispose();       tfLabel       = null; }
		}

		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			base.OnRender(chartControl, chartScale);

			if (RenderTarget == null)	return;
			if (ChartBars == null)		return;
			if (!IsVisible)				return;
			if (IsInHitTest)			return;
			if (sSmoothed == null)		return;

			int fromIdxAll = Math.Max(ChartBars.FromIndex, 0);
			int toIdxAll   = Math.Min(ChartBars.ToIndex,   CurrentBar);

			// Sub-panel divergence connection lines — match the price-panel `Draw.Line`s but at the oscillator's pivot values.
			if (ShowDivergenceLines && divergenceLines != null && divergenceLines.Count > 0)
			{
				foreach (DivergenceLine dl in divergenceLines)
				{
					if (dl.CurrPivotBar < fromIdxAll || dl.PrevPivotBar > toIdxAll) continue;

					float xPrev = chartControl.GetXByBarIndex(ChartBars, dl.PrevPivotBar);
					float xCurr = chartControl.GetXByBarIndex(ChartBars, dl.CurrPivotBar);
					float yPrev = (float)chartScale.GetYByValue(dl.PrevOscValue);
					float yCurr = (float)chartScale.GetYByValue(dl.CurrOscValue);

					var brush = dl.IsBull ? dxLineBull : dxLineBear;
					if (brush == null) continue;
					RenderTarget.DrawLine(new SharpDX.Vector2(xPrev, yPrev), new SharpDX.Vector2(xCurr, yCurr), brush, 2.0f);
				}
			}

			if (!ShowDynamicMidline) return;
			if (toIdxAll < fromIdxAll) return;

			float yAt49 = (float)chartScale.GetYByValue(49.0);
			float yAt51 = (float)chartScale.GetYByValue(51.0);
			float bandTop    = Math.Min(yAt49, yAt51);
			float bandBottom = Math.Max(yAt49, yAt51);
			float bandHeight = bandBottom - bandTop;
			if (bandHeight < 0.5f) return;

			// One thin rectangle per visible bar — alpha = scaled(|smoothed - 50|).
			SharpDX.Direct2D1.AntialiasMode prevAA = RenderTarget.AntialiasMode;
			RenderTarget.AntialiasMode = SharpDX.Direct2D1.AntialiasMode.Aliased;
			try
			{
			for (int j = fromIdxAll; j <= toIdxAll; j++)
			{
				if (!sSmoothed.IsValidDataPointAt(j)) continue;
				double sm = sSmoothed.GetValueAt(j);
				double dist = Math.Abs(sm - 50.0) * 2.0;
				float alpha = (float)Math.Max(0.0, Math.Min(0.9, dist)) / 0.9f;	// 0..1 scaling
				if (alpha < 0.05f) continue;

				float xL = chartControl.GetXByBarIndex(ChartBars, j);
				float xR = chartControl.GetXByBarIndex(ChartBars, Math.Min(j + 1, CurrentBar));
				if (xR <= xL) continue;

				SharpDX.Direct2D1.SolidColorBrush src = sm > 50.0 ? dxMidlineBull : dxMidlineBear;
				if (src == null) continue;

				SharpDX.Direct2D1.SolidColorBrush perBar = null;
				try
				{
					var c = src.Color;
					perBar = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
						new SharpDX.Color4(c.Red, c.Green, c.Blue, c.Alpha * alpha));
					RenderTarget.FillRectangle(new SharpDX.RectangleF(xL, bandTop, xR - xL, bandHeight), perBar);
				}
				finally
				{
					if (perBar != null) perBar.Dispose();
				}
			}
			}
			finally { RenderTarget.AntialiasMode = prevAA; }
		}

		#endregion

		#region Helpers

		private void DrawSizedText(string tag, string text, int barsAgo, double y, Brush brush)
		{
			SimpleFont sf = new SimpleFont("Arial", LabelFontSize);
			Draw.Text(this, tag, false, text, barsAgo, y, 0, brush, sf, TextAlignment.Center, (Brush)null, (Brush)null, 0);
		}

		private SharpDX.Direct2D1.SolidColorBrush MakeDXBrush(Brush wpf, float alpha)
		{
			if (RenderTarget == null) return null;

			var scb = wpf as SolidColorBrush;
			System.Windows.Media.Color c = scb != null ? scb.Color : Colors.Gray;
			float wpfA = scb != null ? (float)scb.Opacity : 1f;
			return new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
				new SharpDX.Color4(c.R / 255f, c.G / 255f, c.B / 255f,
					Math.Max(0f, Math.Min(1f, alpha * wpfA))));
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
		private indTradingView.MoneyFlowDivergenceZones[] cacheMoneyFlowDivergenceZones;
		public indTradingView.MoneyFlowDivergenceZones MoneyFlowDivergenceZones(int len, int lookbackLeft, int lookbackRight, bool calculateDivergence, int expiryAge, MoneyFlowDivergenceZones_Mitigate mitigate, bool allowRejection, bool showHistoricalZones, int labelFontSize, bool showDivergenceLines, bool showDynamicMidline)
		{
			return MoneyFlowDivergenceZones(Input, len, lookbackLeft, lookbackRight, calculateDivergence, expiryAge, mitigate, allowRejection, showHistoricalZones, labelFontSize, showDivergenceLines, showDynamicMidline);
		}

		public indTradingView.MoneyFlowDivergenceZones MoneyFlowDivergenceZones(ISeries<double> input, int len, int lookbackLeft, int lookbackRight, bool calculateDivergence, int expiryAge, MoneyFlowDivergenceZones_Mitigate mitigate, bool allowRejection, bool showHistoricalZones, int labelFontSize, bool showDivergenceLines, bool showDynamicMidline)
		{
			if (cacheMoneyFlowDivergenceZones != null)
				for (int idx = 0; idx < cacheMoneyFlowDivergenceZones.Length; idx++)
					if (cacheMoneyFlowDivergenceZones[idx] != null && cacheMoneyFlowDivergenceZones[idx].Len == len && cacheMoneyFlowDivergenceZones[idx].LookbackLeft == lookbackLeft && cacheMoneyFlowDivergenceZones[idx].LookbackRight == lookbackRight && cacheMoneyFlowDivergenceZones[idx].CalculateDivergence == calculateDivergence && cacheMoneyFlowDivergenceZones[idx].ExpiryAge == expiryAge && cacheMoneyFlowDivergenceZones[idx].Mitigate == mitigate && cacheMoneyFlowDivergenceZones[idx].AllowRejection == allowRejection && cacheMoneyFlowDivergenceZones[idx].ShowHistoricalZones == showHistoricalZones && cacheMoneyFlowDivergenceZones[idx].LabelFontSize == labelFontSize && cacheMoneyFlowDivergenceZones[idx].ShowDivergenceLines == showDivergenceLines && cacheMoneyFlowDivergenceZones[idx].ShowDynamicMidline == showDynamicMidline && cacheMoneyFlowDivergenceZones[idx].EqualsInput(input))
						return cacheMoneyFlowDivergenceZones[idx];
			return CacheIndicator<indTradingView.MoneyFlowDivergenceZones>(new indTradingView.MoneyFlowDivergenceZones(){ Len = len, LookbackLeft = lookbackLeft, LookbackRight = lookbackRight, CalculateDivergence = calculateDivergence, ExpiryAge = expiryAge, Mitigate = mitigate, AllowRejection = allowRejection, ShowHistoricalZones = showHistoricalZones, LabelFontSize = labelFontSize, ShowDivergenceLines = showDivergenceLines, ShowDynamicMidline = showDynamicMidline }, input, ref cacheMoneyFlowDivergenceZones);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.MoneyFlowDivergenceZones MoneyFlowDivergenceZones(int len, int lookbackLeft, int lookbackRight, bool calculateDivergence, int expiryAge, MoneyFlowDivergenceZones_Mitigate mitigate, bool allowRejection, bool showHistoricalZones, int labelFontSize, bool showDivergenceLines, bool showDynamicMidline)
		{
			return indicator.MoneyFlowDivergenceZones(Input, len, lookbackLeft, lookbackRight, calculateDivergence, expiryAge, mitigate, allowRejection, showHistoricalZones, labelFontSize, showDivergenceLines, showDynamicMidline);
		}

		public Indicators.indTradingView.MoneyFlowDivergenceZones MoneyFlowDivergenceZones(ISeries<double> input , int len, int lookbackLeft, int lookbackRight, bool calculateDivergence, int expiryAge, MoneyFlowDivergenceZones_Mitigate mitigate, bool allowRejection, bool showHistoricalZones, int labelFontSize, bool showDivergenceLines, bool showDynamicMidline)
		{
			return indicator.MoneyFlowDivergenceZones(input, len, lookbackLeft, lookbackRight, calculateDivergence, expiryAge, mitigate, allowRejection, showHistoricalZones, labelFontSize, showDivergenceLines, showDynamicMidline);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.MoneyFlowDivergenceZones MoneyFlowDivergenceZones(int len, int lookbackLeft, int lookbackRight, bool calculateDivergence, int expiryAge, MoneyFlowDivergenceZones_Mitigate mitigate, bool allowRejection, bool showHistoricalZones, int labelFontSize, bool showDivergenceLines, bool showDynamicMidline)
		{
			return indicator.MoneyFlowDivergenceZones(Input, len, lookbackLeft, lookbackRight, calculateDivergence, expiryAge, mitigate, allowRejection, showHistoricalZones, labelFontSize, showDivergenceLines, showDynamicMidline);
		}

		public Indicators.indTradingView.MoneyFlowDivergenceZones MoneyFlowDivergenceZones(ISeries<double> input , int len, int lookbackLeft, int lookbackRight, bool calculateDivergence, int expiryAge, MoneyFlowDivergenceZones_Mitigate mitigate, bool allowRejection, bool showHistoricalZones, int labelFontSize, bool showDivergenceLines, bool showDynamicMidline)
		{
			return indicator.MoneyFlowDivergenceZones(input, len, lookbackLeft, lookbackRight, calculateDivergence, expiryAge, mitigate, allowRejection, showHistoricalZones, labelFontSize, showDivergenceLines, showDynamicMidline);
		}
	}
}

#endregion
