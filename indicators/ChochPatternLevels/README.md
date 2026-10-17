# Choch Pattern Levels [BigBeluga] — NinjaTrader 8

A faithful, **non-repainting** NinjaScript conversion of the TradingView Pine indicator by **BigBeluga**.

| | |
|---|---|
| **Full write-up** | [mydailytake.com](https://mydailytake.com/choch-pattern-levels-bigbeluga-ninjatrader-8/) |
| **Original Pine script** | [by BigBeluga](https://www.tradingview.com/script/WVwxrsY3) |
| **License** | **CC-BY-NC-SA-4.0** — inherited from the original. See [LICENSE](LICENSE). |
| **Platform** | NinjaTrader 8 |

## What it does

Marks Change-of-Character (CHOCH) structure breaks. It tracks the most recent confirmed pivot high and low; when price breaks the prior pivot against the trend, it builds a triangle from the broken pivot, the breakout bar, and the extreme between them, and draws a horizontal level at that extreme extending right — a clean read on where structure shifted.

[Read the full write-up →](https://mydailytake.com/choch-pattern-levels-bigbeluga-ninjatrader-8/)

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
| Parameters | **Parameters** | Pivot length and how CHOCH breaks are detected. |
| Delta Volume | **Delta Volume** | Delta-volume readout shown on the levels. |
| Colors | **Colors** | Bullish/bearish level and label coloring. |

## Found a bug?

If it doesn't match TradingView exactly, that's probably the **data**, not the code — different feed,
different ticks and session times, different line. If it repaints, errors, or the logic plainly
diverges from the Pine, that's more likely **my port** than BigBeluga's script, and I'd like to know.
[Open an issue](../../issues) with your NT8 version, instrument, bar type, and data provider. More in
the [main README](../../README.md#found-a-bug).

## Attribution

Adapted from Pine Script™ code originally licensed under **CC-BY-NC-SA-4.0**. The original Pine Script™ code
is by **BigBeluga**. Adaptation for NinjaTrader by [jack@mydailytake.com](mailto:jack@mydailytake.com).

## Disclaimer

For educational and informational purposes only. Nothing here is trading advice. Futures trading
involves substantial risk of loss and is not suitable for every investor. See the
[full disclosure](https://mydailytake.com/disclosure/).
