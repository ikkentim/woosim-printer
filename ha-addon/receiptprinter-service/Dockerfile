# Build context must be the repo root (not this folder) since the Service project references sibling
# projects under src/. Build it explicitly with:
#
#   docker build -f ha-addon/receiptprinter-service/Dockerfile -t <your-registry>/receiptprinter-service:latest .
#   docker push <your-registry>/receiptprinter-service:latest
#
# then point config.yaml's "image" at that tag. See ha-addon/receiptprinter-service/README.md.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ .
RUN dotnet publish ReceiptPrinter.Service/ReceiptPrinter.Service.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# HA add-ons always mount a persistent /data folder for the add-on - store config/state there
# (ha-config.json, reminders-config.json, briefing-config.json, todo.txt, todo-note-store.json) so it
# survives image rebuilds/updates instead of living inside the container.
ENV RECEIPTPRINTER_CONFIG_DIR=/data
ENV ASPNETCORE_URLS=http://0.0.0.0:8099

EXPOSE 8099
ENTRYPOINT ["dotnet", "ReceiptPrinter.Service.dll"]
