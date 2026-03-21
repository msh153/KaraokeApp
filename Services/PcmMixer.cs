namespace KaraokeApp.Services;

/// <summary>
/// Mixes multiple 16-bit PCM mono streams into one output stream.
/// Uses soft-clipping to prevent distortion when all channels are loud.
/// </summary>
public static class PcmMixer
{
    /// <summary>
    /// Mix up to 3 mono PCM streams (mic1, mic2, music backing track).
    /// All inputs must be 16-bit signed LE, mono, same sample rate.
    /// Output is 16-bit signed LE mono.
    /// </summary>
    public static byte[] Mix(
        ReadOnlySpan<byte> mic1,   float vol1,
        ReadOnlySpan<byte> mic2,   float vol2,
        ReadOnlySpan<byte> music,  float volM)
    {
        // Find the shortest non-empty length (in samples)
        int samples = 0;
        if (mic1.Length > 0)  samples = Math.Max(samples, mic1.Length / 2);
        if (mic2.Length > 0)  samples = Math.Max(samples, mic2.Length / 2);
        if (music.Length > 0) samples = Math.Max(samples, music.Length / 2);

        if (samples == 0) return Array.Empty<byte>();

        var output = new byte[samples * 2];

        for (int i = 0; i < samples; i++)
        {
            float mixed = 0f;

            if (i * 2 + 1 < mic1.Length)
                mixed += BitConverter.ToInt16(mic1.Slice(i * 2, 2)) * vol1;

            if (i * 2 + 1 < mic2.Length)
                mixed += BitConverter.ToInt16(mic2.Slice(i * 2, 2)) * vol2;

            if (i * 2 + 1 < music.Length)
                mixed += BitConverter.ToInt16(music.Slice(i * 2, 2)) * volM;

            // Soft clip via tanh to prevent harsh distortion
            var clipped = SoftClip(mixed);
            var sample = (short)clipped;

            output[i * 2]     = (byte)(sample & 0xFF);
            output[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }

        return output;
    }

    /// <summary>Soft-clip using tanh (smoother than hard-clip at ±32767).</summary>
    private static float SoftClip(float x)
    {
        const float threshold = 28000f;
        if (x > threshold)
            return threshold + (32767f - threshold) * (float)Math.Tanh((x - threshold) / 10000f);
        if (x < -threshold)
            return -threshold + (-32767f + threshold) * (float)Math.Tanh((-x - threshold) / 10000f);
        return x;
    }

    /// <summary>Calculate RMS level 0..1 for a PCM buffer.</summary>
    public static float CalculateRms(ReadOnlySpan<byte> pcm)
    {
        if (pcm.Length < 2) return 0f;

        long sumSq = 0;
        int sampleCount = pcm.Length / 2;

        for (int i = 0; i < sampleCount; i++)
        {
            short s = BitConverter.ToInt16(pcm.Slice(i * 2, 2));
            sumSq += (long)s * s;
        }

        var rms = Math.Sqrt((double)sumSq / sampleCount);
        return (float)Math.Min(rms / 32768.0, 1.0);
    }
}
