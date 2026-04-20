using ContextMenuApp.Interfaces;
using ContextMenuApp.Models;
using ContextMenuApp.Services;

namespace ContextMenuApp
{
    /// <summary>
    /// Entry point for the Context Menu Application.
    /// 
    /// === TWO MODES OF OPERATION ===
    /// 
    /// 1. NAUTILUS SCRIPT MODE (no arguments — called by Nautilus Scripts feature):
    ///    The binary is placed in ~/.local/share/nautilus/scripts/ with two symlinks:
    ///      "📤 Upload"  → ContextMenuApp
    ///      "📥 Download" → ContextMenuApp
    ///    When the user right-clicks → Scripts → "📤 Upload" or "📥 Download",
    ///    Nautilus executes the symlink and sets environment variables:
    ///      NAUTILUS_SCRIPT_SELECTED_FILE_PATHS — newline-separated selected files
    ///      NAUTILUS_SCRIPT_CURRENT_URI          — current folder as file:// URI
    ///    The app detects which symlink was called by inspecting its own process name
    ///    (Environment.ProcessPath), then reads the appropriate environment variable.
    /// 
    /// 2. CLI MODE (with arguments — for manual testing):
    ///    dotnet run -- upload "/home/user/Documents"
    ///    dotnet run -- download "/home/user/Documents/file.txt"
    /// 
    /// === OS FUNCTIONS USED ===
    /// 
    /// 1. Environment.GetEnvironmentVariable() (System.Environment)
    ///    - Reads OS environment variables set by Nautilus
    ///    - Uses the Linux getenv() system call to read from the process environment block
    ///    - NAUTILUS_SCRIPT_SELECTED_FILE_PATHS: paths of selected files (newline-separated)
    ///    - NAUTILUS_SCRIPT_CURRENT_URI: current directory as file:// URI
    /// 
    /// 2. Environment.ProcessPath (System.Environment)
    ///    - Returns the full path of the currently running executable
    ///    - On Linux, reads from /proc/self/exe (procfs virtual filesystem)
    ///    - Used to detect which symlink name launched this process
    /// 
    /// 3. Path.GetFileName() (System.IO.Path)
    ///    - Extracts the filename from the process path to determine the symlink name
    ///    - e.g., "/home/user/.local/share/nautilus/scripts/📤 Upload" → "📤 Upload"
    /// 
    /// 4. Console.WriteLine() / Console.Error.WriteLine()
    ///    - Writes to stdout (fd 1) and stderr (fd 2) respectively
    ///    - Uses the Linux write() system call on these file descriptors
    /// 
    /// === ARCHITECTURE: SEPARATE APIs ===
    /// 
    /// The application follows the Single Responsibility Principle:
    /// - IUploadService  → handles ONLY upload operations (separate API)
    /// - IDownloadService → handles ONLY download operations (separate API)
    /// - IDialogService  → handles ONLY OS dialog display (separate API)
    /// 
    /// Each service is a separate API with its own interface, making them:
    /// - Independently testable
    /// - Independently replaceable/mockable
    /// - Loosely coupled via dependency injection
    /// </summary>
    class Program
    {
        // Console color helpers for professional output
        static void WriteColor(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ResetColor();
        }

        static void WriteLineColor(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }

        static int Main(string[] args)
        {
            // Global try-catch: When launched from Nautilus, there is no terminal.
            // If the app crashes, the user sees NOTHING. This catch logs all errors
            // to a file so we can diagnose issues.
            try
            {
                return Run(args);
            }
            catch (Exception ex)
            {
                DialogService.Log($"[FATAL] Unhandled exception: {ex}");
                return 1;
            }
        }

