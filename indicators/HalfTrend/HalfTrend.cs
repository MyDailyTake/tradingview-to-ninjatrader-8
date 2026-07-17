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

// NT8 Version of HalfTrend
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the GNU General Public License v3.0 (GPL-3.0).
// Copyright (c) 2021-present, Alex Orekhov (everget).
// The original Pine Script™ code is by everget and can be found at: https://www.tradingview.com/script/U1SJ8ubc-HalfTrend/
// Adaptation for NinjaTrader by jack@mydailytake.com
// Write-up + downloads: https://mydailytake.com/halftrend-everget-ninjatrader-8/
// Source + all conversions: https://github.com/MyDailyTake/tradingview-to-ninjatrader-8
// © 2026 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the GNU General Public License v3.0. Full license details at https://www.gnu.org/licenses/gpl-3.0.html
// The use of everget name or its adapted code in this work does not imply endorsement by the original authors.
//
// Notes:
//   Trend follower that flips when SMA(high) crosses below stored max-low (down-flip) or SMA(low)
//   crosses above stored min-high (up-flip). The active line is bracketed by ATR-multiple channels
//   filled translucently above (sell side) and below (buy side).
//   ATR length defaults to 100; exposed as a property for tuning.
//   Non-repainting. Public Series outputs: HalfTrendLine, AtrHigh, AtrLow, BuySignal, SellSignal, TrendSeries.

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	#region Categories
	[Gui.CategoryOrder("Calculation",	10100)]
	[Gui.CategoryOrder("Visuals",		10200)]
	[Gui.CategoryOrder("Display",		10300)]
	#endregion

	public class HalfTrend : Indicator
	{
		#region indInfo

		private string indName        = "HalfTrend [everget]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by everget can be found here: https://www.tradingview.com/script/U1SJ8ubc-HalfTrend/";

		#endregion

		#region Properties

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 1, GroupName = "Calculation", Name = "Amplitude",
			Description = "Lookback for the SMA-of-High / SMA-of-Low and pivot detection.")]
		public int Amplitude { get; set; }

		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Order = 2, GroupName = "Calculation", Name = "Channel Deviation",
			Description = "ATR multiplier for the upper / lower channel offset from the HalfTrend line.")]
		public int ChannelDeviation { get; set; }

		[NinjaScriptProperty]
		[Range(2, int.MaxValue)]
		[Display(Order = 3, GroupName = "Calculation", Name = "ATR Period",
			Description = "Period for the channel-width ATR. Default 100.")]
		public int AtrPeriod { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 1, GroupName = "Visuals", Name = "Show Arrows",
			Description = "Triangle markers at trend flips.")]
		public bool ShowArrows { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 2, GroupName = "Visuals", Name = "Show Channels",
			Description = "ATR upper / lower channel lines and the translucent fills.")]
		public bool ShowChannels { get; set; }

		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Order = 3, GroupName = "Visuals", Name = "Channel Fill Opacity",
			Description = "Opacity (0–100) of the translucent channel fills. 0 = invisible, 100 = solid.")]
		public int ChannelOpacity { get; set; }

		[NinjaScriptProperty]
		[Display(Order = 4, GroupName = "Visuals", Name = "Show Buy/Sell Labels",
			Description = "'Buy' / 'Sell' text labels at trend flips.")]
		public bool ShowLabels { get; set; }

		[NinjaScriptProperty]
		[Range(6, 48)]
		[Display(Order = 5, GroupName = "Visuals", Name = "Label Font Size",
			Description = "Font size of the 'Buy' / 'Sell' labels.")]
		public int LabelFontSize { get; set; }

		[XmlIgnore]
		[Display(Order = 1, GroupName = "Display", Name = "Buy Color",
			Description = "HalfTrend line, lower channel, and buy markers when in an up-trend.")]
		public Brush BuyColor { get; set; }
			[Browsable(false)]
			public string BuyColorSerialize
			{
				get { return Serialize.BrushToString(BuyColor); }
				set { BuyColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore]
		[Display(Order = 2, GroupName = "Display", Name = "Sell Color",
			Description = "HalfTrend line, upper channel, and sell markers when in a down-trend.")]
		public Brush SellColor { get; set; }
			[Browsable(false)]
			public string SellColorSerialize
			{
				get { return Serialize.BrushToString(SellColor); }
				set { SellColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		#endregion

		#region Public outputs

		[Browsable(false)][XmlIgnore] public Series<double> HalfTrendLine { get { Update(); return sHalfTrendLine; } }
		[Browsable(false)][XmlIgnore] public Series<double> AtrHigh       { get { Update(); return sAtrHigh; } }
		[Browsable(false)][XmlIgnore] public Series<double> AtrLow        { get { Update(); return sAtrLow; } }
		[Browsable(false)][XmlIgnore] public Series<int>    TrendSeries   { get { Update(); return sTrend; } }
		[Browsable(false)][XmlIgnore] public Series<bool>   BuySignal     { get { Update(); return sBuySignal; } }
		[Browsable(false)][XmlIgnore] public Series<bool>   SellSignal    { get { Update(); return sSellSignal; } }

		#endregion

		#region Variables

		private ATR				atr;
		private MAX				maxHigh;
		private MIN				minLow;
		private SMA				highMa;
		private SMA				lowMa;

		// Persistent scalar state — values carry across bars.
		private int		trend;
		private int		nextTrend;
		private double	maxLowPrice;
		private double	minHighPrice;
		private double	up;
		private double	down;

		// Series — backing fields for OnRender + strategy consumption.
		private Series<double>	sHalfTrendLine;	// Infinite — OnRender visible-window read.
		private Series<double>	sAtrHigh;		// Infinite — OnRender visible-window read.
		private Series<double>	sAtrLow;		// Infinite — OnRender visible-window read.
		private Series<int>		sTrend;
		private Series<bool>	sBuySignal;
		private Series<bool>	sSellSignal;

		// SharpDX
		private SharpDX.Direct2D1.SolidColorBrush dxBuyFill;
		private SharpDX.Direct2D1.SolidColorBrush dxSellFill;
		private SharpDX.Direct2D1.SolidColorBrush dxBuyText;
		private SharpDX.Direct2D1.SolidColorBrush dxSellText;
		private SharpDX.DirectWrite.TextFormat tfLabel;
		private int lastLabelSize = -1;

		// Label bar tracking — keyed by absolute bar index, value = price anchor.
		private Dictionary<int, double> buyLabels;
		private Dictionary<int, double> sellLabels;

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

				Amplitude			= 2;
				ChannelDeviation	= 2;
				AtrPeriod			= 100;
				ShowArrows			= true;
				ShowChannels		= true;
				ChannelOpacity		= 15;
				ShowLabels			= true;
				LabelFontSize		= 14;

				BuyColor	= Brushes.DodgerBlue;
				SellColor	= Brushes.Red;

				// Stroke defaults DimGray — per-bar color is driven by BuyColor / SellColor via PlotBrushes.
				AddPlot(new Stroke(Brushes.DimGray, DashStyleHelper.Solid, 2f), PlotStyle.Line, "HalfTrend");
				AddPlot(new Stroke(Brushes.DimGray, DashStyleHelper.Solid, 2f), PlotStyle.Dot,  "ATR High");
				AddPlot(new Stroke(Brushes.DimGray, DashStyleHelper.Solid, 2f), PlotStyle.Dot,  "ATR Low");
			}
			else if (State == State.DataLoaded)
			{
				atr		= ATR(AtrPeriod);
				maxHigh	= MAX(High, Amplitude);
				minLow	= MIN(Low,  Amplitude);
				highMa	= SMA(High, Amplitude);
				lowMa	= SMA(Low,  Amplitude);

				sHalfTrendLine	= new Series<double>(this, MaximumBarsLookBack.Infinite);
				sAtrHigh		= new Series<double>(this, MaximumBarsLookBack.Infinite);
				sAtrLow			= new Series<double>(this, MaximumBarsLookBack.Infinite);
				// IsValidDataPoint(N) requires Infinite — sTrend is read with that check in OnBarUpdate.
				sTrend			= new Series<int>(this, MaximumBarsLookBack.Infinite);
				sBuySignal		= new Series<bool>(this);
				sSellSignal		= new Series<bool>(this);

				trend			= 0;
				nextTrend		= 0;
				up				= 0.0;
				down			= 0.0;
				maxLowPrice		= 0.0;
				minHighPrice	= 0.0;

				buyLabels	= new Dictionary<int, double>();
				sellLabels	= new Dictionary<int, double>();
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
			sBuySignal[0]  = false;
			sSellSignal[0] = false;

			int needed = Math.Max(AtrPeriod, Amplitude) + 2;
			if (CurrentBar < needed)
			{
				Values[0].Reset();
				Values[1].Reset();
				Values[2].Reset();
				sHalfTrendLine.Reset();
				sAtrHigh.Reset();
				sAtrLow.Reset();
				return;
			}

			// Seed the price-anchored state once warmup completes.
			if (CurrentBar == needed)
			{
				maxLowPrice  = Low[1];
				minHighPrice = High[1];
			}

			int    prevTrend = sTrend.IsValidDataPoint(1) ? sTrend[1] : 0;
			double prevUp    = up;
			double prevDown  = down;

			double atr2     = atr[0] / 2.0;
			double dev      = ChannelDeviation * atr2;
			double highPrice = maxHigh[0];
			double lowPrice  = minLow[0];
			double highmaVal = highMa[0];
			double lowmaVal  = lowMa[0];

			bool arrowUpFired   = false;
			bool arrowDownFired = false;

			if (nextTrend == 1)
			{
				maxLowPrice = Math.Max(lowPrice, maxLowPrice);
				if (highmaVal < maxLowPrice && Close[0] < Low[1])
				{
					trend        = 1;
					nextTrend    = 0;
					minHighPrice = highPrice;
				}
			}
			else
			{
				minHighPrice = Math.Min(highPrice, minHighPrice);
				if (lowmaVal > minHighPrice && Close[0] > High[1])
				{
					trend        = 0;
					nextTrend    = 1;
					maxLowPrice  = lowPrice;
				}
			}

			double atrHighVal, atrLowVal;
			if (trend == 0)
			{
				if (prevTrend != 0)
				{
					up            = prevDown;
					arrowUpFired  = true;
				}
				else
				{
					up = Math.Max(maxLowPrice, prevUp);
				}
				atrHighVal = up + dev;
				atrLowVal  = up - dev;
			}
			else
			{
				if (prevTrend != 1)
				{
					down            = prevUp;
					arrowDownFired  = true;
				}
				else
				{
					down = Math.Min(minHighPrice, prevDown);
				}
				atrHighVal = down + dev;
				atrLowVal  = down - dev;
			}

			double ht = trend == 0 ? up : down;

			sTrend[0]          = trend;
			sHalfTrendLine[0]  = ht;
			sAtrHigh[0]        = atrHighVal;
			sAtrLow[0]         = atrLowVal;

			Values[0][0]       = ht;
			PlotBrushes[0][0]  = trend == 0 ? BuyColor : SellColor;

			if (ShowChannels)
			{
				Values[1][0]      = atrHighVal;
				Values[2][0]      = atrLowVal;
				PlotBrushes[1][0] = SellColor;
				PlotBrushes[2][0] = BuyColor;
			}
			else
			{
				Values[1].Reset();
				Values[2].Reset();
			}

			bool buy  = arrowUpFired   && trend == 0 && prevTrend == 1;
			bool sell = arrowDownFired && trend == 1 && prevTrend == 0;
			sBuySignal[0]  = buy;
			sSellSignal[0] = sell;

			if (buy)
			{
				if (ShowArrows)
					Draw.TriangleUp(this, "htBuy" + CurrentBar, true, 0, atrLowVal, BuyColor);
				if (ShowLabels)
					buyLabels[CurrentBar] = atrLowVal;
			}
			if (sell)
			{
				if (ShowArrows)
					Draw.TriangleDown(this, "htSell" + CurrentBar, true, 0, atrHighVal, SellColor);
				if (ShowLabels)
					sellLabels[CurrentBar] = atrHighVal;
			}
		}

		#endregion

		#region OnRenderTargetChanged + OnRender

		public override void OnRenderTargetChanged()
		{
			try
			{
				ReleaseRenderResources();
				if (RenderTarget == null) return;

				float fillAlpha = Math.Max(0f, Math.Min(1f, ChannelOpacity / 100f));
				dxBuyFill  = MakeDXBrush(BuyColor,  fillAlpha);
				dxSellFill = MakeDXBrush(SellColor, fillAlpha);
				dxBuyText  = MakeDXBrush(BuyColor,  1.0f);
				dxSellText = MakeDXBrush(SellColor, 1.0f);

				BuildTextFormat();
			}
			catch
			{
				// Suppress during chart teardown.
			}
		}

		private void BuildTextFormat()
		{
			DisposeTextFormat();
			tfLabel = new SharpDX.DirectWrite.TextFormat(Core.Globals.DirectWriteFactory, "Arial",
				SharpDX.DirectWrite.FontWeight.Bold, SharpDX.DirectWrite.FontStyle.Normal, LabelFontSize)
			{
				TextAlignment      = SharpDX.DirectWrite.TextAlignment.Center,
				ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center
			};
			lastLabelSize = LabelFontSize;
		}

		private void EnsureTextFormat()
		{
			if (tfLabel == null || lastLabelSize != LabelFontSize)
				BuildTextFormat();
		}

		private void DisposeTextFormat()
		{
			if (tfLabel != null) { tfLabel.Dispose(); tfLabel = null; }
		}

		private void ReleaseRenderResources()
		{
			if (dxBuyFill  != null) { dxBuyFill.Dispose();  dxBuyFill  = null; }
			if (dxSellFill != null) { dxSellFill.Dispose(); dxSellFill = null; }
			if (dxBuyText  != null) { dxBuyText.Dispose();  dxBuyText  = null; }
			if (dxSellText != null) { dxSellText.Dispose(); dxSellText = null; }
			DisposeTextFormat();
		}

		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			base.OnRender(chartControl, chartScale);

			if (RenderTarget == null)	return;
			if (ChartBars == null)		return;
			if (!IsVisible)				return;
			if (IsInHitTest)			return;

			int fromIdx = Math.Max(ChartBars.FromIndex, 0);
			int toIdx   = Math.Min(ChartBars.ToIndex,   CurrentBar);
			if (toIdx <= fromIdx) return;

			// Channel fills — gated by ShowChannels and ChannelOpacity > 0.
			if (ShowChannels && ChannelOpacity > 0
				&& sHalfTrendLine != null && sAtrHigh != null && sAtrLow != null)
			{
				for (int j = fromIdx; j < toIdx; j++)
				{
					if (!sHalfTrendLine.IsValidDataPointAt(j) || !sHalfTrendLine.IsValidDataPointAt(j + 1)) continue;
					if (!sAtrHigh.IsValidDataPointAt(j) || !sAtrHigh.IsValidDataPointAt(j + 1)) continue;
					if (!sAtrLow.IsValidDataPointAt(j)  || !sAtrLow.IsValidDataPointAt(j + 1)) continue;

					double htJ   = sHalfTrendLine.GetValueAt(j);
					double htJ1  = sHalfTrendLine.GetValueAt(j + 1);
					double ahJ   = sAtrHigh.GetValueAt(j);
					double ahJ1  = sAtrHigh.GetValueAt(j + 1);
					double alJ   = sAtrLow.GetValueAt(j);
					double alJ1  = sAtrLow.GetValueAt(j + 1);

					// Sell fill — between HT line and ATR High (above).
					FillBarTrapezoid(chartControl, chartScale, j,
						topLeftPrice:  ahJ,  botLeftPrice:  htJ,
						topRightPrice: ahJ1, botRightPrice: htJ1,
						brush: dxSellFill);

					// Buy fill — between ATR Low and HT line (below).
					FillBarTrapezoid(chartControl, chartScale, j,
						topLeftPrice:  htJ,  botLeftPrice:  alJ,
						topRightPrice: htJ1, botRightPrice: alJ1,
						brush: dxBuyFill);
				}
			}

			// Buy / Sell labels — sized text rendered via SharpDX so font size is fully controllable.
			if (ShowLabels && (buyLabels.Count > 0 || sellLabels.Count > 0))
			{
				EnsureTextFormat();
				if (tfLabel == null || dxBuyText == null || dxSellText == null) return;

				float w = LabelFontSize * 3.0f;
				float h = LabelFontSize * 1.6f;

				foreach (var kv in buyLabels)
				{
					int bar = kv.Key;
					if (bar < fromIdx || bar > toIdx) continue;
					float xC = chartControl.GetXByBarIndex(ChartBars, bar);
					float yC = (float)chartScale.GetYByValue(kv.Value);
					var rect = new SharpDX.RectangleF(xC - w * 0.5f, yC + h * 0.4f, w, h);	// below the channel
					RenderTarget.DrawText("Buy", tfLabel, rect, dxBuyText);
				}
				foreach (var kv in sellLabels)
				{
					int bar = kv.Key;
					if (bar < fromIdx || bar > toIdx) continue;
					float xC = chartControl.GetXByBarIndex(ChartBars, bar);
					float yC = (float)chartScale.GetYByValue(kv.Value);
					var rect = new SharpDX.RectangleF(xC - w * 0.5f, yC - h * 1.4f, w, h);	// above the channel
					RenderTarget.DrawText("Sell", tfLabel, rect, dxSellText);
				}
			}
		}

		private void FillBarTrapezoid(ChartControl chartControl, ChartScale chartScale, int barLeftIdx,
			double topLeftPrice, double botLeftPrice, double topRightPrice, double botRightPrice,
			SharpDX.Direct2D1.Brush brush)
		{
			if (brush == null) return;

			float xL  = chartControl.GetXByBarIndex(ChartBars, barLeftIdx);
			float xR  = chartControl.GetXByBarIndex(ChartBars, barLeftIdx + 1);
			float yTL = (float)chartScale.GetYByValue(topLeftPrice);
			float yBL = (float)chartScale.GetYByValue(botLeftPrice);
			float yTR = (float)chartScale.GetYByValue(topRightPrice);
			float yBR = (float)chartScale.GetYByValue(botRightPrice);

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
		private indTradingView.HalfTrend[] cacheHalfTrend;
		public indTradingView.HalfTrend HalfTrend(int amplitude, int channelDeviation, int atrPeriod, bool showArrows, bool showChannels, int channelOpacity, bool showLabels, int labelFontSize)
		{
			return HalfTrend(Input, amplitude, channelDeviation, atrPeriod, showArrows, showChannels, channelOpacity, showLabels, labelFontSize);
		}

		public indTradingView.HalfTrend HalfTrend(ISeries<double> input, int amplitude, int channelDeviation, int atrPeriod, bool showArrows, bool showChannels, int channelOpacity, bool showLabels, int labelFontSize)
		{
			if (cacheHalfTrend != null)
				for (int idx = 0; idx < cacheHalfTrend.Length; idx++)
					if (cacheHalfTrend[idx] != null && cacheHalfTrend[idx].Amplitude == amplitude && cacheHalfTrend[idx].ChannelDeviation == channelDeviation && cacheHalfTrend[idx].AtrPeriod == atrPeriod && cacheHalfTrend[idx].ShowArrows == showArrows && cacheHalfTrend[idx].ShowChannels == showChannels && cacheHalfTrend[idx].ChannelOpacity == channelOpacity && cacheHalfTrend[idx].ShowLabels == showLabels && cacheHalfTrend[idx].LabelFontSize == labelFontSize && cacheHalfTrend[idx].EqualsInput(input))
						return cacheHalfTrend[idx];
			return CacheIndicator<indTradingView.HalfTrend>(new indTradingView.HalfTrend(){ Amplitude = amplitude, ChannelDeviation = channelDeviation, AtrPeriod = atrPeriod, ShowArrows = showArrows, ShowChannels = showChannels, ChannelOpacity = channelOpacity, ShowLabels = showLabels, LabelFontSize = labelFontSize }, input, ref cacheHalfTrend);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.HalfTrend HalfTrend(int amplitude, int channelDeviation, int atrPeriod, bool showArrows, bool showChannels, int channelOpacity, bool showLabels, int labelFontSize)
		{
			return indicator.HalfTrend(Input, amplitude, channelDeviation, atrPeriod, showArrows, showChannels, channelOpacity, showLabels, labelFontSize);
		}

		public Indicators.indTradingView.HalfTrend HalfTrend(ISeries<double> input , int amplitude, int channelDeviation, int atrPeriod, bool showArrows, bool showChannels, int channelOpacity, bool showLabels, int labelFontSize)
		{
			return indicator.HalfTrend(input, amplitude, channelDeviation, atrPeriod, showArrows, showChannels, channelOpacity, showLabels, labelFontSize);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.HalfTrend HalfTrend(int amplitude, int channelDeviation, int atrPeriod, bool showArrows, bool showChannels, int channelOpacity, bool showLabels, int labelFontSize)
		{
			return indicator.HalfTrend(Input, amplitude, channelDeviation, atrPeriod, showArrows, showChannels, channelOpacity, showLabels, labelFontSize);
		}

		public Indicators.indTradingView.HalfTrend HalfTrend(ISeries<double> input , int amplitude, int channelDeviation, int atrPeriod, bool showArrows, bool showChannels, int channelOpacity, bool showLabels, int labelFontSize)
		{
			return indicator.HalfTrend(input, amplitude, channelDeviation, atrPeriod, showArrows, showChannels, channelOpacity, showLabels, labelFontSize);
		}
	}
}

#endregion
