using System.IO;

namespace ContextMenuApp.Services
{
    /// <summary>
    /// Implements programmatic visibility rules for the context menu.
    /// This allows complex logic (like database checks, API calls, or specific extension checks)
    /// instead of relying on static configuration files.
    /// </summary>
    public class VisibilityService : Interfaces.IVisibilityService
    {
        public bool IsVisible(string operation, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            if (operation.ToLower() == "download")
            {
                // Dynamic programmatic definition for 'Download' options.
                // The manager can integrate complex custom permissions here.
                string extension = Path.GetExtension(path).ToLower();
                
                if (extension == ".mp3" || extension == ".txt" )
                {
                    return true;
                }

                // If not matched, hide the menu.
                return false;
            }

            // For upload (directories), it's visible by default.
            if (operation.ToLower() == "upload")
            {
                return false;
            }

            return false;
        }
    }
}