        static int Run(string[] args)
        {
            DialogService.Log($"[Program] Started. args.Length={args.Length}");
            DialogService.Log($"[Program] argv[0]={Environment.GetCommandLineArgs()[0]}");
            Console.WriteLine();
            WriteLineColor("  ╔═══════════════════════════════════════════════════╗", ConsoleColor.Cyan);
            WriteLineColor("  ║                                                   ║", ConsoleColor.Cyan);
            WriteLineColor("  ║     📁  Context Menu App — Upload/Download API    ║", ConsoleColor.Cyan);
            WriteLineColor("  ║         OS Right-Click Integration (Linux)        ║", ConsoleColor.Cyan);
            WriteLineColor("  ║                                                   ║", ConsoleColor.Cyan);
            WriteLineColor("  ╚═══════════════════════════════════════════════════╝", ConsoleColor.Cyan);
            Console.WriteLine();

            WriteColor("  ⏰ Started at : ", ConsoleColor.DarkGray);
            WriteLineColor($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}", ConsoleColor.White);

            WriteColor("  📥 Arguments  : ", ConsoleColor.DarkGray);
            WriteLineColor($"{args.Length}", ConsoleColor.White);
            Console.WriteLine();

            // --- Determine command and path ---
            // Two modes: CLI mode (args provided) or Nautilus Script mode (no args)
            string command;
            string path;

            // List of single-argument commands (no path needed)
            string[] singleArgCommands = { "register", "unregister", "config", "help", "-h", "--help" };

            if (args.Length == 1 && singleArgCommands.Any(c => args[0].Equals(c, StringComparison.OrdinalIgnoreCase)))
            {
                // CLI MODE: System management (register / unregister / config)
                command = args[0].ToLower().Trim();
                path = string.Empty;

                WriteColor("  🔧 Mode       : ", ConsoleColor.DarkGray);
                WriteLineColor("CLI (system management)", ConsoleColor.White);
            }
            else if (args.Length >= 2)
            {
                // CLI MODE: dotnet run -- upload "/path"
                //           dotnet run -- enable upload
                //           dotnet run -- disable download
                command = args[0].ToLower().Trim();
                path = args[1].Trim();

                WriteColor("  🔧 Mode       : ", ConsoleColor.DarkGray);
                WriteLineColor("CLI (manual arguments)", ConsoleColor.White);
            }
            else if (args.Length == 0)
            {
                // NAUTILUS SCRIPT MODE: auto-detect from environment variables
                WriteColor("  🔧 Mode       : ", ConsoleColor.DarkGray);
                WriteLineColor("Nautilus Script (auto-detect)", ConsoleColor.White);

                // Detect which operation from the symlink name or env vars
                command = DetectCommandFromProcessName();
                DialogService.Log($"[Program] Detected command: {command}");

                if (string.IsNullOrEmpty(command))
                {
                    Console.Error.WriteLine("  ❌ Could not detect operation from process name.");
                    Console.Error.WriteLine("     Expected symlink name containing 'Upload' or 'Download'.");
                    PrintUsage();
                    return 1;
                }

                // Read the path from Nautilus environment variables
                // These are set by Nautilus before launching the script
                path = ReadNautilusPath(command);
                DialogService.Log($"[Program] ReadNautilusPath returned: {path}");

                if (string.IsNullOrEmpty(path))
                {
                    Console.Error.WriteLine("  ❌ No path received from Nautilus environment variables.");
                    Console.Error.WriteLine("     NAUTILUS_SCRIPT_SELECTED_FILE_PATHS and");
                    Console.Error.WriteLine("     NAUTILUS_SCRIPT_CURRENT_URI are both empty.");
                    PrintUsage();
                    return 1;
                }
            }
            else
            {
                // Invalid arguments
                PrintUsage();
                return 1;
            }

            WriteColor("  🔧 Command    : ", ConsoleColor.DarkGray);
            WriteLineColor(command.ToUpper(), ConsoleColor.Yellow);

            WriteColor("  📂 Path       : ", ConsoleColor.DarkGray);
            WriteLineColor(path, ConsoleColor.White);
            Console.WriteLine();

            // --- Create services (Dependency Injection) ---
            // DialogService is shared between Upload and Download services
            IDialogService dialogService = new DialogService();
            IConfigurationService configService = new ConfigurationService();
            IUploadService uploadService = new UploadService(dialogService);
            IDownloadService downloadService = new DownloadService(dialogService);
            IExtensionService extensionService = new ExtensionService(dialogService);
            IVisibilityService visibilityService = new VisibilityService();

            // --- Load configuration (feature flags) ---
            ContextMenuConfig config = configService.LoadConfiguration();

            WriteColor("  ⚙️  Config     : ", ConsoleColor.DarkGray);
            WriteLineColor($"Upload={config.Upload}, Download={config.Download}", ConsoleColor.White);

            // --- Route to appropriate API based on command ---
            OperationResult result;

            switch (command)
            {
                case "upload":
                    // Safety guard: check if Upload is enabled in config
                    if (!config.Upload)
                    {
                        WriteLineColor("  ⚠️  Upload is DISABLED in appsettings.json", ConsoleColor.Yellow);
                        dialogService.ShowInfoDialog("Upload Disabled",
                            "The Upload option is currently disabled in the configuration.\n" +
                            "To enable it, set \"Upload\": true in appsettings.json.");
                        result = new OperationResult
                        {
                            Success = false,
                            OperationType = "Upload",
                            CallbackData = "Disabled",
                            Message = "Upload is disabled in appsettings.json",
                            Timestamp = DateTime.Now
                        };
                        break;
                    }
                    WriteLineColor("  ▶ Routing to Upload API (IUploadService)...", ConsoleColor.Cyan);
                    WriteLineColor("  ─────────────────────────────────────────────", ConsoleColor.DarkGray);
                    Console.WriteLine();
                    result = uploadService.UploadFile(path);
                    break;

                case "download":
                    // Safety guard: check if Download is enabled in config
                    if (!config.Download)
                    {
                        WriteLineColor("  ⚠️  Download is DISABLED in appsettings.json", ConsoleColor.Yellow);
                        dialogService.ShowInfoDialog("Download Disabled",
                            "The Download option is currently disabled in the configuration.\n" +
                            "To enable it, set \"Download\": true in appsettings.json.");
                        result = new OperationResult
                        {
                            Success = false,
                            OperationType = "Download",
                            CallbackData = "Disabled",
                            Message = "Download is disabled in appsettings.json",
                            Timestamp = DateTime.Now
                        };
                        break;
                    }
                    WriteLineColor("  ▶ Routing to Download API (IDownloadService)...", ConsoleColor.Cyan);
                    WriteLineColor("  ─────────────────────────────────────────────", ConsoleColor.DarkGray);
                    Console.WriteLine();
                    result = downloadService.DownloadFile(path);
                    break;

                case "check-visibility-upload":
                case "check-visibility-download":
                    // Used programmatically by the Python hook.
                    // Instead of parsing JSON inside Python, we evaluate C# programmatic logic.
                    // Exit code 0 = Visible, Exit code 1 = Hidden
                    string op = command.Replace("check-visibility-", "");

                    // We also evaluate the basic feature flags from config
                    bool featureFlagEnabled = op == "upload" ? config.Upload : config.Download;

                    if (!featureFlagEnabled)
                    {
                        return 1;
                    }

                    bool isVisible = visibilityService.IsVisible(op, path);
                    return isVisible ? 0 : 1;

                case "register":
                    WriteLineColor("  ▶ Routing to Extension API (IExtensionService)...", ConsoleColor.Cyan);
                    WriteLineColor("  ─────────────────────────────────────────────", ConsoleColor.DarkGray);
                    Console.WriteLine();
                    result = extensionService.RegisterExtension();
                    break;

                case "unregister":
                    WriteLineColor("  ▶ Routing to Extension API (IExtensionService)...", ConsoleColor.Cyan);
                    WriteLineColor("  ─────────────────────────────────────────────", ConsoleColor.DarkGray);
                    Console.WriteLine();
                    result = extensionService.UnregisterExtension();
                    break;

                case "config":
                    // Show current configuration status
                    WriteLineColor("  ▶ Current Configuration (appsettings.json):", ConsoleColor.Cyan);
                    WriteLineColor("  ─────────────────────────────────────────────", ConsoleColor.DarkGray);
                    Console.WriteLine();
                    WriteColor("    📤 Upload   : ", ConsoleColor.White);
                    WriteLineColor(config.Upload ? "✅ ENABLED" : "❌ DISABLED", config.Upload ? ConsoleColor.Green : ConsoleColor.Red);
                    WriteColor("    📥 Download : ", ConsoleColor.White);
                    WriteLineColor(config.Download ? "✅ ENABLED" : "❌ DISABLED", config.Download ? ConsoleColor.Green : ConsoleColor.Red);
                    Console.WriteLine();
                    WriteLineColor("  💡 Use 'enable' or 'disable' to change:", ConsoleColor.DarkGray);
                    WriteLineColor("     dotnet run -- enable upload", ConsoleColor.DarkGray);
                    WriteLineColor("     dotnet run -- disable download", ConsoleColor.DarkGray);
                    Console.WriteLine();
                    result = new OperationResult
                    {
                        Success = true,
                        OperationType = "Config",
                        CallbackData = $"Upload={config.Upload}, Download={config.Download}",
                        Message = "Configuration displayed successfully",
                        Timestamp = DateTime.Now
                    };
                    break;

                case "enable":
                case "disable":
                    // Toggle a feature flag: enable/disable upload or download
                    bool enableFlag = (command == "enable");
                    string target = path.ToLower().Trim(); // path holds the second arg (upload/download)

                    if (target == "upload")
                    {
                        config.Upload = enableFlag;
                    }
                    else if (target == "download")
                    {
                        config.Download = enableFlag;
                    }
                    else
                    {
                        Console.Error.WriteLine($"  ❌ Unknown option: {target}");
                        Console.Error.WriteLine("     Valid options: upload, download");
                        PrintUsage();
                        return 1;
                    }

                    // Save updated config
                    configService.SaveConfiguration(config);

                    string action = enableFlag ? "ENABLED ✅" : "DISABLED ❌";
                    string emoji = target == "upload" ? "📤" : "📥";
                    WriteLineColor($"  {emoji} {char.ToUpper(target[0]) + target.Substring(1)} is now {action}", enableFlag ? ConsoleColor.Green : ConsoleColor.Red);
                    Console.WriteLine();
                    WriteLineColor("  📋 Updated configuration:", ConsoleColor.White);
                    WriteColor("    📤 Upload   : ", ConsoleColor.White);
                    WriteLineColor(config.Upload ? "✅ ENABLED" : "❌ DISABLED", config.Upload ? ConsoleColor.Green : ConsoleColor.Red);
                    WriteColor("    📥 Download : ", ConsoleColor.White);
                    WriteLineColor(config.Download ? "✅ ENABLED" : "❌ DISABLED", config.Download ? ConsoleColor.Green : ConsoleColor.Red);
                    Console.WriteLine();

                    result = new OperationResult
                    {
                        Success = true,
                        OperationType = enableFlag ? "Enable" : "Disable",
                        CallbackData = $"{target}={enableFlag}",
                        Message = $"{target} has been {(enableFlag ? "enabled" : "disabled")}",
                        Timestamp = DateTime.Now
                    };
                    break;

                case "help":
                case "-h":
                case "--help":
                    PrintUsage();
                    return 0;

                default:
                    Console.Error.WriteLine($"  ❌ Unknown command: {command}");
                    PrintUsage();
                    return 1;
            }

            // --- Print result summary ---
            Console.WriteLine();
            WriteLineColor("  ╔═══════════════════════════════════════════════════╗", ConsoleColor.Green);
            WriteLineColor("  ║              ✅  OPERATION RESULT                 ║", ConsoleColor.Green);
            WriteLineColor("  ╠═══════════════════════════════════════════════════╣", ConsoleColor.Green);

            WriteColor("  ║  Operation  : ", ConsoleColor.Green);
            WriteColor($"{result.OperationType,-37}", result.OperationType == "Upload" ? ConsoleColor.Magenta : ConsoleColor.Blue);
            WriteLineColor("║", ConsoleColor.Green);

            WriteColor("  ║  Status     : ", ConsoleColor.Green);
            WriteColor($"{(result.Success ? "SUCCESS ✅" : "FAILED ❌"),-37}", result.Success ? ConsoleColor.Green : ConsoleColor.Red);
            WriteLineColor("║", ConsoleColor.Green);

            WriteColor("  ║  Callback   : ", ConsoleColor.Green);
            WriteColor($"{result.CallbackData,-37}", ConsoleColor.Yellow);
            WriteLineColor("║", ConsoleColor.Green);

            WriteColor("  ║  Message    : ", ConsoleColor.Green);
            string msgTruncated = result.Message.Length > 37 ? result.Message.Substring(0, 34) + "..." : result.Message;
            WriteColor($"{msgTruncated,-37}", ConsoleColor.White);
            WriteLineColor("║", ConsoleColor.Green);

            WriteColor("  ║  Timestamp  : ", ConsoleColor.Green);
            WriteColor($"{result.Timestamp:yyyy-MM-dd HH:mm:ss,-37}", ConsoleColor.DarkGray);
            WriteLineColor("║", ConsoleColor.Green);

            WriteLineColor("  ╚═══════════════════════════════════════════════════╝", ConsoleColor.Green);
            Console.WriteLine();

            DialogService.Log($"[Program] Completed. Success={result.Success}");
            return result.Success ? 0 : 1;
        }

