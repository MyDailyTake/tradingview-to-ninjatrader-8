# Chop and explode (ps5) [capissimo] — NinjaTrader 8

A faithful, **non-repainting** NinjaScript conversion of the TradingView Pine indicator by **capissimo**.

| | |
|---|---|
| **Full write-up** | [mydailytake.com](https://mydailytake.com/chop-and-explode-capissimo-ninjatrader-8/) |
| **Original Pine script** | [by capissimo](https://www.tradingview.com/script/L7ydBiKM) |
| **License** | **MPL-2.0** — inherited from the original. See [LICENSE](LICENSE). |
| **Platform** | NinjaTrader 8 |

## What it does

capissimo's Chop and explode (ps5) reframes the classic RSI as a regime detector. Instead of reading 70/30 thresholds for overbought/oversold, this version applies RSI to a minimax-scaled source — the price is normalized to a 0-100 range over a window — and uses 60/40 as the BUY/SELL switches.

[Read the full write-up →](https://mydailytake.com/chop-and-explode-capissimo-ninjatrader-8/)

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
| Parameters | **Parameters** | Price input fed into the cleaning filter and minimax scaler. |
| Signaling | **Signaling** | Render arrow labels on signal flips. |
| Appearance | **Appearance** | Color used for the BUY-regime overlay line and labels. |

## Found a bug?

If it doesn't match TradingView exactly, that's probably the **data**, not the code — different feed,
different ticks and session times, different line. If it repaints, errors, or the logic plainly
diverges from the Pine, that's more likely **my port** than capissimo's script, and I'd like to know.
[Open an issue](../../issues) with your NT8 version, instrument, bar type, and data provider. More in
the [main README](../../README.md#found-a-bug).

## Attribution

Adapted from Pine Script™ code originally licensed under **MPL-2.0**. The original Pine Script™ code
is by **capissimo**. Adaptation for NinjaTrader by [jack@mydailytake.com](mailto:jack@mydailytake.com).
Use of the capissimo name or its adapted code here does not imply endorsement by the original author.

## Disclaimer

For educational and informational purposes only. Nothing here is trading advice. Futures trading
involves substantial risk of loss and is not suitable for every investor. See the
[full disclosure](https://mydailytake.com/disclosure/).
