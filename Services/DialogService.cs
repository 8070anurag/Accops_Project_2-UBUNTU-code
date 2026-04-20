using System.Diagnostics;
using ContextMenuApp.Interfaces;

namespace ContextMenuApp.Services
{
    /// <summary>
    /// Implementation of IDialogService using Zenity.
    /// 
    /// === OS FUNCTION EXPLANATION ===
    /// 
    /// Zenity is a command-line utility that comes pre-installed on Ubuntu GNOME.
    /// It creates native GTK dialog windows from shell scripts or programs.
    /// 
    /// We use System.Diagnostics.Process to start a new OS process that runs
    /// the 'zenity' command. This is the same as typing 'zenity --info ...' in terminal.
    /// 
    /// Key OS functions used:
    /// - Process.Start()          → Creates a new OS-level process (fork+exec on Linux)
    /// - ProcessStartInfo         → Configures HOW the process is launched
    /// - UseShellExecute = false  → We bypass the shell and call zenity directly
    /// - RedirectStandardError    → Captures any error output from zenity
    /// - WaitForExit()            → Blocks until the user closes the dialog
    /// </summary>
    public class DialogService : IDialogService
    {
        // Log file path for debugging when running from Nautilus (no terminal)
        // We use the HOME environment variable instead of SpecialFolder.UserProfile
        // because SpecialFolder may not resolve correctly in self-contained apps
        private static readonly string LogFile = Path.Combine(
            Environment.GetEnvironmentVariable("HOME") ?? "/tmp",
            "context_menu_app.log");

        /// <summary>
        /// Shows an informational dialog using 'zenity --info'.
        /// 
        /// The zenity command used:
        ///   zenity --info --title="title" --text="message" --width=400 --height=200
        /// 
        /// --info    : Creates an information dialog (blue 'i' icon)
        /// --title   : Sets the window title bar text
        /// --text    : Sets the message body inside the dialog
        /// --width   : Sets minimum dialog width in pixels
        /// --height  : Sets minimum dialog height in pixels
        /// </summary>
        public void ShowInfoDialog(string title, string message)
        {
            RunZenity("--info", title, message);
        }

        /// <summary>
        /// Shows an error dialog using 'zenity --error'.
        /// 
        /// The zenity command used:
        ///   zenity --error --title="title" --text="message" --width=400 --height=200
        /// 
        /// --error   : Creates an error dialog (red 'X' icon)
        /// </summary>
        public void ShowErrorDialog(string title, string message)
        {
            RunZenity("--error", title, message);
        }