        /// <summary>
        /// Detects the command (upload/download) from the process executable name.
        /// 
        /// When Nautilus launches a script, the process name is the symlink name.
        /// We create symlinks like "📤 Upload" and "📥 Download" that point to
        /// the ContextMenuApp binary. By reading the process name, we know which
        /// operation the user selected.
        /// 
        /// === WHY WE USE argv[0] INSTEAD OF Environment.ProcessPath ===
        /// 
        /// Environment.ProcessPath reads /proc/self/exe, which is a kernel-level
        /// symlink that RESOLVES to the actual binary path. So if "📤 Upload" is
        /// a symlink to "ContextMenuApp", ProcessPath returns "ContextMenuApp".
        /// 
        /// argv[0] (Environment.GetCommandLineArgs()[0]) preserves the ORIGINAL
        /// name used to invoke the process — the symlink name. On Linux, when
        /// the shell calls execvp(), it passes the symlink path as argv[0].
        /// 
        /// FALLBACK: If the name doesn't contain Upload/Download:
        ///   - If NAUTILUS_SCRIPT_SELECTED_FILE_PATHS is set → user right-clicked
        ///     a file → "download"
        ///   - Otherwise → user right-clicked empty space → "upload"
        /// </summary>
        private static string DetectCommandFromProcessName()
        {
            // === PRIMARY DETECTION: argv[0] ===
            // Environment.GetCommandLineArgs()[0] returns argv[0] from the OS
            // On Linux, argv[0] is populated by the execvp() system call
            // It preserves the symlink name (e.g., "📤 Upload")
            string[] cmdArgs = Environment.GetCommandLineArgs();
            string processName = cmdArgs.Length > 0 ? Path.GetFileName(cmdArgs[0]) : string.Empty;

            WriteColor("  🔍 argv[0]    : ", ConsoleColor.DarkGray);
            WriteLineColor(processName, ConsoleColor.White);

            // Check if the name contains "Upload" or "Download"
            if (processName.Contains("Upload", StringComparison.OrdinalIgnoreCase))
            {
                return "upload";
            }
            else if (processName.Contains("Download", StringComparison.OrdinalIgnoreCase))
            {
                return "download";
            }

            // === FALLBACK DETECTION: Environment variables ===
            // If argv[0] didn't help (e.g., symlink resolved), use Nautilus env vars
            // to intelligently determine the operation:
            //   - If files are selected → the user right-clicked a file → download
            //   - If no files selected → the user right-clicked empty space → upload
            WriteColor("  🔍 Fallback   : ", ConsoleColor.DarkGray);
            WriteLineColor("Detecting from Nautilus environment variables...", ConsoleColor.White);

            string? selectedFiles = Environment.GetEnvironmentVariable(
                "NAUTILUS_SCRIPT_SELECTED_FILE_PATHS");

            if (!string.IsNullOrWhiteSpace(selectedFiles))
            {
                WriteColor("  🔍 Detected   : ", ConsoleColor.DarkGray);
                WriteLineColor("Files selected → DOWNLOAD", ConsoleColor.Blue);
                return "download";
            }
            else
            {
                WriteColor("  🔍 Detected   : ", ConsoleColor.DarkGray);
                WriteLineColor("No files selected → UPLOAD", ConsoleColor.Magenta);
                return "upload";
            }
        }

