interface UserInfoProps {
  email: string;
  id: string;
}

export const UserInfo = ({ email, id }: UserInfoProps) => {
  return (
    <div className="bg-green-100 dark:bg-green-900 text-green-600 dark:text-green-100 rounded-lg p-4 mb-6">
      <p className="font-medium">Logged in as: {email}</p>
      <p className="text-sm">User ID: {id}</p>
    </div>
  );
};
