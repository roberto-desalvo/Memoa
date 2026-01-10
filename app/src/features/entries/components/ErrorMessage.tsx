interface ErrorMessageProps {
  message: string;
}

export const ErrorMessage = ({ message }: ErrorMessageProps) => {
  return (
    <div className="col-span-full mb-6 p-4 bg-red-100 dark:bg-red-900 text-red-600 dark:text-red-100 rounded-lg">
      {message}
    </div>
  );
};
