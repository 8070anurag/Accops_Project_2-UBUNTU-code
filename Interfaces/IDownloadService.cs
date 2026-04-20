namespace ContextMenuApp.Interfaces
{
    /// <summary>
    /// API interface for Download operations.
    /// Defines the contract for handling download actions triggered
    /// from the OS right-click context menu on a selected file.
    /// </summary>
    public interface IDownloadService
    {
        /// <summary>
        /// Handles the download action when the user right-clicks on a file
        /// in the file manager and selects "Download".
        /// </summary>
        /// <param name="filePath">
        /// The absolute path of the file the user right-clicked on.
        /// This is obtained from the Nautilus environment variable
        /// NAUTILUS_SCRIPT_SELECTED_FILE_PATHS.
        /// </param>
        /// <returns>
        /// A Result object containing success/failure status and the filename.
        /// </returns>
        OperationResult DownloadFile(string filePath);
    }
}
