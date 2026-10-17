# Volume Whale Zones [ChartPrime] — NinjaTrader 8

A faithful, **non-repainting** NinjaScript conversion of the TradingView Pine indicator by **ChartPrime**.

| | |
|---|---|
| **Full write-up** | [mydailytake.com](https://mydailytake.com/volume-whale-zones-chartprime-ninjatrader-8/) |
| **Original Pine script** | [by ChartPrime](https://www.tradingview.com/script/ldmdnX5K) |
| **License** | **MPL-2.0** — inherited from the original. See [LICENSE](LICENSE). |
| **Platform** | NinjaTrader 8 |

## What it does

Finds the highest-volume price level of a 50-bin volume profile over the lookback window (the point of control) and draws it on the price panel as a thick, stepped "hot line". A plain POC line jumps every time the busiest bin changes; this one runs the raw POC through a three-stage filter (an ATR clamp plus a 10-bar EMA, then two snap-on-big-moves stages), so single-bar volume spikes don't move the line and it only steps when the POC has committed to a new level. The price range only expands, so the upper and lower zone bands lock at their extremes until a new high or low prints. A 21-cell HUD at the bottom of the panel shows how far the close sits from the hot zone in ATR units, clamped to ±4. The smoothed hot zone and the upper and lower bands are exposed as `Series<double>` outputs (`HotZone`, `HotZoneUp`, `HotZoneDn`) for strategy use.

Two things NT8 forced: Pine's stepline plot is reproduced with NT8's Square plot style, and the Pine table HUD is drawn with SharpDX.

[Read the full write-up →](https://mydailytake.com/volume-whale-zones-chartprime-ninjatrader-8/)

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
| Parameters | **Lookback** | Window over which the volume profile + range are computed. |
| Parameters | **Show Hot Zone Band** | Render the translucent ATR-extent band around the hot zone line. |
| Parameters | **Label Text Size** | Text size for the deviation oscillator HUD at the bottom of the panel. |
| Colors | **Hot Zone Color** | Color for the hot zone line + ATR band. |
| Colors | **Lookback Color** | Color for the recent-window hot line overlay. |

## Found a bug?

If it doesn't match TradingView exactly, that's probably the **data**, not the code — different feed,
different ticks and session times, different line. If it repaints, errors, or the logic plainly
diverges from the Pine, that's more likely **my port** than ChartPrime's script, and I'd like to know.
[Open an issue](../../issues) with your NT8 version, instrument, bar type, and data provider. More in
the [main README](../../README.md#found-a-bug).

## Attribution

Adapted from Pine Script™ code originally licensed under **MPL-2.0**. The original Pine Script™ code
is by **ChartPrime**. Adaptation for NinjaTrader by [jack@mydailytake.com](mailto:jack@mydailytake.com).
