namespace CrossMacro.Application.Execution;

/// <summary>Reads validated encoded image assets without exposing native frame or codec ownership to application workflows.</summary>
public interface IRunImageAssetReader
{
    public Task<byte[]> ReadValidatedFileAsync(string filePath, string assetName, CancellationToken cancellationToken);
    public void ValidateMacroBudget(long totalEncodedBytes);
}
