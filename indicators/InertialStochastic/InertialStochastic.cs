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

// NT8 Version of Inertial Stochastic
// This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under a Attribution-NonCommercial-ShareAlike 4.0 International.
// The original Pine Script™ code is by LuxAlgo and can be found at: https://www.tradingview.com/script/AgyYROJE-Inertial-Stochastic-LuxAlgo/
// Adaptation for NinjaTrader by jack@mydailytake.com
// © 2026 MyDailyTake.com. All rights reserved.
// Adapted code is provided under the terms of the Attribution-NonCommercial-ShareAlike 4.0 International (CC BY-NC-SA 4.0) https://creativecommons.org/licenses/by-nc-sa/4.0/
// The use of LuxAlgo name or its adapted code in this work does not imply endorsement by the original authors.
//
// Notes:
//   For each closed bar, scans every stochastic length from MinLen..MaxLen and picks the one whose
//   stochastic value sits closest to the previous bar's chosen value (LV — "Last Visited"). The result
//   is a stochastic that resists noise — it stays put unless a length confirms it must move. K is an
//   SMA of LV; D is an SMA of K.
//
//   Visuals: K line colored bull/bear above/below 50, D dotted, 80/20 hlines, and a vertical gradient
//   fill between K and the 50 midline (canonical SharpDX LinearGradientBrush template).
//
//   Non-repainting. Public Series outputs: KLine, DLine, LvStoch.

namespace NinjaTrader.NinjaScript.Indicators.indTradingView
{
	#region Categories
	[Gui.CategoryOrder("Settings",	10100)]
	[Gui.CategoryOrder("Style",		10200)]
	#endregion

	public class InertialStochastic : Indicator
	{
		#region indInfo

		private string indName        = "Inertial Stochastic [LuxAlgo]";
		private string indDescription = "This code has been converted from Pine Script™ to NinjaTrader 8 by MyDailyTake.com. The original code by LuxAlgo can be found here: https://www.tradingview.com/script/AgyYROJE-Inertial-Stochastic-LuxAlgo/";

		#endregion

		#region Properties

		[NinjaScriptProperty]
		[Range(2, int.MaxValue)]
		[Display(Order = 1, GroupName = "Settings", Name = "Minimum Length",
			Description = "Smallest stochastic length the inertia scan considers.")]
		public int MinLen { get; set; }

