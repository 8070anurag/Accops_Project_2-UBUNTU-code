using System.Text.Json;
using ContextMenuApp.Interfaces;
using ContextMenuApp.Models;

namespace ContextMenuApp.Services
{
    /// <summary>
    /// Service responsible for reading application configuration from appsettings.json.
    /// 
    /// The config file is expected to be in the SAME directory as the running binary.
    /// This works for both development (dotnet run) and production (self-contained binary
    /// installed to ~/.local/share/context-menu-app/).
    /// 
    /// Uses System.Text.Json which is built into .NET 8 — no NuGet packages needed.
    /// 
    /// === GRACEFUL DEGRADATION ===
    /// If the config file is missing, corrupted, or unreadable, the service returns
    /// a default ContextMenuConfig (Upload=true, Download=true) so the app continues
    /// to work with all features enabled.
    /// </summary>
    public class ConfigurationService : IConfigurationService
    {
        private const string CONFIG_FILE_NAME = "appsettings.json";

        /// <summary>
        /// Loads the context menu configuration from appsettings.json.
        /// 
        /// Searches for the config file in these locations (in order):
        ///   1. Same directory as the running executable (production)
        ///   2. Current working directory (development with dotnet run)
        /// 
        /// OS Functions Used:
        ///   - AppContext.BaseDirectory: Returns the directory containing the running
        ///     executable. On Linux, this reads from /proc/self/exe to find the binary path.
        ///   - File.ReadAllText(): Uses Linux open()/read()/close() syscalls to read the file.
        ///   - JsonSerializer.Deserialize(): Parses the JSON into a C# object.
        /// </summary>
        public ContextMenuConfig LoadConfiguration()
        {
            try
            {
                string configPath = FindConfigFile();

                if (string.IsNullOrEmpty(configPath))
                {
                    DialogService.Log("[ConfigurationService] No appsettings.json found. Using defaults (all enabled).");
                    return new ContextMenuConfig();
                }

                DialogService.Log($"[ConfigurationService] Loading config from: {configPath}");

                string jsonContent = File.ReadAllText(configPath);

                // Parse the JSON into a wrapper structure
                // The JSON looks like: { "ContextMenuOptions": { "Upload": true, "Download": true } }
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var wrapper = JsonSerializer.Deserialize<ConfigWrapper>(jsonContent, options);

                if (wrapper?.ContextMenuOptions != null)
                {
                    DialogService.Log($"[ConfigurationService] Upload={wrapper.ContextMenuOptions.Upload}, Download={wrapper.ContextMenuOptions.Download}");
                    return wrapper.ContextMenuOptions;
                }

                DialogService.Log("[ConfigurationService] Config parsed but ContextMenuOptions section missing. Using defaults.");
                return new ContextMenuConfig();
            }
            catch (Exception ex)
            {
                DialogService.Log($"[ConfigurationService] Error reading config: {ex.Message}. Using defaults.");
                return new ContextMenuConfig();
            }
        }

        /// <summary>
        /// Searches for the appsettings.json file in known locations.
        /// Returns the full path if found, or empty string if not found.
        /// </summary>
        private string FindConfigFile()
        {
            // Location 1: Same directory as the running executable
            // In production, the binary is at ~/.local/share/context-menu-app/ContextMenuApp
            // and appsettings.json should be deployed alongside it
            string exeDir = AppContext.BaseDirectory;
            string path1 = Path.Combine(exeDir, CONFIG_FILE_NAME);
            if (File.Exists(path1))
            {
                return path1;
            }

            // Location 2: Current working directory (for development with dotnet run)
            string cwd = Directory.GetCurrentDirectory();
            string path2 = Path.Combine(cwd, CONFIG_FILE_NAME);
            if (File.Exists(path2))
            {
                return path2;
            }

            return string.Empty;
        }

        /// <summary>
        /// Internal wrapper class to deserialize the top-level JSON structure.
        /// Maps to: { "ContextMenuOptions": { ... } }
        /// </summary>
        private class ConfigWrapper
        {
            public ContextMenuConfig? ContextMenuOptions { get; set; }
        }

        /// <summary>
        /// Saves the given configuration back to appsettings.json.
        /// Writes to ALL known config file locations so changes take effect everywhere:
        ///   1. Current working directory (project root — for development)
        ///   2. Installed app directory (~/.local/share/context-menu-app/ — for production)
        ///
        /// OS Functions Used:
        ///   - File.WriteAllText(): Uses Linux open()/write()/close() syscalls.
        ///   - JsonSerializer.Serialize(): Converts C# object to JSON string.
        /// </summary>
        public void SaveConfiguration(ContextMenuConfig config)
        {
            var wrapper = new ConfigWrapper { ContextMenuOptions = config };
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = null // Keep PascalCase (Upload, Download)
            };

            string jsonContent = JsonSerializer.Serialize(wrapper, options);

            int savedCount = 0;

            // Location 1: Current working directory (project root)
            try
            {
                string cwdPath = Path.Combine(Directory.GetCurrentDirectory(), CONFIG_FILE_NAME);
                File.WriteAllText(cwdPath, jsonContent);
                DialogService.Log($"[ConfigurationService] Saved config to: {cwdPath}");
                savedCount++;
            }
            catch (Exception ex)
            {
                DialogService.Log($"[ConfigurationService] Could not save to CWD: {ex.Message}");
            }

            // Location 2: Installed app directory (production)
            try
            {
                string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string installedPath = Path.Combine(homeDir, ".local", "share", "context-menu-app", CONFIG_FILE_NAME);
                if (Directory.Exists(Path.GetDirectoryName(installedPath)!))
                {
                    File.WriteAllText(installedPath, jsonContent);
                    DialogService.Log($"[ConfigurationService] Saved config to: {installedPath}");
                    savedCount++;
                }
            }
            catch (Exception ex)
            {
                DialogService.Log($"[ConfigurationService] Could not save to installed dir: {ex.Message}");
            }

            if (savedCount == 0)
            {
                throw new Exception("Could not save configuration to any location.");
            }
        }
    }
}
