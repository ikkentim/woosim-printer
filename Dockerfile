ARG BUILD_FROM
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /src

# ReceiptPrinter.Service references sibling projects (Shared, Serial, Network), so the whole solution
# needs to be in the build context - copy it all rather than trying to cherry-pick csproj files.
COPY src/ ./
RUN dotnet publish ReceiptPrinter.Service/ReceiptPrinter.Service.csproj -c Release -o /app

FROM $BUILD_FROM

# .NET's musl runtime needs these - see https://github.com/dotnet/dotnet-docker/blob/main/documentation/known-issues.md
RUN apk add --no-cache \
    ca-certificates \
    krb5-libs \
    icu-libs \
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

CMD ["dotnet", "ReceiptPrinter.Service.dll"]
