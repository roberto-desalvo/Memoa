# syntax=docker/dockerfile:1
# Immagini del job di trascrizione, una per motore: runtime .NET, ffmpeg (decodifica della copia Opus),
# worker con le sole librerie native del motore e i soli modelli del motore. Il motore lo sceglie
# Job__Engine, impostato dal target; quale immagine usare lo sceglie transcriberEngine nel deploy.
# Contesto di build: src/ (serve anche src/shared):
#   docker build -f src/jobs/transcriber/Dockerfile --target whisper src
#   docker build -f src/jobs/transcriber/Dockerfile --target parakeet src
#   docker build -f src/jobs/transcriber/Dockerfile --target faster-whisper src
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

# Worker per motore, con le sole librerie native che usa: vanno tolte prima della copia nell'immagine
# finale, perché un file cancellato in un livello successivo resta nei precedenti. Parakeet usa sia
# sherpa-onnx sia Whisper.net (Whisper tiny per la lingua) e copia direttamente da build.
# Whisper: senza sherpa-onnx e ONNX Runtime (circa 30 MB).
FROM build AS app-whisper
RUN find /app \( -name 'libsherpa-onnx*' -o -name 'libonnxruntime*' \) -delete

# faster-whisper: senza sherpa-onnx né whisper.cpp; CTranslate2 e ONNX Runtime arrivano con le wheel Python.
FROM build AS app-faster-whisper
RUN find /app \( -name 'libsherpa-onnx*' -o -name 'libonnxruntime*' -o -name 'libwhisper*' -o -name 'libggml*' \) -delete

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

FROM runtime AS faster-whisper
# Python con faster-whisper in un virtualenv, dipendenze tutte fissate in requirements.txt.
RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 python3-venv \
    && rm -rf /var/lib/apt/lists/*
COPY jobs/transcriber/FasterWhisper/requirements.txt /tmp/requirements.txt
RUN python3 -m venv /opt/faster-whisper \
    && /opt/faster-whisper/bin/pip install --no-cache-dir -r /tmp/requirements.txt \
    && rm /tmp/requirements.txt
# Modello large-v3-turbo in formato CTranslate2 (float16, quantizzato int8 al caricamento), dal repository
# Hugging Face deepdml/faster-whisper-large-v3-turbo-ct2 (MIT) a una revisione fissata, ogni file
# verificato con lo SHA-256.
ARG FW_MODEL=https://huggingface.co/deepdml/faster-whisper-large-v3-turbo-ct2/resolve/4df90f75321148c3a29a9e2351b7ddf8f5b115a8
ADD --checksum=sha256:e76620f83d5f5b69efd3d87e3dc180c1bd21df9fbebacfd4335e5e1efcc018da ${FW_MODEL}/model.bin /models/faster-whisper-large-v3-turbo/model.bin
ADD --checksum=sha256:b0253ea6c0d3bea6b1e19e91a02acfd3b53f4467362efcb5a3e6b16c9b3a9b7e ${FW_MODEL}/config.json /models/faster-whisper-large-v3-turbo/config.json
ADD --checksum=sha256:7ccc62c6f2765af1f3b46c00c9b5894426835a05021c8b9c01eecb6dfb542711 ${FW_MODEL}/preprocessor_config.json /models/faster-whisper-large-v3-turbo/preprocessor_config.json
ADD --checksum=sha256:297b13372ac43916285644fb9687add3cc62ee2a1adb60da3dc25cc94c1871fd ${FW_MODEL}/tokenizer.json /models/faster-whisper-large-v3-turbo/tokenizer.json
ADD --checksum=sha256:c69260f2ab26d659b7c398f9a2b2b48ed0df16c3b47d7326782fd9cba71690c1 ${FW_MODEL}/vocabulary.json /models/faster-whisper-large-v3-turbo/vocabulary.json
ENV Job__Engine=faster-whisper \
    FasterWhisper__PythonPath=/opt/faster-whisper/bin/python \
    FasterWhisper__ModelDirectory=/models/faster-whisper-large-v3-turbo
# ADD da URL crea i file con permessi 600: il job gira come $APP_UID.
RUN chmod -R a+rX /models
WORKDIR /app
COPY --from=app-faster-whisper /app .
# Utente non privilegiato predefinito delle immagini .NET; i file temporanei vanno in /tmp.
USER $APP_UID
# La build fallisce se l'utente del job non può leggere il modello o importare faster-whisper.
RUN test -r "$FasterWhisper__ModelDirectory/model.bin" \
    && test -r /app/FasterWhisper/transcribe.py \
    && "$FasterWhisper__PythonPath" -I -c "import faster_whisper"
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
COPY --from=app-whisper /app .
# Utente non privilegiato predefinito delle immagini .NET; i file temporanei vanno in /tmp.
USER $APP_UID
# La build fallisce se l'utente del job non può leggere il modello.
RUN test -r "$Whisper__ModelPath"
ENTRYPOINT ["dotnet", "Memoa.Transcriber.dll"]
