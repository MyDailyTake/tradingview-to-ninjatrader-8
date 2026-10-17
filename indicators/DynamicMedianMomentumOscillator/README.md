# Dynamic Median Momentum Oscillator [AlgoAlpha] — NinjaTrader 8

A faithful, **non-repainting** NinjaScript conversion of the TradingView Pine indicator by **AlgoAlpha**.

| | |
|---|---|
| **Full write-up** | [mydailytake.com](https://mydailytake.com/dynamic-median-momentum-oscillator-algoalpha-ninjatrader-8/) |
| **Original Pine script** | [by AlgoAlpha](https://www.tradingview.com/script/EDgeowAw) |
| **License** | **MPL-2.0** — inherited from the original. See [LICENSE](LICENSE). |
| **Platform** | NinjaTrader 8 |

## What it does

Most momentum oscillators measure price against a moving average. This one measures it against a rolling median of HLC3, which barely moves on a single spike bar, so the oscillator stays cleaner around news and gaps. Optional standardization divides the raw oscillator by the average candle range, putting every instrument on the same fixed scale so the ±200 / ±250 / ±300 overbought and oversold zones mean the same thing on a $5 stock and a $5,000 futures contract. Smooth MCD is an EMA of the oscillator, Signal is an EMA of Smooth MCD, and the histogram is their difference.

[Read the full write-up →](https://mydailytake.com/dynamic-median-momentum-oscillator-algoalpha-ninjatrader-8/)

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
| Parameters | **Basis Length** | Lookback used to calculate the rolling median basis (HLC3). |
| Parameters | **Smoothing Length** | EMA length applied to the raw oscillator to produce the Smooth MCD line. |
| Parameters | **Signal Length** | EMA length used for the Signal line. |
| Parameters | **Standardize** | Normalize the oscillator by average candle range so values can use fixed OB/OS zones. |
| Display | **Display Mode** | Chooses which oscillator components are visible. |
| Appearance | **Bullish Color** | Color used for bullish lines, fills, and positive histogram bars. |
| Appearance | **Bearish Color** | Color used for bearish lines, fills, and negative histogram bars. |

## Found a bug?

If it doesn't match TradingView exactly, that's probably the **data**, not the code — different feed,
different ticks and session times, different line. If it repaints, errors, or the logic plainly
diverges from the Pine, that's more likely **my port** than AlgoAlpha's script, and I'd like to know.
[Open an issue](../../issues) with your NT8 version, instrument, bar type, and data provider. More in
the [main README](../../README.md#found-a-bug).

## Attribution

Adapted from Pine Script™ code originally licensed under **MPL-2.0**. The original Pine Script™ code
is by **AlgoAlpha**. Adaptation for NinjaTrader by [jack@mydailytake.com](mailto:jack@mydailytake.com).
