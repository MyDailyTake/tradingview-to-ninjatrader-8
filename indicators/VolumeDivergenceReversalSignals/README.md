# Volume Divergence Reversal Signals [AlgoAlpha] — NinjaTrader 8

A faithful, **non-repainting** NinjaScript conversion of the TradingView Pine indicator by **AlgoAlpha**.

| | |
|---|---|
| **Full write-up** | [mydailytake.com](https://mydailytake.com/volume-divergence-reversal-signals-algoalpha-ninjatrader-8/) |
| **Original Pine script** | [by AlgoAlpha](https://www.tradingview.com/script/JSULpbV7) |
| **License** | **MPL-2.0** — inherited from the original. See [LICENSE](LICENSE). |
| **Platform** | NinjaTrader 8 |

## What it does

Normalizes volume against its highest value over the Normalization Length window (default 1,000 bars) so every bar lands on a 0 to 100 scale that reads the same across instruments and timeframes, then looks for divergences between that normalized volume and price at confirmed volume pivots. A pivot high is confirmed when the bar Pivot Lookback Right bars back is the highest of its window (defaults 21 left, 5 right). At each new pivot the indicator compares against the previous one: a bullish divergence is a lower price low with a higher volume peak (buyers absorbing), a bearish divergence is a higher price high with a lower volume peak (a rally on thinning participation). Two pivots only count when they sit between Min and Max Bars Between Pivots apart (defaults 5 to 60). Each divergence draws a connector line between the two pivots on both the price panel and the volume panel plus a triangle marker on price, and the volume columns are coloured on a purple-to-yellow gradient by normalized value. The normalized volume and the bullish and bearish divergence flags are exposed as `Series<>` outputs for strategy use.

[Read the full write-up →](https://mydailytake.com/volume-divergence-reversal-signals-algoalpha-ninjatrader-8/)

## Non-repainting

Values are computed from **closed-bar data and are not rewritten after the fact**. What you see on a
historical bar is what the indicator would have shown live on that bar. Pivots are confirmed Pivot Lookback Right bars after the pivot bar, and the divergence check runs on that closed-bar pivot stream.

## Install

1. Download the `.cs` file in this folder.
2. Copy it to `Documents\NinjaTrader 8\bin\Custom\Indicators\`.
3. In NinjaTrader: **Control Center → New → NinjaScript Editor**, press **F5** to compile.
4. Add the indicator to a chart.

> Prefer one-click? The write-up offers the same indicator as a NinjaScript `.zip` for
> **Tools → Import → NinjaScript Add-On**.

## Settings

| Group | Setting | Description |
|---|---|---|
| Volume | **Normalization Length** | Lookback used to normalize volume into a 0-100 scale. |
| Volume | **Calculate Divergence** | Detect regular bullish/bearish divergences between price and normalized volume. |
| Divergence | **Pivot Lookback Left** | Bars to the left of a candidate pivot that must be lower for confirmation. |
| Divergence | **Pivot Lookback Right** | Bars to the right of a candidate pivot that must be lower for confirmation. |
| Divergence | **Max Bars Between Pivots** | Maximum spacing between two consecutive pivots used in divergence checks. |
| Divergence | **Min Bars Between Pivots** | Minimum spacing between two consecutive pivots used in divergence checks. |
| Appearance | **Bullish Color** | Color used for bullish divergence visuals. |
| Appearance | **Bearish Color** | Color used for bearish divergence visuals. |

## Found a bug?

If it doesn't match TradingView exactly, that's probably the **data**, not the code — different feed,
different ticks and session times, different line. If it repaints, errors, or the logic plainly
diverges from the Pine, that's more likely **my port** than AlgoAlpha's script, and I'd like to know.
[Open an issue](../../issues) with your NT8 version, instrument, bar type, and data provider. More in
the [main README](../../README.md#found-a-bug).

## Attribution

Adapted from Pine Script™ code originally licensed under **MPL-2.0**. The original Pine Script™ code
is by **AlgoAlpha**. Adaptation for NinjaTrader by [jack@mydailytake.com](mailto:jack@mydailytake.com).

## Disclaimer

For educational and informational purposes only. Nothing here is trading advice. Futures trading
involves substantial risk of loss and is not suitable for every investor. See the
[full disclosure](https://mydailytake.com/disclosure/).
