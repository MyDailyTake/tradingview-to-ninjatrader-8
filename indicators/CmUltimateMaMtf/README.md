# CM Ultimate MA MTF V4 [ChrisMoody] — NinjaTrader 8

A faithful, **non-repainting** NinjaScript conversion of the TradingView Pine indicator by **ChrisMoody**.

| | |
|---|---|
| **Full write-up** | [mydailytake.com](https://mydailytake.com/cm-ultimate-ma-mtf-chrismoody-ninjatrader-8/) |
| **Original Pine script** | [by ChrisMoody](https://www.tradingview.com/script/bkvQ5hNF) |
| **License** | **MPL-2.0** — inherited from the original. See [LICENSE](LICENSE). |
| **Platform** | NinjaTrader 8 |

## What it does

The workhorse dual moving-average overlay. A fast and a slow MA, each with its own price source (Close, Open, High, Low, HL2, HLC3, OHLC4) and its own choice of eight MA types: SMA, EMA, WMA, Hull, VWMA, RMA (Wilder smoothing), TEMA and Tilson T3 with a configurable factor. So an HMA fast on Close over an SMA slow on HL2 is one indicator, not two. The fast line colors itself by regime, either by its own slope against an N-bar prior value or by whether it sits above or below the slow line, with fast-vs-slow taking precedence when both are on. A translucent fill is drawn between the two lines at a transparency you set. The fast and slow values and an "is fast above slow" flag are exposed as `Series` outputs for strategy use.

Two scope decisions, both noted in the write-up: the Pine multi-timeframe option (`request.security`) is omitted, so both legs run on the chart's timeframe, and the Pine plot offset is omitted, so plots sit on their own bar.

[Read the full write-up →](https://mydailytake.com/cm-ultimate-ma-mtf-chrismoody-ninjatrader-8/)

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
| Fast MA | **Fast MA Length** | Lookback for the Fast MA. |
| Fast MA | **Fast MA Source** | Price source feeding the Fast MA. |
| Fast MA | **Fast MA Type** | Algorithm used to compute the Fast MA. |
| Fast MA | **Fast Tilson T3 Factor** | Smoothing factor for Tilson T3. Pine default 0.7. |
| Fast MA | **Fast MA Color** | Default Fast MA stroke color. |
| Fast MA | **Fast MA Width** | Plot stroke width for the Fast MA. |
| Slow MA | **Show Slow MA** | Render the Slow MA line. |
| Slow MA | **Slow MA Length** | Lookback for the Slow MA. |
| Slow MA | **Slow MA Source** | Price source feeding the Slow MA. |
| Slow MA | **Slow MA Type** | Algorithm used to compute the Slow MA. |
| Slow MA | **Slow Tilson T3 Factor** | Smoothing factor for Tilson T3. Pine default 0.7. |
| Slow MA | **Slow MA Color** | Default Slow MA stroke color. |
| Slow MA | **Slow MA Width** | Plot stroke width for the Slow MA. |
| Plot Settings | **Color Fast by Slope** | Color the Fast MA bullish / bearish based on its own slope (Fast >= Fast[smooth]). |
| Plot Settings | **Slope Smoothing** | Bars-back used in the slope-color comparison. 1 = no smoothing. |
| Plot Settings | **Bullish Color** | Color used when slope-up or Fast above Slow. |
| Plot Settings | **Bearish Color** | Color used when slope-down or Fast below Slow. |
| Plot Settings | **Color Fast vs Slow** | Color the Fast MA based on whether it sits above or below the Slow MA. Takes precedence over slope coloring. |
| Plot Settings | **Show Fill** | Render translucent fill between Fast and Slow MAs. |
| Plot Settings | **Fill Transparency (0-100)** | 0 = solid, 100 = invisible. Pine semantics. |

## Found a bug?

If it doesn't match TradingView exactly, that's probably the **data**, not the code — different feed,
different ticks and session times, different line. If it repaints, errors, or the logic plainly
diverges from the Pine, that's more likely **my port** than ChrisMoody's script, and I'd like to know.
[Open an issue](../../issues) with your NT8 version, instrument, bar type, and data provider. More in
the [main README](../../README.md#found-a-bug).

## Attribution

Adapted from Pine Script™ code originally licensed under **MPL-2.0**. The original Pine Script™ code
is by **ChrisMoody**. Adaptation for NinjaTrader by [jack@mydailytake.com](mailto:jack@mydailytake.com).

## Disclaimer

For educational and informational purposes only. Nothing here is trading advice. Futures trading
involves substantial risk of loss and is not suitable for every investor. See the
[full disclosure](https://mydailytake.com/disclosure/).