		[NinjaScriptProperty]
		[Range(2, int.MaxValue)]
		[Display(Order = 2, GroupName = "Settings", Name = "Maximum Length",
			Description = "Largest stochastic length the inertia scan considers.")]
		public int MaxLen { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 3, GroupName = "Settings", Name = "K Smoothing",
			Description = "SMA length applied to the inertia stochastic to produce K.")]
		public int SmoothK { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Order = 4, GroupName = "Settings", Name = "D Smoothing",
			Description = "SMA length applied to K to produce D.")]
		public int SmoothD { get; set; }

		[XmlIgnore]
		[Display(Order = 1, GroupName = "Style", Name = "Bullish Color",
			Description = "K line and gradient fill color when K > 50.")]
		public Brush BullColor { get; set; }
			[Browsable(false)]
			public string BullColorSerialize
			{
				get { return Serialize.BrushToString(BullColor); }
				set { BullColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		[XmlIgnore]
		[Display(Order = 2, GroupName = "Style", Name = "Bearish Color",
			Description = "K line and gradient fill color when K < 50.")]
		public Brush BearColor { get; set; }
			[Browsable(false)]
			public string BearColorSerialize
			{
				get { return Serialize.BrushToString(BearColor); }
				set { BearColor = EnsureFrozen(Serialize.StringToBrush(value)); }
			}

		#endregion

		#region Public outputs

		[Browsable(false)][XmlIgnore] public Series<double> KLine   { get { return Values[0]; } }
		[Browsable(false)][XmlIgnore] public Series<double> DLine   { get { return Values[1]; } }
		[Browsable(false)][XmlIgnore] public Series<double> LvStoch { get { Update(); return sLvStoch; } }

		#endregion

		#region Variables

		// Persistent across bars; seeded to 50.0 in DataLoaded.
		private double			lvStoch;

		private Series<double>	sLvStoch;
		private Series<double>	sK;					// Infinite — OnRender visible-window read.
		private SMA				kInd;
		private SMA				dInd;

		// SharpDX gradient resources — stops cached, brush rebuilt per render (Y coords change with chart scale).
		private SharpDX.Direct2D1.GradientStopCollection	bullStops;
		private SharpDX.Direct2D1.GradientStopCollection	bearStops;
		private SharpDX.Color4								lastBullC4;
		private SharpDX.Color4								lastBearC4;

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

				MinLen	= 10;
				MaxLen	= 40;
				SmoothK	= 3;
				SmoothD	= 3;

				BullColor = new SolidColorBrush(Color.FromRgb(0x08, 0x99, 0x81));
				BearColor = new SolidColorBrush(Color.FromRgb(0xf2, 0x36, 0x45));
				EnsureFrozen(BullColor);
				EnsureFrozen(BearColor);

				// K plot stroke defaults DimGray — per-bar color is driven by BullColor / BearColor via PlotBrushes.
				AddPlot(new Stroke(Brushes.DimGray, DashStyleHelper.Solid, 2f), PlotStyle.Line, "K");
				// D plot is solid #787b86 (medium gray); user can override via NT plot dialog.
				AddPlot(new Stroke(new SolidColorBrush(Color.FromRgb(0x78, 0x7b, 0x86)), DashStyleHelper.Dot, 2f), PlotStyle.Line, "D");

				// Overbought / Oversold reference lines.
				Brush refBrush = new SolidColorBrush(Color.FromArgb(128, 192, 192, 192));
				EnsureFrozen(refBrush);
				AddLine(new Stroke(refBrush, DashStyleHelper.Dash, 1f), 80.0, "Overbought");
				AddLine(new Stroke(refBrush, DashStyleHelper.Dash, 1f), 20.0, "Oversold");
			}
			else if (State == State.DataLoaded)
			{
				lvStoch = 50.0;

				// SMA wraps with SmoothK (user-configurable, can exceed 256) — needs Infinite.
				sLvStoch	= new Series<double>(this, MaximumBarsLookBack.Infinite);
				// OnRender renders gradient fills based on K across the visible window — needs Infinite.
				sK			= new Series<double>(this, MaximumBarsLookBack.Infinite);

				kInd		= SMA(sLvStoch, SmoothK);
				dInd		= SMA(kInd,     SmoothD);
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
			int needed = MaxLen + SmoothK + SmoothD;
			if (CurrentBar < needed) return;

			// Inertia scan: pick the length whose stochastic value sits closest to the previous bar's lvStoch.
			double bestStoch	= 50.0;
			double minDiff		= 1e10;
			double hh			= High[0];
			double ll			= Low[0];

			for (int i = 1; i <= MaxLen - 1; i++)
			{
				hh = Math.Max(hh, High[i]);
				ll = Math.Min(ll, Low[i]);

				int currentLen = i + 1;
				if (currentLen >= MinLen)
				{
					double den   = hh - ll;
					double stoch = den == 0.0 ? 50.0 : 100.0 * (Close[0] - ll) / den;
					double diff  = Math.Abs(stoch - lvStoch);

					if (diff < minDiff)
					{
						minDiff   = diff;
						bestStoch = stoch;
					}
				}
			}

			lvStoch        = bestStoch;
			sLvStoch[0]    = lvStoch;

			double k = kInd[0];
			double d = dInd[0];

			Values[0][0]      = k;
			Values[1][0]      = d;
			sK[0]             = k;
			PlotBrushes[0][0] = (k > 50.0) ? BullColor : BearColor;
		}

		#endregion

		#region OnRenderTargetChanged + helpers

		public override void OnRenderTargetChanged()
		{
			try
			{
				ReleaseRenderResources();
				if (RenderTarget == null) return;

				BuildGradientStops();
			}
			catch
			{
				// Suppress during chart teardown.
			}
		}

		private void BuildGradientStops()
		{
			DisposeGradientStops();
			if (RenderTarget == null) return;

			SharpDX.Color4 bullSolid = ToColor4(BullColor, 1.0f);
			SharpDX.Color4 bullClear = new SharpDX.Color4(bullSolid.Red, bullSolid.Green, bullSolid.Blue, 0f);
			SharpDX.Color4 bearSolid = ToColor4(BearColor, 1.0f);
			SharpDX.Color4 bearClear = new SharpDX.Color4(bearSolid.Red, bearSolid.Green, bearSolid.Blue, 0f);

			// Bull gradient: top (Y at value=100) = solid, bottom (Y at value=50) = transparent.
			bullStops = new SharpDX.Direct2D1.GradientStopCollection(RenderTarget, new[]
			{
				new SharpDX.Direct2D1.GradientStop { Position = 0f, Color = bullSolid },
				new SharpDX.Direct2D1.GradientStop { Position = 1f, Color = bullClear }
			});

			// Bear gradient: top (Y at value=50) = transparent, bottom (Y at value=0) = solid.
			bearStops = new SharpDX.Direct2D1.GradientStopCollection(RenderTarget, new[]
			{
				new SharpDX.Direct2D1.GradientStop { Position = 0f, Color = bearClear },
				new SharpDX.Direct2D1.GradientStop { Position = 1f, Color = bearSolid }
			});

			lastBullC4 = bullSolid;
			lastBearC4 = bearSolid;
		}

		private void EnsureGradientStops()
		{
			if (bullStops == null || bearStops == null)
			{
				BuildGradientStops();
				return;
			}

			SharpDX.Color4 currentBull = ToColor4(BullColor, 1.0f);
			SharpDX.Color4 currentBear = ToColor4(BearColor, 1.0f);
			if (!ColorsEqual(currentBull, lastBullC4) || !ColorsEqual(currentBear, lastBearC4))
				BuildGradientStops();
		}

		private void DisposeGradientStops()
		{
			if (bullStops != null) { bullStops.Dispose(); bullStops = null; }
			if (bearStops != null) { bearStops.Dispose(); bearStops = null; }
		}

		private void ReleaseRenderResources()
		{
			DisposeGradientStops();
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
			if (sK == null)				return;

			EnsureGradientStops();
			if (bullStops == null || bearStops == null) return;

			int fromIdx = Math.Max(ChartBars.FromIndex, 0);
			int toIdx   = Math.Min(ChartBars.ToIndex,   CurrentBar);
			if (toIdx <= fromIdx) return;

			// Gradient extents in Y-space: vertical from value 100 → 50 (bull) and 50 → 0 (bear).
			float yAt100 = (float)chartScale.GetYByValue(100.0);
			float yAt50  = (float)chartScale.GetYByValue(50.0);
			float yAt0   = (float)chartScale.GetYByValue(0.0);

			SharpDX.Direct2D1.LinearGradientBrush bullBrush = null;
			SharpDX.Direct2D1.LinearGradientBrush bearBrush = null;
			try
			{
				bullBrush = new SharpDX.Direct2D1.LinearGradientBrush(RenderTarget,
					new SharpDX.Direct2D1.LinearGradientBrushProperties
					{
						StartPoint = new SharpDX.Vector2(0f, yAt100),
						EndPoint   = new SharpDX.Vector2(0f, yAt50)
					},
					bullStops);

				bearBrush = new SharpDX.Direct2D1.LinearGradientBrush(RenderTarget,
					new SharpDX.Direct2D1.LinearGradientBrushProperties
					{
						StartPoint = new SharpDX.Vector2(0f, yAt50),
						EndPoint   = new SharpDX.Vector2(0f, yAt0)
					},
					bearStops);

				for (int j = fromIdx; j < toIdx; j++)
				{
					if (!sK.IsValidDataPointAt(j) || !sK.IsValidDataPointAt(j + 1)) continue;

					double kJ  = sK.GetValueAt(j);
					double kJ1 = sK.GetValueAt(j + 1);

					float yKj  = (float)chartScale.GetYByValue(kJ);
					float yKj1 = (float)chartScale.GetYByValue(kJ1);

					// Bull half — top edge follows max(K, 50), bottom edge clamped to 50. Renders only when at least one side is > 50.
					if (kJ > 50.0 || kJ1 > 50.0)
					{
						float topL = (float)chartScale.GetYByValue(Math.Max(kJ,  50.0));
						float topR = (float)chartScale.GetYByValue(Math.Max(kJ1, 50.0));
						FillBarTrapezoid(chartControl, j,
							topLeftY: topL, topRightY: topR,
							botLeftY: yAt50, botRightY: yAt50,
							brush: bullBrush);
					}

					// Bear half — top edge clamped to 50, bottom edge follows min(K, 50).
					if (kJ < 50.0 || kJ1 < 50.0)
					{
						float botL = (float)chartScale.GetYByValue(Math.Min(kJ,  50.0));
						float botR = (float)chartScale.GetYByValue(Math.Min(kJ1, 50.0));
						FillBarTrapezoid(chartControl, j,
							topLeftY: yAt50, topRightY: yAt50,
							botLeftY: botL, botRightY: botR,
							brush: bearBrush);
					}
				}
			}
			finally
			{
				if (bullBrush != null) bullBrush.Dispose();
				if (bearBrush != null) bearBrush.Dispose();
			}
		}

		private void FillBarTrapezoid(ChartControl chartControl, int barLeftIdx,
			float topLeftY, float topRightY, float botLeftY, float botRightY,
			SharpDX.Direct2D1.Brush brush)
		{
			if (brush == null) return;

			float xL = chartControl.GetXByBarIndex(ChartBars, barLeftIdx);
			float xR = chartControl.GetXByBarIndex(ChartBars, barLeftIdx + 1);

			SharpDX.Direct2D1.PathGeometry geom = null;
			SharpDX.Direct2D1.GeometrySink sink = null;
			try
			{
				geom = new SharpDX.Direct2D1.PathGeometry(Core.Globals.D2DFactory);
				sink = geom.Open();
				sink.SetFillMode(SharpDX.Direct2D1.FillMode.Winding);
				sink.BeginFigure(new SharpDX.Vector2(xL, topLeftY), SharpDX.Direct2D1.FigureBegin.Filled);
				sink.AddLine(new SharpDX.Vector2(xR, topRightY));
				sink.AddLine(new SharpDX.Vector2(xR, botRightY));
				sink.AddLine(new SharpDX.Vector2(xL, botLeftY));
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

		#region Color helpers

		private static SharpDX.Color4 ToColor4(Brush wpf, float alphaScale)
		{
			var scb = wpf as SolidColorBrush;
			System.Windows.Media.Color c = scb != null ? scb.Color : Colors.Gray;
			float wpfA = scb != null ? (float)(scb.Opacity * (c.A / 255f)) : 1f;
			return new SharpDX.Color4(c.R / 255f, c.G / 255f, c.B / 255f,
				Math.Max(0f, Math.Min(1f, alphaScale * wpfA)));
		}

		private static bool ColorsEqual(SharpDX.Color4 a, SharpDX.Color4 b)
		{
			const float eps = 1e-4f;
			return Math.Abs(a.Red   - b.Red)   < eps
				&& Math.Abs(a.Green - b.Green) < eps
				&& Math.Abs(a.Blue  - b.Blue)  < eps
				&& Math.Abs(a.Alpha - b.Alpha) < eps;
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
		private indTradingView.InertialStochastic[] cacheInertialStochastic;
		public indTradingView.InertialStochastic InertialStochastic(int minLen, int maxLen, int smoothK, int smoothD)
		{
			return InertialStochastic(Input, minLen, maxLen, smoothK, smoothD);
		}

		public indTradingView.InertialStochastic InertialStochastic(ISeries<double> input, int minLen, int maxLen, int smoothK, int smoothD)
		{
			if (cacheInertialStochastic != null)
				for (int idx = 0; idx < cacheInertialStochastic.Length; idx++)
					if (cacheInertialStochastic[idx] != null && cacheInertialStochastic[idx].MinLen == minLen && cacheInertialStochastic[idx].MaxLen == maxLen && cacheInertialStochastic[idx].SmoothK == smoothK && cacheInertialStochastic[idx].SmoothD == smoothD && cacheInertialStochastic[idx].EqualsInput(input))
						return cacheInertialStochastic[idx];
			return CacheIndicator<indTradingView.InertialStochastic>(new indTradingView.InertialStochastic(){ MinLen = minLen, MaxLen = maxLen, SmoothK = smoothK, SmoothD = smoothD }, input, ref cacheInertialStochastic);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.indTradingView.InertialStochastic InertialStochastic(int minLen, int maxLen, int smoothK, int smoothD)
		{
			return indicator.InertialStochastic(Input, minLen, maxLen, smoothK, smoothD);
		}

		public Indicators.indTradingView.InertialStochastic InertialStochastic(ISeries<double> input , int minLen, int maxLen, int smoothK, int smoothD)
		{
			return indicator.InertialStochastic(input, minLen, maxLen, smoothK, smoothD);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.indTradingView.InertialStochastic InertialStochastic(int minLen, int maxLen, int smoothK, int smoothD)
		{
			return indicator.InertialStochastic(Input, minLen, maxLen, smoothK, smoothD);
		}

		public Indicators.indTradingView.InertialStochastic InertialStochastic(ISeries<double> input , int minLen, int maxLen, int smoothK, int smoothD)
		{
			return indicator.InertialStochastic(input, minLen, maxLen, smoothK, smoothD);
		}
	}
}

#endregion
