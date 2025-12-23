using System.Runtime.InteropServices;

namespace CrossMacro.Platform.MacOS.Native;

internal static class Accessibility
{
    private const string ApplicationServicesLib = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";

    [DllImport(ApplicationServicesLib)]
    public static extern bool AXIsProcessTrusted();

    // Often used to prompt; checking with a prompt option
    // extern Boolean AXIsProcessTrustedWithOptions (CFDictionaryRef options);
    [DllImport(ApplicationServicesLib)]
    public static extern bool AXIsProcessTrustedWithOptions(System.IntPtr options);
    
    // We might need a helper to create the dictionary option kAXTrustedCheckOptionPrompt = true
}
