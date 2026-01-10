export type Entry = {
  id: string;
  title: string;
  content: string;
  date: string;
};

export type ApiResponse<T> = {
  success: boolean;
  data?: T;
  error?: string;
};
