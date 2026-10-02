# Inertial RSI [LuxAlgo] — NinjaTrader 8

A faithful, **non-repainting** NinjaScript conversion of the TradingView Pine indicator by **LuxAlgo**.

| | |
|---|---|
| **Full write-up** | [mydailytake.com](https://mydailytake.com/inertial-rsi-luxalgo-ninjatrader-8/) |
| **Original Pine script** | [by LuxAlgo](https://www.tradingview.com/script/eg4t9sX8) |
| **License** | **MPL-2.0** — inherited from the original. See [LICENSE](LICENSE). |
| **Platform** | NinjaTrader 8 |

## What it does

For each closed bar it scans every RSI length in a range and picks the one whose value sits closest to the previous bar's chosen value — an inertia-weighted RSI that resists noise. K is a moving average of that inertial RSI and Signal is a moving average of K; a cross above the oversold level prints a bullish triangle on the price panel, a cross below overbought prints a bearish one.

[Read the full write-up →](https://mydailytake.com/inertial-rsi-luxalgo-ninjatrader-8/)

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
| Settings | **Settings** | Min/max RSI length range, K and Signal smoothing, and the overbought/oversold levels. |
| Visuals | **Visuals** | Triangle signals, gradient fills, and coloring. |

## Found a bug?

If it doesn't match TradingView exactly, that's probably the **data**, not the code — different feed,
different ticks and session times, different line. If it repaints, errors, or the logic plainly
diverges from the Pine, that's more likely **my port** than LuxAlgo's script, and I'd like to know.
[Open an issue](../../issues) with your NT8 version, instrument, bar type, and data provider. More in
the [main README](../../README.md#found-a-bug).

## Attribution

Adapted from Pine Script™ code originally licensed under **MPL-2.0**. The original Pine Script™ code
is by **LuxAlgo**. Adaptation for NinjaTrader by [jack@mydailytake.com](mailto:jack@mydailytake.com).
