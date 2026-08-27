ARG BUILD_FROM
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /src

# ReceiptPrinter.Service references sibling projects (Shared, Serial, Network), so the whole solution
# needs to be in the build context - copy it all rather than trying to cherry-pick csproj files.
COPY src/ ./
RUN dotnet publish ReceiptPrinter.Service/ReceiptPrinter.Service.csproj -c Release -o /app

FROM $BUILD_FROM

# .NET's musl runtime needs these - see https://github.com/dotnet/dotnet-docker/blob/main/documentation/known-issues.md
#
# icu-data-full matters for anything but English: Alpine's icu-libs alone ships a stripped-down ICU with
# no real locale/calendar data, so CultureInfo.GetCultureInfo("nl-NL") "succeeds" but DateTime.ToString
# still renders English day/month names - no exception, just silently wrong. icu-data-full is the actual
# CLDR data that makes non-English formatting (Briefing.Language: nl) work.
RUN apk add --no-cache \
    ca-certificates \
    krb5-libs \
    icu-libs \
    icu-data-full \
    libgcc \
    libintl \
    libssl3 \
    libstdc++ \
    zlib

ENV \
    ASPNETCORE_URLS=http://+:8099 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    # Store config/state (ha-config.json, todo-note-store.json, ...) on the add-on's persistent /data
    # folder instead of next to the binary, so it survives image updates.
    RECEIPTPRINTER_CONFIG_DIR=/data

RUN \
    wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh && \
    chmod +x ./dotnet-install.sh && \
    ./dotnet-install.sh --runtime aspnetcore --channel 10.0 --install-dir /usr/share/dotnet && \
    ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet && \
    rm dotnet-install.sh

WORKDIR /app
COPY --from=build-env /app .

# --urls beats ASPNETCORE_URLS in ASP.NET Core's config precedence, so this pins the bind address even
# if something in the HA base image's entrypoint chain drops/overrides the env var.
#
# with-contenv matters here: s6-overlay (the base image's init system) stores env vars set by its
# cont-init.d scripts - including SUPERVISOR_TOKEN - in its own container_environment store, not real
# Docker ENV. A process only sees them if it's launched through with-contenv, which exports that store
# into the process's actual environment. Without it, dotnet never sees SUPERVISOR_TOKEN even though
# `homeassistant_api: true` is set, which is why HomeAssistantConnection.Resolve came back null.
CMD ["with-contenv", "dotnet", "ReceiptPrinter.Service.dll", "--urls", "http://+:8099"]
