using System;

namespace CrossMacro.Infrastructure.Services.TextExpansion;

internal static class TextExpansionExecutionTimings
{
    public static readonly TimeSpan ClipboardBackupReadTimeout = TimeSpan.FromMilliseconds(750);
    public static readonly TimeSpan ClipboardWriteTimeout = TimeSpan.FromMilliseconds(1500);
    public static readonly TimeSpan ClipboardVerifyTimeout = TimeSpan.FromMilliseconds(1500);
    public static readonly TimeSpan ClipboardWriteSettleDelay = TimeSpan.FromMilliseconds(10);
    public static readonly TimeSpan ClipboardPrePasteDelay = TimeSpan.Zero;
    public static readonly TimeSpan PasteSettleDelay = TimeSpan.FromMilliseconds(50);
    public static readonly TimeSpan DirectTypingNewLineDelay = TimeSpan.FromMilliseconds(1);
    public static readonly TimeSpan DirectTypingInterElementDelay = TimeSpan.FromMilliseconds(1);
    public static readonly TimeSpan BatchedDirectTypingInterElementDelay = TimeSpan.Zero;
    public static readonly TimeSpan BatchedKeyPressReleaseDelay = TimeSpan.FromMilliseconds(1);
    public static readonly TimeSpan TriggerKeyReleaseWaitTimeout = TimeSpan.FromMilliseconds(100);
    public static readonly TimeSpan LinuxUnicodeComposeActivationDelay = TimeSpan.FromMilliseconds(1);
    public static readonly TimeSpan LinuxUnicodeComposeInterKeyDelay = TimeSpan.FromMilliseconds(1);
    public static readonly TimeSpan LinuxUnicodeComposeCompletionDelay = TimeSpan.FromMilliseconds(1);
    public static readonly TimeSpan KeyPressReleaseDelay = TimeSpan.FromMilliseconds(1);
    public static readonly TimeSpan ModifierReleaseTimeout = TimeSpan.FromMilliseconds(2000);
    public static readonly TimeSpan ModifierReleasePollInterval = TimeSpan.FromMilliseconds(50);
    public static readonly TimeSpan ClipboardRestoreDelay = TimeSpan.Zero;
    public static readonly TimeSpan ClipboardRestoreTimeout = TimeSpan.FromMilliseconds(1500);
}
