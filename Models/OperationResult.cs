namespace ContextMenuApp
{
    /// <summary>
    /// Model class representing the result of an Upload or Download operation.
    /// Used as the return type from both IUploadService and IDownloadService
    /// to provide structured callback data.
    /// </summary>
    public class OperationResult
    {
        /// <summary>
        /// Whether the operation completed successfully.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// The type of operation that was performed ("Upload" or "Download").
        /// </summary>
        public string OperationType { get; set; } = string.Empty;

        /// <summary>
        /// The callback data returned from the operation:
        /// - For Upload: the folder location/path where the user right-clicked
        /// - For Download: the filename of the file the user right-clicked on
        /// </summary>
        public string CallbackData { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable message describing the operation result.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp of when the operation was executed.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
