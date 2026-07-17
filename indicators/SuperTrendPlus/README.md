# SuperTrend+ [Electrified] — NinjaTrader 8

A faithful, **non-repainting** NinjaScript conversion of the TradingView Pine indicator
**SuperTrend+**, originally by **Electrified (electrifiedtrading)**.

| | |
|---|---|
| **Full write-up** | [mydailytake.com — SuperTrend+ conversion](https://mydailytake.com/tradingview-supertrend-plus-electrified-ninjatrader-8/) |
| **Original Pine script** | [SuperTrend+ by Electrified](https://www.tradingview.com/script/smXJk7s5-SuperTrend/) |
| **Dependent library** | [SupportResistanceAndTrend](https://www.tradingview.com/script/p7CZyF5N-SupportResitanceAndTrend/) (also © Electrified, MPL-2.0) |
| **License** | **MPL-2.0** — inherited from the original. See [LICENSE](LICENSE). |
| **Platform** | NinjaTrader 8 |

---

## What it does

SuperTrend+ plots an ATR-based trailing trend line that flips between an up-trend and a
down-trend state. It extends the classic SuperTrend in three ways:

- **Outlier filtering** — a standard-deviation limit (`Max Deviation`) discards True Range
  spikes before they're averaged into the ATR, so a single violent bar doesn't blow the bands out.
- **Reversal confirmation** — a flip only counts once `Closed Bars` closed bars have exceeded the
  SuperTrend value, which cuts the whipsaw reversals the original fires on wicks.
- **Highlighting** — a translucent fill between the OHLC4 midline and the active trend line, so
  the trend state reads at a glance.

Two plots are exposed (`UpTrend`, `DnTrend`) so you can reference the trend line from other
NinjaScript.

## Non-repainting

Trend state is computed from **closed-bar data and never rewritten once a bar closes**. What you
see on a historical bar is what the indicator would have shown live on that bar.

## Install

1. Download **[`SuperTrendPlus.cs`](SuperTrendPlus.cs)**.
2. Copy it to:
   `Documents\NinjaTrader 8\bin\Custom\Indicators\`
3. In NinjaTrader: **Control Center → New → NinjaScript Editor**, then press **F5** to compile.
4. Add **SuperTrendPlus** to a chart from the Indicators list.

> Prefer a one-click import? The write-up linked above ships the same indicator as a NinjaScript
> `.zip` archive you can bring in via **Tools → Import → NinjaScript Add-On**.

## Settings

| Group | Setting | Description |
|---|---|---|
| Average True Range | **Mode** | Averaging function used to smooth the True Range into an ATR value. |
| Average True Range | **Period** | Number of bars used when computing the ATR value. |
| Average True Range | **Multiplier** | Multiplier applied to the ATR when defining the upper / lower bands. |
| Average True Range | **Max Deviation** | Standard-deviation limit for filtering True Range outliers before averaging. `0` disables the filter. |
| Confirmation | **Closed Bars** | Number of closed bars that must exceed the SuperTrend value before a reversal is confirmed. |
| Display | **Show Buy / Sell Labels** | Draw text `Buy` / `Sell` labels at reversals, in addition to the reversal dot. |
| Display | **Highlighter On** | Draw translucent fill between the OHLC4 midline and the active trend line. |
| Display | **Highlighter Opacity (%)** | Opacity of the highlighter fill, 1–100. |
| Display | **Text Size** | Font size for the Buy / Sell labels. |
| Colors | **Up / Dn / Unconfirmed** | Trend line colors, including the pending-reversal state. |

## Found a bug?

If it doesn't match TradingView exactly, that's probably the **data**, not the code — different
feed, different ticks and session times, different line. If it repaints, errors, or the logic
plainly diverges from the Pine, that's more likely **my port** than Electrified's script, and I'd
like to know. [Open an issue](../../issues) with your NT8 version, instrument, bar type, and data
provider. More detail in the [main README](../../README.md#found-a-bug).

## Attribution

This NinjaTrader 8 script is adapted from Pine Script™ code originally licensed under the
**Mozilla Public License 2.0**. The original Pine Script™ code is by **Electrified
(electrifiedtrading)**. Adaptation for NinjaTrader by [jack@mydailytake.com](mailto:jack@mydailytake.com).

Use of the Electrified name or its adapted code here does not imply endorsement by the original
author. The adapted code is provided under the terms of the MPL-2.0 — full text in
[LICENSE](LICENSE), summary at <https://mozilla.org/MPL/2.0/>.

## Disclaimer

For educational and informational purposes only. Nothing here is trading advice. Futures trading
involves substantial risk of loss and is not suitable for every investor. See the
[full disclosure](https://mydailytake.com/disclosure/).
