# Changelog

## 1.0.1

- Pin the HTTP port via `--urls http://+:8099` on the container's `dotnet` command instead of relying
  solely on the `ASPNETCORE_URLS` env var, which could get shadowed by the base image's entrypoint
  chain - it was observed listening on the ASP.NET Core default (`http://localhost:5000`) instead of
  the port `config.yaml` actually publishes.

## 1.0.0

- Initial add-on release: HTTP API (`/print`, `/briefing/trigger`, `/todos/check`) and a daily scheduled
  briefing, printing over the network to `ReceiptPrinter.NetworkSerialService`.
