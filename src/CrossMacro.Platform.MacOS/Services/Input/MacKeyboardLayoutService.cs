
namespace CrossMacro.Platform.MacOS.Services.Input;

internal sealed class MacKeyboardLayoutService : IKeyboardLayoutService, IDisposable
{
    private Dictionary<char, (int KeyCode, bool Shift, bool AltGr)>? _charToInputCache;
    private readonly Lock _lock = new();
    private readonly Lock _layoutLock = new();
    private readonly Func<bool> _isMainThread;
    private readonly SynchronizationContext? _mainThreadContext;
    private readonly Func<(IntPtr LayoutData, IntPtr KeyboardLayout, byte KeyboardType)> _loadKeyboardLayoutData;

    // Cache for keyboard layout pointer
    private IntPtr _cachedKeyboardLayout = IntPtr.Zero;
    private IntPtr _cachedLayoutData = IntPtr.Zero;
    private byte _cachedKeyboardType;
    private bool _disposed;
    private readonly IDisposable? _layoutChanges;
    private int _layoutStale;

    public MacKeyboardLayoutService()
        : this(
            MacOSMainThread.IsMainThread,
            SynchronizationContext.Current,
            LoadNativeKeyboardLayoutData,
            warmOnConstruction: true,
            MacOSKeyboardLayoutChangeSubscription.Create)
    { /* Empty */ }

    internal MacKeyboardLayoutService(
        Func<bool> isMainThread,
        SynchronizationContext? mainThreadContext,
        Func<(IntPtr LayoutData, IntPtr KeyboardLayout, byte KeyboardType)> loadKeyboardLayoutData,
        bool warmOnConstruction,
        Func<Action, IDisposable?>? subscribeToLayoutChanges = null)
    {
        _isMainThread = isMainThread ?? throw new ArgumentNullException(nameof(isMainThread));
        _mainThreadContext = mainThreadContext;
        _loadKeyboardLayoutData = loadKeyboardLayoutData ?? throw new ArgumentNullException(nameof(loadKeyboardLayoutData));
        _layoutChanges = subscribeToLayoutChanges?.Invoke(() => Volatile.Write(ref _layoutStale, 1));

        if (warmOnConstruction && _isMainThread())
        {
            _ = LoadAndCacheKeyboardLayoutData();
        }
    }

    public string GetKeyName(int keyCode)
    {
        var semanticName = GetSemanticKeyName(keyCode);
        if (semanticName is not null)
        {
            return semanticName;
        }

        // Try to get character first via UCKeyTranslate
        var c = GetCharFromKeyCode(keyCode, leftShift: false, rightShift: false, rightAlt: false, leftAlt: false, leftCtrl: false, capsLock: false);
        if (c is not null && !char.IsControl(c.Value))
        {
            return c.Value.ToString().ToUpper(CultureInfo.InvariantCulture);
        }

        return $"Key{keyCode.ToString(CultureInfo.InvariantCulture)}";
    }

    public int GetKeyCode(string keyName)
    {
        ArgumentNullException.ThrowIfNull(keyName);

        if (TryGetModifierKeyCode(keyName, out var modCode))
        {
            return modCode;
        }

        if (TryGetFunctionKeyCode(keyName, out var fnCode))
        {
            return fnCode;
        }

        var special = GetSpecialKeyCode(keyName);
        if (special != -1)
        {
            return special;
        }

        // Try to find by character
        if (keyName.Length is 1)
        {
            var input = GetInputForChar(keyName[0]);
            if (input is not null)
            {
                return input.Value.KeyCode;
            }
        }

        return -1;
    }

    private static bool TryGetModifierKeyCode(string keyName, out int code)
        => MacKeyboardLayoutPolicy.TryGetModifierKeyCode(keyName, out code);

    private static bool TryGetFunctionKeyCode(string keyName, out int code)
        => MacKeyboardLayoutPolicy.TryGetFunctionKeyCode(keyName, out code);

    private static int GetSpecialKeyCode(string keyName) => MacKeyboardLayoutPolicy.GetSpecialKeyCode(keyName);

    private static string? GetSemanticKeyName(int keyCode)
        => MacKeyboardLayoutPolicy.GetSemanticKeyName(keyCode);

