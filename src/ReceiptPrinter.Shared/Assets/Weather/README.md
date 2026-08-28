# Weather glyphs

72×72 1-bit bitmaps (binary PBM "P4"), one per Home Assistant weather condition, embedded into
`ReceiptPrinter.Shared` and printed via ESC/POS bit-image by `EscPosEncoder`. Loaded by
[`WeatherIcon`](../../Receipts/WeatherIcon.cs), keyed by the weather entity's state (`-` → `_` in the
filename, e.g. `clear-night` → `clear_night.pbm`). Also reachable from `ReceiptMarkdown` as `![name]`.

## Source & license

Rasterised from [Material Design Icons](https://pictogrammers.com/library/mdi/) (`weather-*`), which
are under the [Pictogrammers Free License](https://pictogrammers.com/docs/general/license/) (Apache
2.0). Mapping:

| condition | MDI icon |
|---|---|
| `sunny` | `weather-sunny` |
| `clear-night` | `weather-night` |
| `partlycloudy` | `weather-partly-cloudy` |
| `cloudy` | `weather-cloudy` |
| `rainy` | `weather-rainy` |
| `pouring` | `weather-pouring` |
| `snowy` | `weather-snowy` |
| `snowy-rainy` | `weather-snowy-rainy` |
| `fog` | `weather-fog` |
| `lightning` | `weather-lightning` |
| `lightning-rainy` | `weather-lightning-rainy` |
| `hail` | `weather-hail` |
| `windy` | `weather-windy` |
| `windy-variant` | `weather-windy-variant` |
| `exceptional` | `weather-cloudy-alert` |

## Regenerating

Per icon, with ImageMagick and the MDI SVG:

```bash
magick -background white -density 384 weather-sunny.svg \
  -resize 72x72 -gravity center -extent 72x72 -threshold 55% sunny.pbm
```
