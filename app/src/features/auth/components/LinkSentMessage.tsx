interface LinkSentMessageProps {
  email: string;
  onTryAgain: () => void;
}

export const LinkSentMessage = ({ email, onTryAgain }: LinkSentMessageProps) => {
  return (
    <div className="text-center space-y-4">
      <div className="p-4 bg-green-100 dark:bg-green-900 border border-green-300 dark:border-green-700 text-green-700 dark:text-green-100 rounded">
        <p className="font-medium">Link inviato!</p>
        <p className="text-sm mt-2">
          Abbiamo inviato un link a <strong>{email}</strong>
        </p>
        <p className="text-sm mt-2">
          Controlla la tua email e clicca il link per accedere.
        </p>
      </div>

      <button
        onClick={onTryAgain}
        className="w-full py-2 px-4 bg-gray-300 dark:bg-gray-600 text-gray-800 dark:text-white font-medium rounded-lg hover:opacity-90 transition"
      >
        Invia un altro link
      </button>
    </div>
  );
};
