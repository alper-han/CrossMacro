using System;
using System.IO;
using Serilog;

namespace CrossMacro.Infrastructure.Helpers;

public static class PermissionHelper
{
    public static bool CheckUInputAccess()
    {
        try
        {
            // Helper to check write access
            bool CheckWrite(string path)
            {
                if (!File.Exists(path)) return false;
                try
                {
                    using var fs = File.OpenWrite(path);
                    return true;
                }
                catch (UnauthorizedAccessException)
                {
                    return false;
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Failed to check permission for {Path}", path);
                    return false;
                }
            }

            // Check standard paths
            if (CheckWrite("/dev/uinput")) return true;
            if (CheckWrite("/dev/input/uinput")) return true;
            
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking uinput permissions");
            return false;
        }
    }
}
