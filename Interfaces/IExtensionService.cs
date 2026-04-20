namespace ContextMenuApp.Interfaces
{
    /// <summary>
    /// API interface for Extension registration operations.
    /// Defines the contract for registering and unregistering the Nautilus Python bridge.
    /// </summary>
    public interface IExtensionService
    {
        /// <summary>
        /// Registers the extension by installing the Python bridge into Nautilus and restarting Nautilus.
        /// </summary>
        OperationResult RegisterExtension();

        /// <summary>
        /// Unregisters the extension by removing the Python bridge from Nautilus and restarting Nautilus.
        /// </summary>
        OperationResult UnregisterExtension();
    }
}
