# Kalman Volume Trend [BigBeluga] — NinjaTrader 8

A faithful, **non-repainting** NinjaScript conversion of the TradingView Pine indicator by **BigBeluga**.

| | |
|---|---|
| **Full write-up** | [mydailytake.com](https://mydailytake.com/kalman-volume-trend-bigbeluga-ninjatrader-8/) |
| **Original Pine script** | [by BigBeluga](https://www.tradingview.com/script/eGdeZydE) |
| **License** | **CC-BY-NC-SA-4.0** — inherited from the original. See [LICENSE](LICENSE). |
| **Platform** | NinjaTrader 8 |

## What it does

A Kalman filter is the natural response to noisy price data — recursively estimate the underlying state and downweight new observations by how much you trust them. BigBeluga applies it to a volume-weighted trend, so the line tracks **conviction**, not just price, and flags the volume extremes that move it.

[Read the full write-up →](https://mydailytake.com/kalman-volume-trend-bigbeluga-ninjatrader-8/)

## Non-repainting

Values are computed from **closed-bar data and are not rewritten after the fact**. What you see on a
historical bar is what the indicator would have shown live on that bar.

## Install

1. Download the `.cs` file in this folder.
2. Copy it to `Documents\NinjaTrader 8\bin\Custom\Indicators\`.
3. In NinjaTrader: **Control Center → New → NinjaScript Editor**, press **F5** to compile.
4. Add the indicator to a chart.

> Prefer one-click? The write-up ships the same indicator as a NinjaScript `.zip` for
> **Tools → Import → NinjaScript Add-On**.

## Settings

| Group | Setting | Description |
|---|---|---|
| Parameters | **Parameters** | Kalman length and smoothing — the filter's responsiveness vs. noise rejection. |
| Volume Extremes | **Volume Extremes** | Threshold for flagging high-conviction volume events on the trend. |
| Dashboard | **Dashboard** | Optional on-chart table reading out the current state. |
| Colors | **Colors** | Bull / bear / neutral trend coloring. |

## Found a bug?

If it doesn't match TradingView exactly, that's probably the **data**, not the code — different feed,
different ticks and session times, different line. If it repaints, errors, or the logic plainly
diverges from the Pine, that's more likely **my port** than BigBeluga's script, and I'd like to know.
[Open an issue](../../issues) with your NT8 version, instrument, bar type, and data provider. More in
the [main README](../../README.md#found-a-bug).

## Attribution

Adapted from Pine Script™ code originally licensed under **CC-BY-NC-SA-4.0**. The original Pine Script™ code
is by **BigBeluga**. Adaptation for NinjaTrader by [jack@mydailytake.com](mailto:jack@mydailytake.com).
