# Immagine del job di normalizzazione: runtime .NET e ffmpeg (con libopus), nient'altro.
# Contesto di build: src/ (serve anche src/shared):
#   docker build -f src/jobs/normalizer/Dockerfile src
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY shared/Memoa.Shared.csproj shared/
COPY jobs/normalizer/Memoa.Normalizer.csproj jobs/normalizer/
RUN dotnet restore jobs/normalizer/Memoa.Normalizer.csproj
COPY shared/ shared/
COPY jobs/normalizer/ jobs/normalizer/
RUN dotnet publish jobs/normalizer/Memoa.Normalizer.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0
RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
# Utente non privilegiato predefinito delle immagini .NET; i file temporanei vanno in /tmp.
USER $APP_UID
ENTRYPOINT ["dotnet", "Memoa.Normalizer.dll"]
