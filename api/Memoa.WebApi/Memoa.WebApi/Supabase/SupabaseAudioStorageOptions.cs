namespace Memoa.WebApi.Supabase
{
    public class SupabaseAudioStorageOptions
    {
        public string? SupabaseUrl { get; set; } // e.g. https://eeckgxnhwqhzqwybzqvm.supabase.co
        public string? Bucket { get; set; }      // e.g. "audio"
        public string? ServiceRoleKey { get; set; } // Service role key from Supabase dashboard
    }
}
