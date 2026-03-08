using CrossMacro.Platform.Linux.Ipc;
using CrossMacro.TestInfrastructure;
using Xunit;

namespace CrossMacro.Platform.Linux.Tests.Services.Ipc;

public class CaptureSubscriptionCoordinatorTests
{
    private static CaptureCommand SetSubscription(
        CaptureSubscriptionCoordinator coordinator,
        string consumerId,
        bool captureMouse,
        bool captureKeyboard)
    {
        coordinator.SetSubscription(consumerId, captureMouse, captureKeyboard);
        var command = coordinator.GetRequiredCommand();
        if (command.Type != CaptureCommandType.None)
        {
            coordinator.MarkCommandIssued(command);
        }

        return command;
    }

    private static CaptureCommand RemoveSubscription(CaptureSubscriptionCoordinator coordinator, string consumerId)
    {
        coordinator.RemoveSubscription(consumerId);
        var command = coordinator.GetRequiredCommand();
        if (command.Type != CaptureCommandType.None)
        {
            coordinator.MarkCommandIssued(command);
        }

        return command;
    }

    private static CaptureCommand ResetTransportStateAndGetCommand(CaptureSubscriptionCoordinator coordinator)
    {
        coordinator.ResetTransportState();
        var command = coordinator.GetRequiredCommand();
        if (command.Type != CaptureCommandType.None)
        {
            coordinator.MarkCommandIssued(command);
        }

        return command;
    }

    private static List<CaptureCommand> RecordCommands(params CaptureCommand[] commands)
    {
        var sent = new List<CaptureCommand>();
        foreach (var command in commands)
        {
            if (command.Type != CaptureCommandType.None)
            {
                sent.Add(command);
            }
        }
        return sent;
    }

    [LinuxFact]
    public void SetSubscription_FirstConsumer_ShouldRequestStart()
    {
        var coordinator = new CaptureSubscriptionCoordinator();

        var command = SetSubscription(coordinator, "hotkeys", captureMouse: true, captureKeyboard: false);

        Assert.Equal(CaptureCommandType.Start, command.Type);
        Assert.True(command.CaptureMouse);
        Assert.False(command.CaptureKeyboard);
    }

    [LinuxFact]
    public void SetSubscription_WhenAggregateChanges_ShouldRequestStartWithMergedFlags()
    {
        var coordinator = new CaptureSubscriptionCoordinator();
        _ = SetSubscription(coordinator, "hotkeys", captureMouse: true, captureKeyboard: false);

        var command = SetSubscription(coordinator, "text-expansion", captureMouse: false, captureKeyboard: true);

        Assert.Equal(CaptureCommandType.Start, command.Type);
        Assert.True(command.CaptureMouse);
        Assert.True(command.CaptureKeyboard);
    }

    [LinuxFact]
    public void SetSubscription_WhenAggregateUnchanged_ShouldReturnNone()
    {
        var coordinator = new CaptureSubscriptionCoordinator();
        _ = SetSubscription(coordinator, "hotkeys", captureMouse: true, captureKeyboard: true);

        coordinator.SetSubscription("recorder", captureMouse: true, captureKeyboard: true);
        var command = coordinator.GetRequiredCommand();

        Assert.Equal(CaptureCommandType.None, command.Type);
    }

    [LinuxFact]
    public void RemoveSubscription_WhenRemainingAggregateChanges_ShouldRequestUpdatedStart()
    {
        var coordinator = new CaptureSubscriptionCoordinator();
        _ = SetSubscription(coordinator, "hotkeys", captureMouse: true, captureKeyboard: false);
        _ = SetSubscription(coordinator, "recorder", captureMouse: false, captureKeyboard: true);

        var command = RemoveSubscription(coordinator, "recorder");

        Assert.Equal(CaptureCommandType.Start, command.Type);
        Assert.True(command.CaptureMouse);
        Assert.False(command.CaptureKeyboard);
    }

