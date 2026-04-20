namespace ContextMenuApp.Models
{
    /// <summary>
    /// Model class that maps to the "ContextMenuOptions" section in appsettings.json.
    /// Controls which context menu items are visible in the Nautilus right-click menu.
    /// 
    /// When a property is set to true, the corresponding menu item appears.
    /// When set to false, the menu item is hidden from the context menu.
    /// 
    /// Default values are true (both options enabled) so the app works
    /// out of the box even if the config file is missing.
    /// </summary>
    public class ContextMenuConfig
    {
        /// <summary>
        /// Whether the "📤 Upload" option appears in the right-click menu.
        /// true  = Upload appears when right-clicking empty space
        /// false = Upload is hidden
        /// </summary>
        public bool Upload { get; set; } = true;

        /// <summary>
        /// Whether the "📥 Download" option appears in the right-click menu.
        /// true  = Download appears when right-clicking a file
        /// false = Download is hidden
        /// </summary>
        public bool Download { get; set; } = true;
    }
}
