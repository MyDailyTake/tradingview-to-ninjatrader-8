# Evasive SuperTrend [LuxAlgo] — NinjaTrader 8

A faithful, **non-repainting** NinjaScript conversion of the TradingView Pine indicator by **LuxAlgo**.

| | |
|---|---|
| **Full write-up** | [mydailytake.com](https://mydailytake.com/evasive-supertrend-luxalgo-ninjatrader-8/) |
| **Original Pine script** | [by LuxAlgo](https://www.tradingview.com/script/tfC7w3jE) |
| **License** | **CC-BY-NC-SA-4.0** — inherited from the original. See [LICENSE](LICENSE). |
| **Platform** | NinjaTrader 8 |

## What it does

Standard SuperTrend gives you a clean trailing stop in trends and brutal whipsaws in noise. LuxAlgo's Evasive SuperTrend is built for the noise problem — when price gets within a configurable Threshold × ATR of the active band, the band actively pushes away from price by Alpha × ATR, dodging the wick that would have flipped the standard…

[Read the full write-up →](https://mydailytake.com/evasive-supertrend-luxalgo-ninjatrader-8/)

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
| Supertrend Settings | **Supertrend Settings** | Lookback for the ATR that drives band width. |
| Noise Avoidance Logic | **Noise Avoidance Logic** | If price gets closer to the band than this fraction of ATR, the band is pushed away. |
| Visualization | **Visualization** | Print BULL / BEAR text at trend flips. |

## Found a bug?

If it doesn't match TradingView exactly, that's probably the **data**, not the code — different feed,
different ticks and session times, different line. If it repaints, errors, or the logic plainly
diverges from the Pine, that's more likely **my port** than LuxAlgo's script, and I'd like to know.
[Open an issue](../../issues) with your NT8 version, instrument, bar type, and data provider. More in
the [main README](../../README.md#found-a-bug).

## Attribution

Adapted from Pine Script™ code originally licensed under **CC-BY-NC-SA-4.0**. The original Pine Script™ code
is by **LuxAlgo**. Adaptation for NinjaTrader by [jack@mydailytake.com](mailto:jack@mydailytake.com).
Use of the LuxAlgo name or its adapted code here does not imply endorsement by the original author.

## Disclaimer

For educational and informational purposes only. Nothing here is trading advice. Futures trading
involves substantial risk of loss and is not suitable for every investor. See the
[full disclosure](https://mydailytake.com/disclosure/).