    public char? GetCharFromKeyCode(int keyCode, bool leftShift, bool rightShift, bool rightAlt, bool leftAlt, bool leftCtrl, bool capsLock)
    {
        bool shift = leftShift || rightShift;
        bool option = leftAlt || rightAlt; // Option key on Mac

        // Don't produce chars for modifiers
        if (IsModifier(keyCode))
        {
            return null;
        }

        // Space special case
        if (keyCode is InputEventCode.KEY_SPACE)
        {
            return ' ';
        }

        // Convert evdev code to Mac key code
        ushort macKeyCode = KeyMap.ToMacKey(keyCode);
        if (macKeyCode is KeyMap.UnmappedKey || GetKeyboardLayoutData() == IntPtr.Zero)
        {
            return null;
        }

        uint modifierState = BuildUCKeyModifierState(shift, option, capsLock, leftCtrl);
        return TranslateKeyCode(macKeyCode, modifierState);
    }

    private static uint BuildUCKeyModifierState(bool shift, bool option, bool capsLock, bool leftCtrl)
        => MacKeyboardLayoutPolicy.BuildUCKeyModifierState(shift, option, capsLock, leftCtrl);

    private char? TranslateKeyCode(ushort macKeyCode, uint modifierState)
    {
        uint deadKeyState = 0;
        ushort[] output = new ushort[4];
        nuint actualLength;

        int result;
        lock (_layoutLock)
        {
            if (_disposed || _cachedKeyboardLayout == IntPtr.Zero)
            {
                return null;
            }

            result = CoreGraphics.UCKeyTranslate(
                _cachedKeyboardLayout,
                macKeyCode,
                CoreGraphics.kUCKeyActionDown,
                modifierState,
                _cachedKeyboardType,
                CoreGraphics.kUCKeyTranslateNoDeadKeysMask,
                ref deadKeyState,
                (nuint)output.Length,
                out actualLength,
                output);
        }

        char translated = (char)output[0];
        if (result is 0 && actualLength > 0 && !char.IsControl(translated))
        {
            return translated;
        }

        return null;
    }

    public (int KeyCode, bool Shift, bool AltGr)? GetInputForChar(char c)
    {
        lock (_lock)
        {
            if (_charToInputCache is null && !BuildCharInputCache())
            {
                return null;
            }

            return _charToInputCache!.TryGetValue(c, out var input) ? input : null;
        }
    }

    private bool BuildCharInputCache()
    {
        if (GetKeyboardLayoutData() == IntPtr.Zero)
        {
            return false;
        }

        var charToInputCache = new Dictionary<char, (int KeyCode, bool Shift, bool AltGr)>();

        // Scan all key codes with different modifiers
        for (int code = 1; code < 128; code++)
        {
            if (IsModifier(code))
            {
                continue;
            }

            // No modifiers
            TryAddCharToCache(charToInputCache, code, shift: false, option: false);
            // Shift
            TryAddCharToCache(charToInputCache, code, shift: true, option: false);
            // Option (AltGr equivalent on Mac)
            TryAddCharToCache(charToInputCache, code, shift: false, option: true);
            // Shift + Option
            TryAddCharToCache(charToInputCache, code, shift: true, option: true);
        }

        _charToInputCache = charToInputCache;
        return true;
    }

    private void TryAddCharToCache(Dictionary<char, (int KeyCode, bool Shift, bool AltGr)> charToInputCache, int code, bool shift, bool option)
    {
        var c = GetCharFromKeyCode(code, shift, rightShift: false, option, leftAlt: false, leftCtrl: false, capsLock: false);
        if (c is not null && !charToInputCache.ContainsKey(c.Value))
        {
            charToInputCache[c.Value] = (code, shift, option);
        }
    }

    private IntPtr GetKeyboardLayoutData()
    {
        DropStaleLayout();
        lock (_layoutLock)
        {
            if (_disposed)
            {
                return IntPtr.Zero;
            }

            if (_cachedKeyboardLayout != IntPtr.Zero)
            {
                return _cachedKeyboardLayout;
            }
        }

        if (_isMainThread())
        {
            return LoadAndCacheKeyboardLayoutData();
        }

        if (_mainThreadContext is null)
        {
            return IntPtr.Zero;
        }

        IntPtr layoutData = IntPtr.Zero;
        try
        {
            _mainThreadContext.Send(_ => layoutData = LoadAndCacheKeyboardLayoutData(), state: null);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return IntPtr.Zero;
        }

        return layoutData;
    }

