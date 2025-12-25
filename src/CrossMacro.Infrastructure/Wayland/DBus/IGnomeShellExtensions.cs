using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus;

namespace CrossMacro.Infrastructure.Wayland.DBus;

[DBusInterface("org.gnome.Shell.Extensions")]
public interface IGnomeShellExtensions : IDBusObject
{
    // Returns true if successful
    Task<bool> EnableExtensionAsync(string uuid);
    
    // Returns true if successful
    Task<bool> DisableExtensionAsync(string uuid);

    // Returns dictionary of extension info
    // We only need this to check status if necessary, but EnableExtension return value is usually sufficient
    // or we can catch errors.
    Task<IDictionary<string, object>> GetExtensionInfoAsync(string uuid);
}
