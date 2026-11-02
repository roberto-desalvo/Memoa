"""Trascrizione con faster-whisper (CTranslate2 su CPU), avviata da FasterWhisperEngine.

Richiesta in JSON su stdin, risultato in JSON su stdout. L'audio arriva già decodificato da ffmpeg in
PCM float32 mono a 16 kHz. Su stderr solo il tipo dell'eccezione: il testo non deve finire nei log.

Codici di uscita: 0 riuscita, 3 caricamento del modello non riuscito, 1 ogni altro errore.
"""

import json
import sys

EXIT_FAILED = 1
EXIT_MODEL_LOAD_FAILED = 3


def main() -> int:
    request = json.load(sys.stdin)

    import numpy as np
    from faster_whisper import WhisperModel

    try:
        model = WhisperModel(
            request["modelDirectory"],
            device="cpu",
            compute_type="int8",
            cpu_threads=request["threads"],
        )
    except Exception as ex:  # noqa: BLE001 - qualsiasi errore di caricamento ha lo stesso esito
        print(type(ex).__name__, file=sys.stderr)
        return EXIT_MODEL_LOAD_FAILED

    audio = np.fromfile(request["pcmPath"], dtype="<f4")

    # Rilevamento sulla prima finestra di 30 secondi, scegliendo solo tra le lingue supportate.
    _, _, probabilities = model.detect_language(audio=audio)
    candidates = [(language, probability) for language, probability in probabilities
                  if language in request["languages"]]
    detected, probability = max(candidates, key=lambda candidate: candidate[1], default=(None, 0.0))
    language = detected if detected is not None and probability >= request["probabilityThreshold"] \
        else request["fallbackLanguage"]

    # La VAD Silero inclusa salta i silenzi; il prompt spinge Whisper a usare la punteggiatura.
    segments, _ = model.transcribe(
        audio,
        language=language,
        beam_size=request["beamSize"],
        initial_prompt=request["prompts"].get(language) or None,
        vad_filter=True,
    )
    text = "".join(segment.text for segment in segments).strip()

    json.dump({
        "text": text,
        "language": language,
        "detectedLanguage": detected,
        "languageProbability": probability,
    }, sys.stdout, ensure_ascii=False)
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as ex:  # noqa: BLE001 - l'esito dipende solo dal codice di uscita
        print(type(ex).__name__, file=sys.stderr)
        sys.exit(EXIT_FAILED)
