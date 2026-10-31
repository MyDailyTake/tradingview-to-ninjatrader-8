# Nadaraya-Watson: Rational Quadratic Kernel (Non-Repainting) [jdehorty] — NinjaTrader 8

A faithful, **non-repainting** NinjaScript conversion of the TradingView Pine indicator by **jdehorty**.

| | |
|---|---|
| **Full write-up** | [mydailytake.com](https://mydailytake.com/nadaraya-watson-rq-kernel-jdehorty-ninjatrader-8/) |
| **Original Pine script** | [by jdehorty](https://www.tradingview.com/script/AWNvbPRM) |
| **License** | **MPL-2.0** — inherited from the original. See [LICENSE](LICENSE). |
| **Platform** | NinjaTrader 8 |

## What it does

The minimal form of jdehorty's kernel-regression family: a single Rational Quadratic kernel line over close, no envelope, no bands. Kernel weights `w_i = (1 + i² / (alpha × h²))^(−alpha)` are computed once over a fixed window when data loads, so recent bars carry the most weight and older bars decay more gently than they would under a Gaussian kernel. Each bar is then read through that fixed window, which is what makes the line non-repainting. Two color modes: rate of change, which tints the line by its own slope, and Smooth Colors, which runs a second kernel at Lookback minus Lag and tints by the crossover of the two lines, giving earlier signals with slightly less smoothing. The kernel line and an alert stream (+1 on a bullish color change, −1 on a bearish one, 0 otherwise) are exposed as `Series<double>` outputs for strategy use.

The two Pine `alertcondition` calls are not carried over; the alert stream series replaces them.

[Read the full write-up →](https://mydailytake.com/nadaraya-watson-rq-kernel-jdehorty-ninjatrader-8/)

## Non-repainting

Values are computed from **closed-bar data and are not rewritten after the fact**. What you see on a
historical bar is what the indicator would have shown live on that bar.

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
| Kernel Settings | **Lookback Window (h)** | Bandwidth of the Rational Quadratic kernel. Higher = smoother, lower = more reactive. Recommended 3-50. |
| Kernel Settings | **Relative Weighting (alpha)** | Mix between long and short timeframes. Lower = longer timeframes dominate; higher = behaves like a Gaussian kernel. Recommended 0.25-25. |
| Kernel Settings | **Start Regression at Bar** | Skip the first N bars when fitting. Recommended 5-25. |
| Colors | **Smooth Colors** | Use a crossover-based color mechanism for fewer transitions, instead of slope-based. |
| Colors | **Lag** | Lookback delta for the crossover detection kernel. Lower = earlier crossovers. Recommended 1-2. |
| Colors | **Bullish Color** | Line color while the kernel is bullish. |
| Colors | **Bearish Color** | Line color while the kernel is bearish. |

## Found a bug?

If it doesn't match TradingView exactly, that's probably the **data**, not the code — different feed,
different ticks and session times, different line. If it repaints, errors, or the logic plainly
diverges from the Pine, that's more likely **my port** than jdehorty's script, and I'd like to know.
[Open an issue](../../issues) with your NT8 version, instrument, bar type, and data provider. More in
the [main README](../../README.md#found-a-bug).

## Attribution

Adapted from Pine Script™ code originally licensed under **MPL-2.0**. The original Pine Script™ code
is by **jdehorty**. Adaptation for NinjaTrader by [jack@mydailytake.com](mailto:jack@mydailytake.com).

## Disclaimer

For educational and informational purposes only. Nothing here is trading advice. Futures trading
involves substantial risk of loss and is not suitable for every investor. See the
[full disclosure](https://mydailytake.com/disclosure/).
