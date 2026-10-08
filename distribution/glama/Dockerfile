FROM mcr.microsoft.com/dotnet/sdk:10.0

RUN dotnet tool install StockSharp.DesktopDriver.Mcp --version 1.0.1 --tool-path /opt/mcp \
    && mkdir -p /data/artifacts \
    && printf '{"applications":[]}\n' > /data/catalogue.json

ENV STOCKSHARP_UI_CATALOGUE=/data/catalogue.json \
    STOCKSHARP_UI_ARTIFACTS=/data/artifacts \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1

WORKDIR /data
ENTRYPOINT ["/opt/mcp/desktop-driver-mcp"]
