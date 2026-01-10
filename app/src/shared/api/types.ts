export type Entry = {
  id: string;
  text: string;
  createdAt: string;
};

export type CreateEntryRequest = {
  id: string;
  text: string;
  createdAt: string;
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
