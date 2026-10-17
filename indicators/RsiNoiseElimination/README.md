# RSI With Noise Elimination Technology [alexgrover] — NinjaTrader 8

A faithful, **non-repainting** NinjaScript conversion of the TradingView Pine indicator by **alexgrover**.

| | |
|---|---|
| **Full write-up** | [mydailytake.com](https://mydailytake.com/rsi-noise-elimination-technology-alexgrover-ninjatrader-8/) |
| **Original Pine script** | [by alexgrover](https://www.tradingview.com/script/Rso1v0W9) |
| **License** | **MPL-2.0** — inherited from the original. See [LICENSE](LICENSE). |
| **Platform** | NinjaTrader 8 |

## What it does

John Ehlers' Noise Elimination Technology applied to a normalized RSI. The NET line is a rolling Kendall rank-correlation of the underlying RSI: absolute values close to 1 signal a strong directional move, while values near zero signal noise. Two plots — the NET line and the normalized RSI — let you separate real momentum from chop.

[Read the full write-up →](https://mydailytake.com/rsi-noise-elimination-technology-alexgrover-ninjatrader-8/)

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
| Indicator Setup | **Indicator Setup** | RSI length and the NET correlation window. |

## Found a bug?

If it doesn't match TradingView exactly, that's probably the **data**, not the code — different feed,
different ticks and session times, different line. If it repaints, errors, or the logic plainly
diverges from the Pine, that's more likely **my port** than alexgrover's script, and I'd like to know.
[Open an issue](../../issues) with your NT8 version, instrument, bar type, and data provider. More in
the [main README](../../README.md#found-a-bug).

## Attribution

Adapted from Pine Script™ code originally licensed under **MPL-2.0**. The original Pine Script™ code
is by **alexgrover**. Adaptation for NinjaTrader by [jack@mydailytake.com](mailto:jack@mydailytake.com).

## Disclaimer

For educational and informational purposes only. Nothing here is trading advice. Futures trading
involves substantial risk of loss and is not suitable for every investor. See the
[full disclosure](https://mydailytake.com/disclosure/).
