using ContextMenuApp.Interfaces;

namespace ContextMenuApp.Services
{
    /// <summary>
    /// Implementation of the IDownloadService API.
    /// 
    /// === WHAT THIS SERVICE DOES ===
    /// 
    /// When the user right-clicks on a FILE in the Nautilus file manager,
    /// the "Download" option appears. Clicking it triggers this service.
    /// 
    /// The service receives the full FILE PATH of the selected file,
    /// extracts the FILENAME, and displays it as a callback via a native
    /// GNOME dialog popup.
    /// 
    /// === OS FUNCTIONS USED ===
    /// 
    /// 1. Path.GetFileName() (System.IO.Path)
    ///    - Extracts the filename from a full path
    ///    - e.g., "/home/user/Documents/report.pdf" → "report.pdf"
    ///    - Uses the OS path separator convention (/ on Linux, \ on Windows)
    ///    - Internally scans from the end of the string looking for '/' separator
    /// 
    /// 2. Path.GetFullPath() (System.IO.Path)
    ///    - Resolves any relative path to an absolute path
    ///    - Uses the Linux realpath() system call under the hood
    /// 
    /// 3. File.Exists() (System.IO.File)
    ///    - Calls the Linux access() or stat() system call
    ///    - Checks if the file exists and is accessible
    ///    - Returns false for directories (only true for regular files)
    /// 
    /// 4. FileInfo class (System.IO.FileInfo)
    ///    - Wraps the Linux stat() system call to get file metadata
    ///    - Provides: file size, creation time, last modified time, extension
    /// </summary>
    public class DownloadService : IDownloadService
    {
        private readonly IDialogService _dialogService;

        /// <summary>
        /// Constructor with dependency injection.
        /// The DialogService is injected so this service doesn't directly
        /// depend on Zenity — making it testable and swappable.
        /// </summary>
        public DownloadService(IDialogService dialogService)
        {
            _dialogService = dialogService;
        }

        /// <summary>
        /// Handles the Download action callback.
        /// 
        /// Flow:
        /// 1. Receives the file path from the Nautilus extension
        /// 2. Cleans/normalizes the path (handles URI format from Nautilus)
        /// 3. Extracts the filename using Path.GetFileName()
        /// 4. Optionally gets file metadata using FileInfo (stat() syscall)
        /// 5. Displays the filename in a native GNOME popup (Zenity)
        /// 6. Returns an OperationResult with the callback data
        /// </summary>
        /// <param name="filePath">
        /// The absolute path of the file the user right-clicked on.
        /// Can be in URI format (file:///path/file) or plain path (/path/file).
        /// </param>
        public OperationResult DownloadFile(string filePath)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("  📥 [DownloadService] Download API triggered");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("     Raw input   : ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(filePath);
            Console.ResetColor();

            // --- Step 1: Normalize the path ---
            string normalizedPath = NormalizePath(filePath);

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("     Step 1 ✔    : ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"Path normalized → {normalizedPath}");
            Console.ResetColor();

            // --- Step 2: Extract filename ---
            // Path.GetFileName() scans from the end of the string for the OS
            // path separator character ('/' on Linux) and returns everything after it.
            // e.g., "/home/user/Documents/report.pdf" → "report.pdf"
            string fileName = Path.GetFileName(normalizedPath);

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("     Step 2 ✔    : ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Filename extracted → {fileName}");
            Console.ResetColor();

            // --- Step 3: Validate the file exists ---
            // File.Exists() → Linux stat() system call
            // Checks if the path points to a regular file (not a directory)
            if (!File.Exists(normalizedPath))
            {
                string errorMsg = $"File not found: {normalizedPath}";
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"     Step 3 ✘    : {errorMsg}");
                Console.ResetColor();

                _dialogService.ShowErrorDialog("Download Error", errorMsg);

                return new OperationResult
                {
                    Success = false,
                    OperationType = "Download",
                    CallbackData = fileName,
                    Message = errorMsg
                };
            }

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("     Step 3 ✔    : ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("File validated (stat() syscall)");
            Console.ResetColor();

            // --- Step 4: Get file metadata using FileInfo ---
            // FileInfo wraps the Linux stat() system call
            // stat() returns a struct containing:
            //   st_size  → file size in bytes
            //   st_mtime → last modification time
            //   st_mode  → file permissions and type
            var fileInfo = new FileInfo(normalizedPath);
            string fileSize = FormatFileSize(fileInfo.Length);
            string fileExtension = fileInfo.Extension;

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("     Step 4 ✔    : ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"Metadata → Size: {fileSize} | Ext: {fileExtension}");
            Console.ResetColor();

            // --- Step 5: Callback - Show the filename to the user ---
            string callbackMessage = $"📥 Download File Info:\n\n" +
                                     $"Filename: {fileName}\n" +
                                     $"Extension: {fileExtension}\n" +
                                     $"Size: {fileSize}\n" +
                                     $"Path: {normalizedPath}";

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("     Step 5 ✔    : ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"CALLBACK → Filename: {fileName}");
            Console.ResetColor();

            // Display native GNOME dialog via Zenity
            _dialogService.ShowInfoDialog("Download - Filename Callback", callbackMessage);

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("     Step 6 ✔    : ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Zenity dialog displayed (fork + execvp)");
            Console.ResetColor();

            // --- Step 6: Return structured result ---
            return new OperationResult
            {
                Success = true,
                OperationType = "Download",
                CallbackData = fileName,
                Message = $"Download callback executed. Filename: {fileName}"
            };
        }

        /// <summary>
        /// Normalizes the file path from various formats.
        /// Same logic as UploadService - handles file:// URIs from Nautilus.
        /// </summary>
        private static string NormalizePath(string path)
        {
            string cleanPath = path.Trim();

            // Handle file:// URI format from Nautilus
            if (cleanPath.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(cleanPath);
                cleanPath = uri.LocalPath;
            }

            return Path.GetFullPath(cleanPath);
        }

        /// <summary>
        /// Formats file size from bytes to human-readable format.
        /// Uses the file size obtained from stat() system call via FileInfo.Length.
        /// </summary>
        private static string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int suffixIndex = 0;
            double size = bytes;

            while (size >= 1024 && suffixIndex < suffixes.Length - 1)
            {
                size /= 1024;
                suffixIndex++;
            }

            return $"{size:0.##} {suffixes[suffixIndex]}";
        }
    }
}
