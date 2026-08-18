# SuperTrend Cluster [Zeiierman] — NinjaTrader 8

A faithful, **non-repainting** NinjaScript conversion of the TradingView Pine indicator by **Zeiierman**.

| | |
|---|---|
| **Full write-up** | [mydailytake.com](https://mydailytake.com/supertrend-cluster-zeiierman-ninjatrader-8/) |
| **Original Pine script** | [by Zeiierman](https://www.tradingview.com/script/r8j7m88J) |
| **License** | **CC-BY-NC-SA-4.0** — inherited from the original. See [LICENSE](LICENSE). |
| **Platform** | NinjaTrader 8 |

## What it does

One SuperTrend is a single opinion. Zeiierman's SuperTrend Cluster is a committee — five SuperTrend members, each with its own ATR length, factor, MA-smoothing type, MA length, and weight — and it trades the **weighted vote** instead of one line.

[Read the full write-up →](https://mydailytake.com/supertrend-cluster-zeiierman-ninjatrader-8/)

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
| Cluster Engine | **Cluster Engine** | Consensus threshold (how many members must agree) and which base member anchors the signal. |
| SuperTrend 1–5 | **SuperTrend 1–5** | The five independent members — ATR length, factor, MA type/length, and vote weight each. |
| Visual Analytics | **Visual Analytics** | Dynamic bar coloring, cluster labels, and base-flip dots. |
| Cloud Fill | **Cloud Fill** | Shaded consensus band. |

## Found a bug?

If it doesn't match TradingView exactly, that's probably the **data**, not the code — different feed,
different ticks and session times, different line. If it repaints, errors, or the logic plainly
diverges from the Pine, that's more likely **my port** than Zeiierman's script, and I'd like to know.
[Open an issue](../../issues) with your NT8 version, instrument, bar type, and data provider. More in
the [main README](../../README.md#found-a-bug).

## Attribution

Adapted from Pine Script™ code originally licensed under **CC-BY-NC-SA-4.0**. The original Pine Script™ code
is by **Zeiierman**. Adaptation for NinjaTrader by [jack@mydailytake.com](mailto:jack@mydailytake.com).
