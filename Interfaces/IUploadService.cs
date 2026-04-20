namespace ContextMenuApp.Interfaces
{
    /// <summary>
    /// API interface for Upload operations.
    /// Defines the contract for handling upload actions triggered
    /// from the OS right-click context menu on empty space.
    /// </summary>
    public interface IUploadService
    {
        /// <summary>
        /// Handles the upload action when the user right-clicks on empty space
        /// in the file manager and selects "Upload".
        /// </summary>
        /// <param name="folderLocation">
        /// The absolute path of the current directory/folder where the user
        /// right-clicked on empty space. This is obtained from the Nautilus
        /// environment variable NAUTILUS_SCRIPT_CURRENT_URI.
        /// </param>
        /// <returns>
        /// A Result object containing success/failure status and the folder location.
        /// </returns>
        OperationResult UploadFile(string folderLocation);
    }
}
