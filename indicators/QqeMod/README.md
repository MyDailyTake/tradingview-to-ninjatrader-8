# QQE MOD [Mihkel00] — NinjaTrader 8

A faithful, **non-repainting** NinjaScript conversion of the TradingView Pine indicator by **Mihkel00**.

| | |
|---|---|
| **Full write-up** | [mydailytake.com](https://mydailytake.com/qqe-mod-mihkel00-ninjatrader-8/) |
| **Original Pine script** | [by Mihkel00](https://www.tradingview.com/script/TpUW4muw) |
| **License** | **MPL-2.0** — inherited from the original. See [LICENSE](LICENSE). |
| **Platform** | NinjaTrader 8 |

## What it does

Two QQE (Quantitative Qualitative Estimation) oscillators run in parallel, with a Bollinger band on the primary trend line acting as a zero-cross filter. The secondary RSI plots as a histogram, and a bar only lights up Up or Down when both QQEs agree — the secondary is outside its threshold and the primary is outside the band. Bars inside the threshold stay hidden.

[Read the full write-up →](https://mydailytake.com/qqe-mod-mihkel00-ninjatrader-8/)

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
| Primary QQE | **Primary QQE** | RSI length, smoothing, and QQE factor for the primary oscillator. |
| Secondary QQE | **Secondary QQE** | Length, smoothing, and threshold for the secondary histogram. |
| Bollinger Bands | **Bollinger Bands** | Length and multiplier for the zero-cross filter on the primary trend line. |
| Display | **Display** | Colors and plot visibility. |

## Found a bug?

If it doesn't match TradingView exactly, that's probably the **data**, not the code — different feed,
different ticks and session times, different line. If it repaints, errors, or the logic plainly
diverges from the Pine, that's more likely **my port** than Mihkel00's script, and I'd like to know.
[Open an issue](../../issues) with your NT8 version, instrument, bar type, and data provider. More in
the [main README](../../README.md#found-a-bug).

## Attribution

Adapted from Pine Script™ code originally licensed under **MPL-2.0**. The original Pine Script™ code
is by **Mihkel00**. Adaptation for NinjaTrader by [jack@mydailytake.com](mailto:jack@mydailytake.com).

## Disclaimer

For educational and informational purposes only. Nothing here is trading advice. Futures trading
involves substantial risk of loss and is not suitable for every investor. See the
[full disclosure](https://mydailytake.com/disclosure/).