    [LinuxFact]
    public void RemoveSubscription_WhenLastConsumerRemoved_ShouldRequestStop()
    {
        var coordinator = new CaptureSubscriptionCoordinator();
        _ = SetSubscription(coordinator, "hotkeys", captureMouse: false, captureKeyboard: true);

        var command = RemoveSubscription(coordinator, "hotkeys");

        Assert.Equal(CaptureCommandType.Stop, command.Type);
    }

    [LinuxFact]
    public void ResetTransportStateAndGetCommand_WhenSubscriptionsExist_ShouldReissueStart()
    {
        var coordinator = new CaptureSubscriptionCoordinator();
        _ = SetSubscription(coordinator, "hotkeys", captureMouse: true, captureKeyboard: true);

        var command = ResetTransportStateAndGetCommand(coordinator);

        Assert.Equal(CaptureCommandType.Start, command.Type);
        Assert.True(command.CaptureMouse);
        Assert.True(command.CaptureKeyboard);
    }

    [LinuxFact]
    public void MultiServiceScenario_ShouldNotStopUntilLastServiceUnsubscribes()
    {
        var coordinator = new CaptureSubscriptionCoordinator();

        var sent = RecordCommands(
            SetSubscription(coordinator, "global-hotkeys", captureMouse: false, captureKeyboard: true),
            SetSubscription(coordinator, "macro-recorder", captureMouse: true, captureKeyboard: true),
            SetSubscription(coordinator, "text-expansion", captureMouse: false, captureKeyboard: true),
            RemoveSubscription(coordinator, "text-expansion"),
            RemoveSubscription(coordinator, "macro-recorder"),
            RemoveSubscription(coordinator, "global-hotkeys"));

        Assert.Equal(4, sent.Count);

        Assert.Equal(CaptureCommandType.Start, sent[0].Type);
        Assert.False(sent[0].CaptureMouse);
        Assert.True(sent[0].CaptureKeyboard);

        Assert.Equal(CaptureCommandType.Start, sent[1].Type);
        Assert.True(sent[1].CaptureMouse);
        Assert.True(sent[1].CaptureKeyboard);

        Assert.Equal(CaptureCommandType.Start, sent[2].Type);
        Assert.False(sent[2].CaptureMouse);
        Assert.True(sent[2].CaptureKeyboard);

        Assert.Equal(CaptureCommandType.Stop, sent[3].Type);
    }

    [LinuxFact]
    public void ReconnectScenario_ShouldReissueCurrentAggregateCapture()
    {
        var coordinator = new CaptureSubscriptionCoordinator();

        var sent = RecordCommands(
            SetSubscription(coordinator, "global-hotkeys", captureMouse: false, captureKeyboard: true),
            SetSubscription(coordinator, "macro-recorder", captureMouse: true, captureKeyboard: true),
            ResetTransportStateAndGetCommand(coordinator),
            RemoveSubscription(coordinator, "macro-recorder"),
            ResetTransportStateAndGetCommand(coordinator),
            RemoveSubscription(coordinator, "global-hotkeys"),
            ResetTransportStateAndGetCommand(coordinator));

        Assert.Equal(6, sent.Count);

        Assert.Equal(CaptureCommandType.Start, sent[0].Type);
        Assert.False(sent[0].CaptureMouse);
        Assert.True(sent[0].CaptureKeyboard);

        Assert.Equal(CaptureCommandType.Start, sent[1].Type);
        Assert.True(sent[1].CaptureMouse);
        Assert.True(sent[1].CaptureKeyboard);

        Assert.Equal(CaptureCommandType.Start, sent[2].Type);
        Assert.True(sent[2].CaptureMouse);
        Assert.True(sent[2].CaptureKeyboard);

        Assert.Equal(CaptureCommandType.Start, sent[3].Type);
        Assert.False(sent[3].CaptureMouse);
        Assert.True(sent[3].CaptureKeyboard);

        Assert.Equal(CaptureCommandType.Start, sent[4].Type);
        Assert.False(sent[4].CaptureMouse);
        Assert.True(sent[4].CaptureKeyboard);

        Assert.Equal(CaptureCommandType.Stop, sent[5].Type);
    }
}
