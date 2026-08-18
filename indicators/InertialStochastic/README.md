# Inertial Stochastic [LuxAlgo] — NinjaTrader 8

A faithful, **non-repainting** NinjaScript conversion of the TradingView Pine indicator by **LuxAlgo**.

| | |
|---|---|
| **Full write-up** | [mydailytake.com](https://mydailytake.com/inertial-stochastic-luxalgo-ninjatrader-8/) |
| **Original Pine script** | [by LuxAlgo](https://www.tradingview.com/script/AgyYROJE) |
| **License** | **CC-BY-NC-SA-4.0** — inherited from the original. See [LICENSE](LICENSE). |
| **Platform** | NinjaTrader 8 |

## What it does

A standard stochastic uses a single fixed lookback — too short and it twitches, too long and it lags every turn. LuxAlgo's Inertial Stochastic scans every length each bar and keeps the **steadiest**, so the oscillator stays smooth without going stale.

[Read the full write-up →](https://mydailytake.com/inertial-stochastic-luxalgo-ninjatrader-8/)

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
| Settings | **Settings** | The range of lookback lengths scanned each bar and the input series. |
| Style | **Style** | Oscillator coloring and overbought / oversold levels. |

## Found a bug?

If it doesn't match TradingView exactly, that's probably the **data**, not the code — different feed,
different ticks and session times, different line. If it repaints, errors, or the logic plainly
diverges from the Pine, that's more likely **my port** than LuxAlgo's script, and I'd like to know.
[Open an issue](../../issues) with your NT8 version, instrument, bar type, and data provider. More in
the [main README](../../README.md#found-a-bug).

## Attribution

Adapted from Pine Script™ code originally licensed under **CC-BY-NC-SA-4.0**. The original Pine Script™ code
is by **LuxAlgo**. Adaptation for NinjaTrader by [jack@mydailytake.com](mailto:jack@mydailytake.com).