    private IntPtr LoadAndCacheKeyboardLayoutData()
    {
        lock (_layoutLock)
        {
            if (_disposed)
            {
                return IntPtr.Zero;
            }

            if (_cachedKeyboardLayout != IntPtr.Zero)
            {
                return _cachedKeyboardLayout;
            }

            if (!_isMainThread())
            {
                return IntPtr.Zero;
            }

            var layoutData = _loadKeyboardLayoutData();
            _cachedLayoutData = layoutData.LayoutData;
            _cachedKeyboardLayout = layoutData.KeyboardLayout;
            _cachedKeyboardType = layoutData.KeyboardType;
            return _cachedKeyboardLayout;
        }
    }

    private static (IntPtr LayoutData, IntPtr KeyboardLayout, byte KeyboardType) LoadNativeKeyboardLayoutData()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return default;
        }

        IntPtr inputSource = IntPtr.Zero;
        IntPtr retainedLayoutData = IntPtr.Zero;

        try
        {
            inputSource = AcquireInputSource();
            if (inputSource == IntPtr.Zero)
            {
                return default;
            }

            // Get the property key for keyboard layout data
            IntPtr propertyKey = CoreGraphics.kTISPropertyUnicodeKeyLayoutData;
            if (propertyKey == IntPtr.Zero)
            {
                ReleaseInputSource(ref inputSource);
                return default;
            }

            // Get the layout data
            IntPtr layoutData = CoreGraphics.TISGetInputSourceProperty(inputSource, propertyKey);
            if (layoutData == IntPtr.Zero)
            {
                ReleaseInputSource(ref inputSource);
                return default;
            }

            retainedLayoutData = CoreFoundation.CFRetain(layoutData);
            if (retainedLayoutData == IntPtr.Zero)
            {
                ReleaseInputSource(ref inputSource);
                return default;
            }

            // Get the actual byte pointer from CFData
            var keyboardLayout = CoreFoundation.CFDataGetBytePtr(retainedLayoutData);
            if (keyboardLayout == IntPtr.Zero)
            {
                CleanupKeyboardResources(ref inputSource, ref retainedLayoutData);
                return default;
            }

            var keyboardType = CoreGraphics.LMGetKbdType();
            ReleaseInputSource(ref inputSource);
            return (retainedLayoutData, keyboardLayout, keyboardType);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            CleanupKeyboardResources(ref inputSource, ref retainedLayoutData);
            return default;
        }
    }

    private static IntPtr AcquireInputSource()
    {
        IntPtr inputSource = CoreGraphics.TISCopyCurrentKeyboardLayoutInputSource();
        if (inputSource == IntPtr.Zero)
        {
            inputSource = CoreGraphics.TISCopyCurrentKeyboardInputSource();
        }

        return inputSource;
    }

    private static void CleanupKeyboardResources(ref IntPtr inputSource, ref IntPtr retainedLayoutData)
    {
        if (retainedLayoutData != IntPtr.Zero)
        {
            ReleaseLayoutData(ref retainedLayoutData);
        }

        if (inputSource != IntPtr.Zero)
        {
            ReleaseInputSource(ref inputSource);
        }
    }

    private static void ReleaseInputSource(ref IntPtr inputSource)
    {
        if (inputSource == IntPtr.Zero)
        {
            return;
        }

        var sourceToRelease = inputSource;
        inputSource = IntPtr.Zero;
        CoreFoundation.CFRelease(sourceToRelease);
    }

    private static void ReleaseLayoutData(ref IntPtr layoutData)
    {
        if (layoutData == IntPtr.Zero)
        {
            return;
        }

        var dataToRelease = layoutData;
        layoutData = IntPtr.Zero;
        CoreFoundation.CFRelease(dataToRelease);
    }

    private static bool IsModifier(int keyCode)
        => MacKeyboardLayoutPolicy.IsModifier(keyCode);

    public void Dispose()
    {
        _layoutChanges?.Dispose();

        lock (_layoutLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ReleaseCachedLayout();
        }

        GC.SuppressFinalize(this);
    }

    private void ReleaseCachedLayout()
    {
        if (_cachedLayoutData != IntPtr.Zero && OperatingSystem.IsMacOS())
        {
            CoreFoundation.CFRelease(_cachedLayoutData);
        }

        _cachedLayoutData = IntPtr.Zero;
        _cachedKeyboardLayout = IntPtr.Zero;
        _cachedKeyboardType = 0;
    }

    /// <summary>Drops the cached layout and character map once the system reported a keyboard layout change.</summary>
    private void DropStaleLayout()
    {
        if (Interlocked.Exchange(ref _layoutStale, 0) is 0)
        {
            return;
        }

        lock (_layoutLock)
        {
            ReleaseCachedLayout();
        }

        lock (_lock)
        {
            _charToInputCache = null;
        }
    }
}
