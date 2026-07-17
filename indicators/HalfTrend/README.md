# HalfTrend [everget] — NinjaTrader 8

A faithful, **non-repainting** NinjaScript conversion of the TradingView Pine indicator by **everget**.

| | |
|---|---|
| **Full write-up** | [mydailytake.com](https://mydailytake.com/halftrend-everget-ninjatrader-8/) |
| **Original Pine script** | [by everget](https://www.tradingview.com/script/U1SJ8ubc) |
| **License** | **GPL-3.0** — inherited from the original. See [LICENSE](LICENSE). |
| **Platform** | NinjaTrader 8 |

## What it does

SuperTrend was a milestone for systematic trend trading — but its biggest weakness is whipsaw. Price wicks past the band by a tick, the trend flips, and you're stopped out one bar before the real move resumes.

[Read the full write-up →](https://mydailytake.com/halftrend-everget-ninjatrader-8/)

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
| Calculation | **Calculation** | Lookback for the SMA-of-High / SMA-of-Low and pivot detection. |
| Visuals | **Visuals** | Triangle markers at trend flips. |
| Display | **Display** | HalfTrend line, lower channel, and buy markers when in an up-trend. |

## Found a bug?

If it doesn't match TradingView exactly, that's probably the **data**, not the code — different feed,
different ticks and session times, different line. If it repaints, errors, or the logic plainly
diverges from the Pine, that's more likely **my port** than everget's script, and I'd like to know.
[Open an issue](../../issues) with your NT8 version, instrument, bar type, and data provider. More in
the [main README](../../README.md#found-a-bug).

## Attribution

Adapted from Pine Script™ code originally licensed under **GPL-3.0**. The original Pine Script™ code
is by **everget**. Adaptation for NinjaTrader by [jack@mydailytake.com](mailto:jack@mydailytake.com).
Use of the everget name or its adapted code here does not imply endorsement by the original author.

## Disclaimer

For educational and informational purposes only. Nothing here is trading advice. Futures trading
involves substantial risk of loss and is not suitable for every investor. See the
[full disclosure](https://mydailytake.com/disclosure/).
