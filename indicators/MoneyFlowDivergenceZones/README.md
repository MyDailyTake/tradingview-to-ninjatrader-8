# Money Flow Divergence Zones [AlgoAlpha] — NinjaTrader 8

A faithful, **non-repainting** NinjaScript conversion of the TradingView Pine indicator by **AlgoAlpha**.

| | |
|---|---|
| **Full write-up** | [mydailytake.com](https://mydailytake.com/money-flow-divergence-zones-algoalpha-ninjatrader-8/) |
| **Original Pine script** | [by AlgoAlpha](https://www.tradingview.com/script/uAu2PvQM) |
| **License** | **MPL-2.0** — inherited from the original. See [LICENSE](LICENSE). |
| **Platform** | NinjaTrader 8 |

## What it does

A Money Flow Index oscillator, smoothed by an HMA, becomes a divergence detector: pivot lows and highs on the oscillator are paired with price extremes, and a regular bullish or bearish divergence fires when price and the oscillator disagree. Each divergence draws a liquidity zone on the price panel, and zones are removed once price sweeps through them.

[Read the full write-up →](https://mydailytake.com/money-flow-divergence-zones-algoalpha-ninjatrader-8/)

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
| Settings | **Settings** | MFI length and HMA smoothing for the divergence oscillator. |
| Divergence | **Divergence** | Pivot lookback and the min/max bar distance for a valid divergence. |
| Management | **Management** | Sweep type and how liquidity zones are invalidated and removed. |
| Appearance | **Appearance** | Zone and signal coloring. |

## Found a bug?

If it doesn't match TradingView exactly, that's probably the **data**, not the code — different feed,
different ticks and session times, different line. If it repaints, errors, or the logic plainly
diverges from the Pine, that's more likely **my port** than AlgoAlpha's script, and I'd like to know.
[Open an issue](../../issues) with your NT8 version, instrument, bar type, and data provider. More in
the [main README](../../README.md#found-a-bug).

## Attribution

Adapted from Pine Script™ code originally licensed under **MPL-2.0**. The original Pine Script™ code
is by **AlgoAlpha**. Adaptation for NinjaTrader by [jack@mydailytake.com](mailto:jack@mydailytake.com).
