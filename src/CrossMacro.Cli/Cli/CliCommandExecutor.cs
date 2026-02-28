using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.Cli;

public sealed class CliCommandExecutor
{
    private readonly IServiceProvider _serviceProvider;

    public CliCommandExecutor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<int> ExecuteAsync(CliCommandOptions options, CancellationToken cancellationToken)
    {
        var result = await ExecuteWithResolvedHandlerAsync(options, cancellationToken);
        CliOutputFormatter.Write(result, options.JsonOutput);
        return result.ExitCode;
    }

    private async Task<CliCommandExecutionResult> ExecuteWithResolvedHandlerAsync(CliCommandOptions options, CancellationToken cancellationToken)
    {
        switch (options)
        {
            case MacroValidateCliOptions typed:
                return await ExecuteAsync<MacroValidateCommandHandler>(typed, cancellationToken);
            case MacroInfoCliOptions typed:
                return await ExecuteAsync<MacroInfoCommandHandler>(typed, cancellationToken);
            case PlayCliOptions typed:
                return await ExecuteAsync<PlayCommandHandler>(typed, cancellationToken);
            case DoctorCliOptions typed:
                return await ExecuteAsync<DoctorCommandHandler>(typed, cancellationToken);
            case SettingsGetCliOptions typed:
                return await ExecuteAsync<SettingsGetCommandHandler>(typed, cancellationToken);
            case SettingsSetCliOptions typed:
                return await ExecuteAsync<SettingsSetCommandHandler>(typed, cancellationToken);
            case ScheduleListCliOptions typed:
                return await ExecuteAsync<ScheduleListCommandHandler>(typed, cancellationToken);
            case ScheduleRunCliOptions typed:
                return await ExecuteAsync<ScheduleRunCommandHandler>(typed, cancellationToken);
            case ShortcutListCliOptions typed:
                return await ExecuteAsync<ShortcutListCommandHandler>(typed, cancellationToken);
            case ShortcutRunCliOptions typed:
                return await ExecuteAsync<ShortcutRunCommandHandler>(typed, cancellationToken);
            case RecordCliOptions typed:
                return await ExecuteAsync<RecordCommandHandler>(typed, cancellationToken);
            case RunCliOptions typed:
                return await ExecuteAsync<RunCommandHandler>(typed, cancellationToken);
            case HeadlessCliOptions typed:
                return await ExecuteAsync<HeadlessCommandHandler>(typed, cancellationToken);
            default:
                return CliCommandExecutionResult.Fail(
                    CliExitCode.InvalidArguments,
                    $"No handler registered for command options type: {options.GetType().Name}");
        }
    }

    private async Task<CliCommandExecutionResult> ExecuteAsync<THandler>(CliCommandOptions options, CancellationToken cancellationToken)
        where THandler : ICliCommandHandler
    {
        var handler = _serviceProvider.GetRequiredService<THandler>();
        return await handler.ExecuteAsync(options, cancellationToken);
    }
}
