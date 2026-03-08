namespace CrossMacro.Platform.Linux.Ipc;

internal static class CaptureStartFailureReconciler
{
    public static bool ShouldReconcile(
        CaptureCommand currentRequiredCommand,
        CaptureCommand failedCommand,
        bool allowSameCommandRetry,
        bool subscriptionRemovedSinceStart,
        bool rollbackChangedSubscriptions)
    {
        if (currentRequiredCommand.Type != CaptureCommandType.Start)
        {
            return false;
        }

        if (currentRequiredCommand != failedCommand)
        {
            return true;
        }

        if (subscriptionRemovedSinceStart)
        {
            return true;
        }

        if (rollbackChangedSubscriptions)
        {
            return true;
        }

        return allowSameCommandRetry;
    }
}
