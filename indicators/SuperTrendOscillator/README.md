# SuperTrend Oscillator [ChartPrime] — NinjaTrader 8

A faithful, **non-repainting** NinjaScript conversion of the TradingView Pine indicator by **ChartPrime**.

| | |
|---|---|
| **Full write-up** | [mydailytake.com](https://mydailytake.com/supertrend-oscillator-chartprime-ninjatrader-8/) |
| **Original Pine script** | [by ChartPrime](https://www.tradingview.com/script/JqEFTgOE) |
| **License** | **MPL-2.0** — inherited from the original. See [LICENSE](LICENSE). |
| **Platform** | NinjaTrader 8 |

## What it does

SuperTrend gives you a binary trend signal: above the band or below it. ChartPrime's SuperTrend Oscillator turns that into a smoothed oscillator, so you read the **strength** of the trend, not just which side of the line price is on.

[Read the full write-up →](https://mydailytake.com/supertrend-oscillator-chartprime-ninjatrader-8/)

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
| SuperTrend | **SuperTrend** | ATR length and multiplier for the underlying band the oscillator is built from. |
| Oscillator | **Oscillator** | Oscillator type (HMA/EMA/…) and smoothing applied to the normalized trend distance. |
| Appearance | **Appearance** | Bull/bear coloring and how the oscillator is plotted. |

## Found a bug?

If it doesn't match TradingView exactly, that's probably the **data**, not the code — different feed,
different ticks and session times, different line. If it repaints, errors, or the logic plainly
diverges from the Pine, that's more likely **my port** than ChartPrime's script, and I'd like to know.
[Open an issue](../../issues) with your NT8 version, instrument, bar type, and data provider. More in
the [main README](../../README.md#found-a-bug).

## Attribution

Adapted from Pine Script™ code originally licensed under **MPL-2.0**. The original Pine Script™ code
is by **ChartPrime**. Adaptation for NinjaTrader by [jack@mydailytake.com](mailto:jack@mydailytake.com).
