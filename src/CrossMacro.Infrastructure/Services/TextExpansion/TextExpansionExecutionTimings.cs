using System;

namespace CrossMacro.Infrastructure.Services.TextExpansion;

internal static class TextExpansionExecutionTimings
{
    public static readonly TimeSpan ClipboardBackupReadTimeout = TimeSpan.FromMilliseconds(100);
    public static readonly TimeSpan ClipboardWriteTimeout = TimeSpan.FromMilliseconds(100);
    public static readonly TimeSpan ClipboardWriteSettleDelay = TimeSpan.FromMilliseconds(50);
    public static readonly TimeSpan ClipboardPrePasteDelay = TimeSpan.FromMilliseconds(50);
    public static readonly TimeSpan PasteSettleDelay = TimeSpan.FromMilliseconds(150);
    public static readonly TimeSpan DirectTypingNewLineDelay = TimeSpan.FromMilliseconds(1);
    public static readonly TimeSpan DirectTypingInterElementDelay = TimeSpan.FromMilliseconds(1);
    public static readonly TimeSpan LinuxUnicodeComposeActivationDelay = TimeSpan.FromMilliseconds(1);
    public static readonly TimeSpan LinuxUnicodeComposeInterKeyDelay = TimeSpan.FromMilliseconds(1);
    public static readonly TimeSpan LinuxUnicodeComposeCompletionDelay = TimeSpan.FromMilliseconds(1);
    public static readonly TimeSpan KeyPressReleaseDelay = TimeSpan.FromMilliseconds(1);
    public static readonly TimeSpan ModifierReleaseTimeout = TimeSpan.FromMilliseconds(2000);
    public static readonly TimeSpan ModifierReleasePollInterval = TimeSpan.FromMilliseconds(50);
    public static readonly TimeSpan ClipboardRestoreDelay = TimeSpan.FromMilliseconds(1000);
    public static readonly TimeSpan ClipboardRestoreTimeout = TimeSpan.FromMilliseconds(400);
}
