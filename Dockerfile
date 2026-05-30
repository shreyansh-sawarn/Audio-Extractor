FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY AudioExtractor.Core/AudioExtractor.Core.csproj AudioExtractor.Core/
COPY AudioExtractor.Cli/AudioExtractor.Cli.csproj AudioExtractor.Cli/
RUN dotnet restore AudioExtractor.Cli/AudioExtractor.Cli.csproj

COPY AudioExtractor.Core/ AudioExtractor.Core/
COPY AudioExtractor.Cli/ AudioExtractor.Cli/
RUN dotnet publish AudioExtractor.Cli/AudioExtractor.Cli.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0
RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /work
RUN mkdir -p /input /output
COPY --from=build /app/publish/ /app/

ENTRYPOINT ["dotnet", "/app/audio-extractor.dll"]
CMD ["--help"]
