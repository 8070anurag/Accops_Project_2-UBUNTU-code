using ContextMenuApp.Interfaces;

namespace ContextMenuApp.Services
{
    /// <summary>
    /// Implementation of the IUploadService API.
    /// 
    /// === WHAT THIS SERVICE DOES ===
    /// 
    /// When the user right-clicks on EMPTY SPACE in the Nautilus file manager,
    /// the "Upload" option appears. Clicking it triggers this service.
    /// 
    /// The service receives the FOLDER LOCATION (current directory path) where
    /// the user right-clicked, and displays it as a callback via a native
    /// GNOME dialog popup.
    /// 
    /// === OS FUNCTIONS USED ===
    /// 
    /// 1. Uri class (System.Uri)
    ///    - Nautilus passes folder paths as URIs (e.g., "file:///home/user/Documents")
    ///    - System.Uri.LocalPath converts this to a normal path: "/home/user/Documents"
    ///    - This uses the OS file path conventions (forward slashes on Linux)
    /// 
    /// 2. Path.GetFullPath() (System.IO.Path)
    ///    - Resolves any relative path components (like .. or .)
    ///    - Uses the OS kernel's path resolution (realpath on Linux)
    /// 
    /// 3. Directory.Exists() (System.IO.Directory)
    ///    - Calls the Linux stat() system call to check if the path exists
    ///    - stat() returns file metadata including whether it's a directory
    /// </summary>
    public class UploadService : IUploadService
    {
        private readonly IDialogService _dialogService;

        /// <summary>
        /// Constructor with dependency injection.
        /// The DialogService is injected so this service doesn't directly
        /// depend on Zenity — making it testable and swappable.
        /// </summary>
        public UploadService(IDialogService dialogService)
        {
            _dialogService = dialogService;
        }

        /// <summary>
        /// Handles the Upload action callback.
        /// 
        /// Flow:
        /// 1. Receives the folder location from the Nautilus extension
        /// 2. Cleans/normalizes the path (handles URI format from Nautilus)
        /// 3. Validates the directory exists using OS stat() syscall
        /// 4. Displays the location in a native GNOME popup (Zenity)
        /// 5. Returns an OperationResult with the callback data
        /// </summary>
        /// <param name="folderLocation">
        /// The folder path where the user right-clicked on empty space.
        /// Can be in URI format (file:///path) or plain path format (/path).
        /// </param>
        public OperationResult UploadFile(string folderLocation)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("  📤 [UploadService] Upload API triggered");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("     Raw input   : ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(folderLocation);
            Console.ResetColor();

            // --- Step 1: Normalize the path ---
            // Nautilus may pass the path as a file:// URI or as a plain path
            string normalizedPath = NormalizePath(folderLocation);

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("     Step 1 ✔    : ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"Path normalized → {normalizedPath}");
            Console.ResetColor();

            // --- Step 2: Validate the directory exists ---
            // Directory.Exists() → Linux stat() system call
            // stat() fills a struct with file metadata (type, permissions, size, etc.)
            // We check if the inode type is S_IFDIR (directory)
            if (!Directory.Exists(normalizedPath))
            {
                string errorMsg = $"Directory not found: {normalizedPath}";
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"     Step 2 ✘    : {errorMsg}");
                Console.ResetColor();

                _dialogService.ShowErrorDialog("Upload Error", errorMsg);

                return new OperationResult
                {
                    Success = false,
                    OperationType = "Upload",
                    CallbackData = normalizedPath,
                    Message = errorMsg
                };
            }

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("     Step 2 ✔    : ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Directory validated (stat() syscall)");
            Console.ResetColor();

            // --- Step 3: Callback - Open file upload dialog ---
            // The start folder for the file selection dialog must end with a slash 
            // so Zenity treats it as a directory to open, not a filename to pre-fill.
            string startFolder = normalizedPath;
            if (!startFolder.EndsWith(Path.DirectorySeparatorChar))
            {
                startFolder += Path.DirectorySeparatorChar;
            }

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("     Step 3 ✔    : ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Opening file dialog at: {startFolder}");
            Console.ResetColor();

            // Display native GNOME file selection dialog via Zenity
            string? selectedFile = _dialogService.ShowFileOpenDialog("Select File to Upload", startFolder);

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("     Step 4 ✔    : ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Zenity dialog returned. Selected: {(string.IsNullOrEmpty(selectedFile) ? "None (Canceled)" : selectedFile)}");
            Console.ResetColor();

            // If user selected a file, show a confirmation info dialog for the demo
            if (!string.IsNullOrEmpty(selectedFile))
            {
                _dialogService.ShowInfoDialog("Upload Success", $"Simulating upload of file:\n\n{selectedFile}");
            }

            // --- Step 5: Return structured result ---
            return new OperationResult
            {
                Success = true,
                OperationType = "Upload",
                CallbackData = string.IsNullOrEmpty(selectedFile) ? normalizedPath : selectedFile,
                Message = string.IsNullOrEmpty(selectedFile) ? "Upload canceled by user." : $"File selected for upload: {selectedFile}"
            };
        }

        /// <summary>
        /// Normalizes the folder path from various formats.
        /// 
        /// Nautilus can pass paths in two formats:
        /// 1. URI format:  "file:///home/user/Documents"
        /// 2. Plain path:  "/home/user/Documents"
        /// 
        /// System.Uri.LocalPath handles the URI → local path conversion,
        /// including URL-decoding any encoded characters (e.g., %20 → space).
        /// 
        /// Path.GetFullPath() then resolves the path to its absolute form
        /// using the OS's path resolution (calls realpath() on Linux).
        /// </summary>
        private static string NormalizePath(string path)
        {
            string cleanPath = path.Trim();

            // Handle file:// URI format from Nautilus
            if (cleanPath.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                // System.Uri parses the URI and LocalPath extracts the OS path
                // e.g., "file:///home/user/My%20Docs" → "/home/user/My Docs"
                var uri = new Uri(cleanPath);
                cleanPath = uri.LocalPath;
            }

            // Remove trailing slashes for consistency
            cleanPath = cleanPath.TrimEnd('/');

            // Path.GetFullPath() → resolves to absolute path
            // On Linux, this uses realpath() to resolve symlinks and '..' components
            return Path.GetFullPath(cleanPath);
        }
    }
}
