namespace CrossMacro.Core.Services;

internal static class RandomNumberGeneratorUtility
{
    internal static int GetInt32Inclusive(int min, int max)
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
