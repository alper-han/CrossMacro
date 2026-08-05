namespace CrossMacro.Core.Services;

/// <summary>
/// Provides inclusive, cryptographically unbiased integer samples.
/// </summary>
/// <remarks>
/// The helper is intentionally stateless so Core domain policies and outer
/// runtime adapters can share the same inclusive-range semantics without
/// granting either assembly friend access to Core implementation details.
/// </remarks>
public static class RandomNumberGeneratorUtility
{
    /// <summary>Returns a uniformly distributed value in the inclusive range.</summary>
    public static int GetInt32Inclusive(int min, int max)
    {
        if (min > max)
        {
            throw new ArgumentOutOfRangeException(nameof(min), "min must be less than or equal to max.");
        }

        var range = Convert.ToUInt64(Convert.ToInt64(max) - min) + 1;
        var limit = (1UL << 32) - ((1UL << 32) % range);
        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        uint sample;

        do
        {
            System.Security.Cryptography.RandomNumberGenerator.Fill(buffer);
            sample = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        }
        while (sample >= limit);

        return Convert.ToInt32(Convert.ToInt64(min) + Convert.ToInt64(sample % range));
    }
}
