namespace MessageQueueNET.Models.Authentication
{
    public class ApiClient
    {
        public string Id { get; set; } = "";

        /// <summary>
        /// Plain text secret or "sha256:&lt;hex&gt;" (SHA-256 hash of the secret)
        /// </summary>
        public string Secret { get; set; } = "";

        /// <summary>
        /// read, write or admin (hierarchical). Empty => admin (backward compatibility)
        /// </summary>
        public string[]? Roles { get; set; }
    }
}
