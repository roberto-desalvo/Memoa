export type Entry = {
  id: string;
  text: string;
  createdAt: string;
  audioPath?: string | null;
  audioMime?: string | null;
  audioDurationMs?: number | null;
  audioSizeBytes?: number | null;
};

export type CreateEntryRequest = {
  id: string;
  text: string;
  createdAt: string;
  audioPath?: string | null;
  audioMime?: string | null;
  audioDurationMs?: number | null;
  audioSizeBytes?: number | null;
};

export type UserDto = {
  id: string;
  email: string;
};

export type ApiSuccessResult<T> = {
  data?: T;
  httpStatusCode: number;
  resultCode: number;
  resultDescription: string;
  details?: string;
  problemDetails?: unknown;
  parameters?: Record<string, unknown>;
};

export type ApiResponse<T> = {
  success: boolean;
  data?: T;
  error?: string;
  status?: number;
};

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
}
