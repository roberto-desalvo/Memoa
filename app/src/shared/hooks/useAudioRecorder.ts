import { useState, useRef, useEffect } from 'react';

const MAX_DURATION_MS = 300000; // 300 secondi in ms

export type AudioMetadata = {
  mimeType: string;
  durationMs: number;
  sizeBytes: number;
};

export const useAudioRecorder = () => {
  const [isRecording, setIsRecording] = useState(false);
  const [elapsedSeconds, setElapsedSeconds] = useState(0);
  const [blob, setBlob] = useState<Blob | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [audioMetadata, setAudioMetadata] = useState<AudioMetadata | null>(null);

  const mediaRecorderRef = useRef<MediaRecorder | null>(null);
  const audioChunksRef = useRef<BlobPart[]>([]);
  const timerIntervalRef = useRef<NodeJS.Timeout | null>(null);
  const startTimeRef = useRef<number>(0);

  // Cleanup al unmount
  useEffect(() => {
    return () => {
      if (timerIntervalRef.current) {
        clearInterval(timerIntervalRef.current);
      }
      if (mediaRecorderRef.current && mediaRecorderRef.current.state === 'recording') {
        mediaRecorderRef.current.stop();
      }
    };
  }, []);

  const startRecording = async () => {
    try {
      setError(null);
      audioChunksRef.current = [];
      setElapsedSeconds(0);

      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      
      // Scegli il mime type supportato
      const mimeType = MediaRecorder.isTypeSupported('audio/webm')
        ? 'audio/webm'
        : MediaRecorder.isTypeSupported('audio/mp4')
        ? 'audio/mp4'
        : 'audio/wav';

      const mediaRecorder = new MediaRecorder(stream, { mimeType });
      mediaRecorderRef.current = mediaRecorder;
      startTimeRef.current = Date.now();

      mediaRecorder.ondataavailable = (event) => {
        if (event.data.size > 0) {
          audioChunksRef.current.push(event.data);
        }
      };

      mediaRecorder.onstop = () => {
        const audioBlob = new Blob(audioChunksRef.current, { type: mimeType });
        const durationMs = Date.now() - startTimeRef.current;
        
        setBlob(audioBlob);
        setAudioMetadata({
          mimeType,
          durationMs,
          sizeBytes: audioBlob.size,
        });

        // Ferma lo stream
        stream.getTracks().forEach(track => track.stop());
      };

      mediaRecorder.onerror = (event) => {
        setError(`Errore registrazione: ${event.error}`);
        stream.getTracks().forEach(track => track.stop());
      };

      mediaRecorder.start();
      setIsRecording(true);

      // Timer per update UI ogni 100ms
      timerIntervalRef.current = setInterval(() => {
        const elapsed = Math.floor((Date.now() - startTimeRef.current) / 1000);
        setElapsedSeconds(elapsed);

        // Auto-stop a 300s
        if (elapsed >= 300) {
          stopRecording();
        }
      }, 100);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Errore accesso microfono';
      setError(message);
      setIsRecording(false);
    }
  };

  const stopRecording = () => {
    if (mediaRecorderRef.current && mediaRecorderRef.current.state === 'recording') {
      mediaRecorderRef.current.stop();
    }

    if (timerIntervalRef.current) {
      clearInterval(timerIntervalRef.current);
      timerIntervalRef.current = null;
    }

    setIsRecording(false);
  };

  const resetRecording = () => {
    setBlob(null);
    setAudioMetadata(null);
    setElapsedSeconds(0);
    setError(null);
    audioChunksRef.current = [];
  };

  return {
    isRecording,
    elapsedSeconds,
    blob,
    audioMetadata,
    error,
    startRecording,
    stopRecording,
    resetRecording,
  };
};
