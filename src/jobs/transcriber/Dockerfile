# syntax=docker/dockerfile:1
# Immagine del job di trascrizione: runtime .NET, ffmpeg (decodifica della copia Opus), runtime nativo
# di whisper.cpp per CPU e pesi del modello.
# Contesto di build: src/ (serve anche src/shared):
#   docker build -f src/jobs/transcriber/Dockerfile src
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY shared/Memoa.Shared.csproj shared/
COPY jobs/transcriber/Memoa.Transcriber.csproj jobs/transcriber/
RUN dotnet restore jobs/transcriber/Memoa.Transcriber.csproj
COPY shared/ shared/
COPY jobs/transcriber/ jobs/transcriber/
RUN dotnet publish jobs/transcriber/Memoa.Transcriber.csproj -c Release -o /app --no-restore
# Whisper.net.Runtime copia le librerie native di tutte le piattaforme: serve solo linux-x64.
RUN find /app/runtimes -mindepth 1 -maxdepth 1 ! -name linux-x64 -exec rm -rf {} +

FROM mcr.microsoft.com/dotnet/runtime:10.0
# libgomp1: OpenMP, richiesto dalle librerie native di whisper.cpp.
RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg libgomp1 \
    && rm -rf /var/lib/apt/lists/*
# Modello large-v3-turbo quantizzato q5_0 (GGML), fissato a una revisione del repository Hugging Face e
# verificato con lo SHA-256. Sta prima del codice: un cambio del worker non rigenera questo livello.
ADD --checksum=sha256:394221709cd5ad1f40c46e6031ca61bce88931e6e088c188294c6d5a55ffa7e2 --chmod=644 \
    https://huggingface.co/ggerganov/whisper.cpp/resolve/5359861c739e955e79d9a303bcbc70fb988958b1/ggml-large-v3-turbo-q5_0.bin \
    /models/ggml-large-v3-turbo-q5_0.bin
ENV Whisper__ModelPath=/models/ggml-large-v3-turbo-q5_0.bin
WORKDIR /app
COPY --from=build /app .
# Utente non privilegiato predefinito delle immagini .NET; i file temporanei vanno in /tmp.
USER $APP_UID
ENTRYPOINT ["dotnet", "Memoa.Transcriber.dll"]
