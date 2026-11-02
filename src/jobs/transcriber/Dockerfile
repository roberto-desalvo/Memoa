# syntax=docker/dockerfile:1
# Immagini del job di trascrizione, una per motore: runtime .NET, ffmpeg (decodifica della copia Opus),
# librerie native e i soli modelli del motore. Il motore lo sceglie Job__Engine, impostato dal target.
# Contesto di build: src/ (serve anche src/shared):
#   docker build -f src/jobs/transcriber/Dockerfile --target whisper src
#   docker build -f src/jobs/transcriber/Dockerfile --target parakeet src
# Senza --target si costruisce l'ultimo stage (whisper).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY shared/Memoa.Shared.csproj shared/
COPY jobs/transcriber/Memoa.Transcriber.csproj jobs/transcriber/
RUN dotnet restore jobs/transcriber/Memoa.Transcriber.csproj
COPY shared/ shared/
COPY jobs/transcriber/ jobs/transcriber/
RUN dotnet publish jobs/transcriber/Memoa.Transcriber.csproj -c Release -o /app --no-restore
# Whisper.net.Runtime e sherpa-onnx copiano le librerie native di tutte le piattaforme: serve solo linux-x64.
RUN find /app/runtimes -mindepth 1 -maxdepth 1 ! -name linux-x64 -exec rm -rf {} +

# Parakeet TDT 0.6B v3 int8 per sherpa-onnx, dalla release "asr-models" del repository sherpa-onnx,
# verificato con lo SHA-256. ADD non estrae gli archivi remoti: si estraggono qui solo i file del modello.
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS parakeet-model
RUN apt-get update \
    && apt-get install -y --no-install-recommends bzip2 \
    && rm -rf /var/lib/apt/lists/*
ADD --checksum=sha256:5793d0fd397c5778d2cf2126994d58e9d56b1be7c04d13c7a15bb1b4eafb16bf \
    https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8.tar.bz2 \
    /tmp/parakeet.tar.bz2
RUN mkdir /parakeet \
    && tar -xjf /tmp/parakeet.tar.bz2 -C /parakeet --strip-components=1 \
        --wildcards '*/encoder.int8.onnx' '*/decoder.int8.onnx' '*/joiner.int8.onnx' '*/tokens.txt'

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
# libgomp1: OpenMP, richiesto dalle librerie native di whisper.cpp.
RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg libgomp1 \
    && rm -rf /var/lib/apt/lists/*

FROM runtime AS parakeet
COPY --from=parakeet-model /parakeet /models/parakeet
# VAD Silero e Whisper tiny (rilevamento della lingua), fissati e verificati con lo SHA-256. Whisper tiny
# alla stessa revisione del repository Hugging Face del modello large-v3-turbo.
ADD --checksum=sha256:9e2449e1087496d8d4caba907f23e0bd3f78d91fa552479bb9c23ac09cbb1fd6 \
    https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/silero_vad.onnx \
    /models/silero_vad.onnx
ADD --checksum=sha256:be07e048e1e599ad46341c8d2a135645097a538221678b7acdd1b1919c6e1b21 \
    https://huggingface.co/ggerganov/whisper.cpp/resolve/5359861c739e955e79d9a303bcbc70fb988958b1/ggml-tiny.bin \
    /models/ggml-tiny.bin
ENV Job__Engine=parakeet \
    Parakeet__ModelDirectory=/models/parakeet \
    Parakeet__VadModelPath=/models/silero_vad.onnx \
    Parakeet__LanguageModelPath=/models/ggml-tiny.bin
# ADD da URL crea i file con permessi 600: il job gira come $APP_UID, quindi cartelle attraversabili e
# file leggibili da tutti.
RUN chmod -R a+rX /models
WORKDIR /app
COPY --from=build /app .
# Utente non privilegiato predefinito delle immagini .NET; i file temporanei vanno in /tmp.
USER $APP_UID
# La build fallisce se l'utente del job non può leggere i modelli.
RUN test -r "$Parakeet__ModelDirectory/encoder.int8.onnx" \
    && test -r "$Parakeet__VadModelPath" \
    && test -r "$Parakeet__LanguageModelPath"
ENTRYPOINT ["dotnet", "Memoa.Transcriber.dll"]

FROM runtime AS whisper
# Modello large-v3-turbo quantizzato q5_0 (GGML), fissato a una revisione del repository Hugging Face e
# verificato con lo SHA-256. Sta prima del codice: un cambio del worker non rigenera questo livello.
ADD --checksum=sha256:394221709cd5ad1f40c46e6031ca61bce88931e6e088c188294c6d5a55ffa7e2 \
    https://huggingface.co/ggerganov/whisper.cpp/resolve/5359861c739e955e79d9a303bcbc70fb988958b1/ggml-large-v3-turbo-q5_0.bin \
    /models/ggml-large-v3-turbo-q5_0.bin
ENV Job__Engine=whisper \
    Whisper__ModelPath=/models/ggml-large-v3-turbo-q5_0.bin
# ADD da URL crea il file con permessi 600 e la cartella senza garanzie per gli altri utenti: il job gira
# come $APP_UID, quindi cartella attraversabile e file leggibile da tutti.
RUN chmod 755 /models && chmod 644 "$Whisper__ModelPath"
WORKDIR /app
COPY --from=build /app .
# Utente non privilegiato predefinito delle immagini .NET; i file temporanei vanno in /tmp.
USER $APP_UID
# La build fallisce se l'utente del job non può leggere il modello.
RUN test -r "$Whisper__ModelPath"
ENTRYPOINT ["dotnet", "Memoa.Transcriber.dll"]
