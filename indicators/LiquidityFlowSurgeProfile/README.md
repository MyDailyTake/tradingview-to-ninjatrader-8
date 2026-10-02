# Liquidity Flow Surge Profile [ChartPrime] — NinjaTrader 8

A faithful, **non-repainting** NinjaScript conversion of the TradingView Pine indicator by **ChartPrime**.

| | |
|---|---|
| **Full write-up** | [mydailytake.com](https://mydailytake.com/liquidity-flow-surge-profile-chartprime-ninjatrader-8/) |
| **Original Pine script** | [by ChartPrime](https://www.tradingview.com/script/EQSY3xU0) |
| **License** | **MPL-2.0** — inherited from the original. See [LICENSE](LICENSE). |
| **Platform** | NinjaTrader 8 |

## What it does

A 25-bin volume distribution rendered as a thermal heatmap on the price panel: each bar's volume is attributed to the price bin its close falls into, and bin opacity scales with how much volume landed there. On top of that, a liquidation-surge engine flags bars whose volume spikes past a share of their recent peak, so you can see where volume is concentrated and when a surge hits.

[Read the full write-up →](https://mydailytake.com/liquidity-flow-surge-profile-chartprime-ninjatrader-8/)

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
| Profile | **Profile** | Lookback window (Short / Medium / Long) and the volume-heatmap bin behavior. |
| Liquidation Bubbles | **Liquidation Bubbles** | Volume-surge threshold and how liquidation events are marked. |
| Dashboard | **Dashboard** | On-chart readout toggles and styling. |

## Found a bug?

If it doesn't match TradingView exactly, that's probably the **data**, not the code — different feed,
different ticks and session times, different line. If it repaints, errors, or the logic plainly
diverges from the Pine, that's more likely **my port** than ChartPrime's script, and I'd like to know.
[Open an issue](../../issues) with your NT8 version, instrument, bar type, and data provider. More in
the [main README](../../README.md#found-a-bug).

## Attribution

Adapted from Pine Script™ code originally licensed under **MPL-2.0**. The original Pine Script™ code
is by **ChartPrime**. Adaptation for NinjaTrader by [jack@mydailytake.com](mailto:jack@mydailytake.com).
