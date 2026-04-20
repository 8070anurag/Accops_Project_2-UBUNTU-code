using ContextMenuApp.Models;

namespace ContextMenuApp.Interfaces
{
    /// <summary>
    /// API interface for configuration loading operations.
    /// Provides a way to read application settings from the appsettings.json file.
    /// 
    /// Follows the Single Responsibility Principle — this service handles
    /// ONLY configuration reading, separate from dialog, upload, download APIs.
    /// </summary>
    public interface IConfigurationService
    {
        /// <summary>
        /// Loads the context menu configuration from appsettings.json.
        /// Returns a ContextMenuConfig with Upload/Download enabled/disabled flags.
        /// 
        /// If the config file is missing or unreadable, returns default config
        /// (both options enabled) to ensure the app degrades gracefully.
        /// </summary>
        /// <returns>A ContextMenuConfig object with the current feature flag values.</returns>
        ContextMenuConfig LoadConfiguration();

        /// <summary>
        /// Saves the given configuration back to appsettings.json.
        /// Writes to both the project directory (CWD) and the installed location
        /// (~/.local/share/context-menu-app/) so changes take effect immediately.
        /// </summary>
        /// <param name="config">The updated configuration to save.</param>
        void SaveConfiguration(ContextMenuConfig config);
    }
}