        /// <summary>
        /// Reads the file/folder path from Nautilus environment variables.
        /// 
        /// Nautilus sets these environment variables before launching a script:
        /// 
        /// 1. NAUTILUS_SCRIPT_SELECTED_FILE_PATHS
        ///    - Newline-separated list of absolute paths of selected files
        ///    - Set when the user has one or more files/folders selected
        ///    - e.g., "/home/user/Documents/report.pdf\n/home/user/Documents/image.png"
        /// 
        /// 2. NAUTILUS_SCRIPT_CURRENT_URI
        ///    - The current directory as a file:// URI
        ///    - e.g., "file:///home/user/Documents"
        ///    - Always set, even when right-clicking on empty space
        /// 
        /// OS Function: Environment.GetEnvironmentVariable()
        ///   - Calls the Linux getenv() function
        ///   - getenv() searches the process environment block (char** environ)
        ///   - The environ array is inherited from the parent process (Nautilus)
        ///   - Nautilus populates these variables before fork()+exec() of our binary
        /// </summary>
        private static string ReadNautilusPath(string command)
        {
            if (command == "download")
            {
                // For download, read the selected file path
                // NAUTILUS_SCRIPT_SELECTED_FILE_PATHS contains newline-separated paths
                string? selectedFiles = Environment.GetEnvironmentVariable(
                    "NAUTILUS_SCRIPT_SELECTED_FILE_PATHS");

                WriteColor("  🌐 Env Var    : ", ConsoleColor.DarkGray);
                WriteLineColor("NAUTILUS_SCRIPT_SELECTED_FILE_PATHS", ConsoleColor.White);

                if (!string.IsNullOrWhiteSpace(selectedFiles))
                {
                    // Take the first selected file (split by newline)
                    // On Linux, paths use forward slashes and newline is \n
                    string firstFile = selectedFiles.Split('\n',
                        StringSplitOptions.RemoveEmptyEntries)[0].Trim();

                    WriteColor("  📄 Selected   : ", ConsoleColor.DarkGray);
                    WriteLineColor(firstFile, ConsoleColor.White);

                    return firstFile;
                }
            }
            else if (command == "upload")
            {
                // For upload, read the current folder URI
                // NAUTILUS_SCRIPT_CURRENT_URI is a file:// URI
                string? currentUri = Environment.GetEnvironmentVariable(
                    "NAUTILUS_SCRIPT_CURRENT_URI");

                WriteColor("  🌐 Env Var    : ", ConsoleColor.DarkGray);
                WriteLineColor("NAUTILUS_SCRIPT_CURRENT_URI", ConsoleColor.White);

                if (!string.IsNullOrWhiteSpace(currentUri))
                {
                    // Convert file:// URI to local path
                    // System.Uri handles URI decoding (e.g., %20 → space)
                    string folderPath = currentUri.Trim();

                    if (folderPath.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                    {
                        var uri = new Uri(folderPath);
                        folderPath = uri.LocalPath;
                    }

                    WriteColor("  📂 Folder     : ", ConsoleColor.DarkGray);
                    WriteLineColor(folderPath, ConsoleColor.White);

                    return folderPath;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Prints usage instructions to the console.
        /// </summary>
        static void PrintUsage()
        {
            WriteLineColor("  ─────────────────────────────────────────────", ConsoleColor.DarkGray);
            WriteLineColor("  📖 USAGE:", ConsoleColor.Yellow);
            Console.WriteLine();

            WriteLineColor("  Register Extension (install context menu):", ConsoleColor.Cyan);
            Console.WriteLine();

            WriteColor("    dotnet run -- ", ConsoleColor.DarkGray);
            WriteLineColor("register", ConsoleColor.Green);

            WriteColor("      → ", ConsoleColor.DarkGray);
            WriteLineColor("Installs the Nautilus Python bridge, cleans up old scripts,", ConsoleColor.DarkGray);

            WriteColor("        ", ConsoleColor.DarkGray);
            WriteLineColor("and restarts Nautilus. Upload/Download appear in right-click.", ConsoleColor.DarkGray);
            Console.WriteLine();

            WriteLineColor("  Unregister Extension (remove context menu):", ConsoleColor.Cyan);
            Console.WriteLine();

            WriteColor("    dotnet run -- ", ConsoleColor.DarkGray);
            WriteLineColor("unregister", ConsoleColor.Red);

            WriteColor("      → ", ConsoleColor.DarkGray);
            WriteLineColor("Removes the Nautilus Python bridge and restarts Nautilus.", ConsoleColor.DarkGray);

            WriteColor("        ", ConsoleColor.DarkGray);
            WriteLineColor("Upload/Download disappear from right-click.", ConsoleColor.DarkGray);
            Console.WriteLine();

            WriteLineColor("  Configuration (feature flags):", ConsoleColor.Cyan);
            Console.WriteLine();

            WriteColor("    dotnet run -- ", ConsoleColor.DarkGray);
            WriteLineColor("config", ConsoleColor.Yellow);

            WriteColor("      → ", ConsoleColor.DarkGray);
            WriteLineColor("Shows current Upload/Download enabled/disabled status.", ConsoleColor.DarkGray);
            Console.WriteLine();

            WriteColor("    dotnet run -- ", ConsoleColor.DarkGray);
            WriteColor("enable ", ConsoleColor.Green);
            WriteLineColor("upload", ConsoleColor.White);

            WriteColor("    dotnet run -- ", ConsoleColor.DarkGray);
            WriteColor("disable ", ConsoleColor.Red);
            WriteLineColor("upload", ConsoleColor.White);

            WriteColor("    dotnet run -- ", ConsoleColor.DarkGray);
            WriteColor("enable ", ConsoleColor.Green);
            WriteLineColor("download", ConsoleColor.White);

            WriteColor("    dotnet run -- ", ConsoleColor.DarkGray);
            WriteColor("disable ", ConsoleColor.Red);
            WriteLineColor("download", ConsoleColor.White);

            WriteColor("      → ", ConsoleColor.DarkGray);
            WriteLineColor("Toggle menu options ON/OFF without editing JSON.", ConsoleColor.DarkGray);
            Console.WriteLine();

            WriteLineColor("  🔌 SEPARATE APIs:", ConsoleColor.Yellow);
            Console.WriteLine();

            WriteColor("    IUploadService          ", ConsoleColor.Magenta);
            WriteLineColor("→ Handles upload operations", ConsoleColor.DarkGray);

            WriteColor("    IDownloadService        ", ConsoleColor.Blue);
            WriteLineColor("→ Handles download operations", ConsoleColor.DarkGray);

            WriteColor("    IDialogService          ", ConsoleColor.Cyan);
            WriteLineColor("→ Handles OS native popups (Zenity)", ConsoleColor.DarkGray);

            WriteColor("    IExtensionService       ", ConsoleColor.Green);
            WriteLineColor("→ Handles Nautilus bridge registration", ConsoleColor.DarkGray);

            WriteColor("    IConfigurationService   ", ConsoleColor.Yellow);
            WriteLineColor("→ Handles config loading/saving (feature flags)", ConsoleColor.DarkGray);
            Console.WriteLine();

            WriteLineColor("  ─────────────────────────────────────────────", ConsoleColor.DarkGray);
            Console.WriteLine();
        }
    }
}
