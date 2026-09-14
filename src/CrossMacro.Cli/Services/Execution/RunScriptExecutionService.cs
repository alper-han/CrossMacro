namespace CrossMacro.Cli.Services.Execution;

/// <summary>Adapts the host image codec to the shared application script workflow.</summary>
public sealed class RunScriptExecutionService(IRunExecutionService runtimeService, IImageAssetCodec? imageAssetCodec = null) : IRunScriptExecutionService
{
    private readonly RunScriptExecutionWorkflow _workflow = new(runtimeService,
        imageAssetCodec is null ? null : new ImageAssetReader(imageAssetCodec));

    public Task<MacroExecutionResult> ExecuteAsync(RunScriptExecutionRequest request, CancellationToken cancellationToken) =>
        _workflow.ExecuteAsync(request, cancellationToken);

    private sealed class ImageAssetReader(IImageAssetCodec codec) : IRunImageAssetReader
    {
        public async Task<byte[]> ReadValidatedFileAsync(string filePath, string assetName, CancellationToken cancellationToken)
        {
            var bytes = await codec.ReadFileAsync(filePath, assetName, cancellationToken).ConfigureAwait(false);
            using var frame = await codec.DecodePngAsync(bytes, assetName, cancellationToken).ConfigureAwait(false);
            return bytes;
        }

        public void ValidateMacroBudget(long totalEncodedBytes) => codec.ValidateMacroBudget(totalEncodedBytes);
    }
}