        /// <summary>
        /// Displays a file selection dialog using 'zenity --file-selection'.
        /// Captures the standard output to get the selected file path.
        /// </summary>
        public string? ShowFileOpenDialog(string title, string startFolder)
        {
            try
            {
                Log($"[DialogService] ShowFileOpenDialog called, title={title}, startFolder={startFolder}");

                string zenityPath = "/usr/bin/zenity";
                if (!File.Exists(zenityPath)) zenityPath = "zenity";

                var startInfo = new ProcessStartInfo
                {
                    FileName = zenityPath,
                    Arguments = $"--file-selection --title=\"{EscapeQuotes(title)}\" --filename=\"{EscapeQuotes(startFolder)}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                Log($"[DialogService] Starting: {startInfo.FileName} {startInfo.Arguments}");

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    string output = process.StandardOutput.ReadToEnd().Trim();
                    string errors = process.StandardError.ReadToEnd().Trim();
                    process.WaitForExit();

                    Log($"[DialogService] Zenity exit code: {process.ExitCode}");
                    
                    if (!string.IsNullOrEmpty(errors))
                    {
                        Log($"[DialogService] Zenity stderr: {errors}");
                    }

                    if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                    {
                        return output;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[DialogService] EXCEPTION in ShowFileOpenDialog: {ex}");
            }

            return null;
        }

        /// <summary>
        /// Internal method that constructs and executes the zenity process.
        /// 
        /// How it works step-by-step:
        /// 1. Creates a ProcessStartInfo object - this is .NET's way of configuring
        ///    a new OS process before launching it.
        /// 2. Sets FileName = "zenity" - the OS will search PATH to find the zenity binary
        ///    (usually at /usr/bin/zenity on Ubuntu).
        /// 3. Sets Arguments with the dialog type, title, text, and dimensions.
        /// 4. UseShellExecute = false - tells .NET to use the OS-level execvp() system call
        ///    directly instead of going through /bin/sh. This is more efficient.
        /// 5. Process.Start() - calls the Linux fork() system call to create a child process,
        ///    then execvp() to replace it with the zenity program.
        /// 6. WaitForExit() - calls the Linux waitpid() system call, which suspends our
        ///    program until the user closes the zenity dialog.
        /// </summary>
        private void RunZenity(string dialogType, string title, string message)
        {
            try
            {
                Log($"[DialogService] RunZenity called: {dialogType}, title={title}");

                // Ensure DISPLAY is available for GUI
                // When launched from Nautilus, DISPLAY should be inherited,
                // but we log it for debugging
                string? display = Environment.GetEnvironmentVariable("DISPLAY");
                Log($"[DialogService] DISPLAY={display ?? "(not set)"}");

                // Use /usr/bin/zenity directly to avoid PATH issues
                string zenityPath = "/usr/bin/zenity";
                if (!File.Exists(zenityPath))
                {
                    zenityPath = "zenity"; // Fall back to PATH search
                }

                // ProcessStartInfo configures how the OS process will be created
                var startInfo = new ProcessStartInfo
                {
                    // The executable to run
                    FileName = zenityPath,

                    // Command-line arguments passed to zenity
                    // We escape quotes in title/message to prevent injection
                    Arguments = $"{dialogType} --title=\"{EscapeQuotes(title)}\" " +
                                $"--text=\"{EscapeQuotes(message)}\" " +
                                $"--width=450 --height=200",

                    // false = use execvp() directly, don't go through shell
                    UseShellExecute = false,

                    // Redirect stderr so we can capture any zenity errors
                    RedirectStandardError = true,

                    // Don't create a visible console window (we want only the GUI dialog)
                    CreateNoWindow = true
                };

                Log($"[DialogService] Starting: {startInfo.FileName} {startInfo.Arguments}");

                // Process.Start() → Linux fork() + execvp("zenity", args)
                // This creates a brand new OS process running zenity
                using var process = Process.Start(startInfo);

                if (process != null)
                {
                    // Read stderr BEFORE WaitForExit to prevent deadlock
                    // If the child process writes enough to fill the stderr pipe buffer
                    // and we're waiting for exit, both processes would be stuck
                    string errors = process.StandardError.ReadToEnd();

                    // WaitForExit() → Linux waitpid() system call
                    // Our program pauses here until the user clicks OK on the dialog
                    process.WaitForExit();

                    Log($"[DialogService] Zenity exit code: {process.ExitCode}");

                    // Check if zenity reported any errors
                    if (!string.IsNullOrEmpty(errors))
                    {
                        Log($"[DialogService] Zenity stderr: {errors}");
                        Console.Error.WriteLine($"[DialogService] Zenity error: {errors}");
                    }
                }
                else
                {
                    Log("[DialogService] ERROR: Process.Start returned null");
                }
            }
            catch (Exception ex)
            {
                // If zenity is not installed or PATH is wrong, we fall back to console
                Log($"[DialogService] EXCEPTION: {ex}");
                Console.Error.WriteLine($"[DialogService] Failed to show dialog: {ex.Message}");
                Console.WriteLine($"[CALLBACK] {title}: {message}");
            }
        }

        /// <summary>
        /// Escapes double quotes in strings to prevent command injection.
        /// This is important because we're building command-line arguments.
        /// </summary>
        private static string EscapeQuotes(string input)
        {
            return input.Replace("\"", "\\\"");
        }

        /// <summary>
        /// Writes a log message to the log file for debugging.
        /// When the app is launched from Nautilus, there is no terminal,
        /// so file-based logging is the only way to see what happens.
        /// Uses the Linux open() + write() system calls via FileStream.
        /// </summary>
        internal static void Log(string message)
        {
            try
            {
                string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
                File.AppendAllText(LogFile, logLine);
            }
            catch
            {
                // Silently ignore logging failures
            }
        }
    }
}
