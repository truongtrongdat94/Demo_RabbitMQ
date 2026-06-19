FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ConsumerService.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish ConsumerService.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

RUN useradd --create-home --uid 10001 appuser
USER appuser

COPY --from=build --chown=appuser:appuser /app/publish ./

ENTRYPOINT ["dotnet", "ConsumerService.dll"]

