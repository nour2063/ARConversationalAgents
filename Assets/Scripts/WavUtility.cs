using System;
using System.Text;
using UnityEngine;

public static class WavUtility
{
    public static float[] GetSamplesFromWav(byte[] wavBytes, out int channels, out int sampleRate)
    {
        // Basic WAV header parsing
        // Assumes 16-bit PCM audio, which is standard for TTS output
        try
        {
            channels = BitConverter.ToInt16(wavBytes, 22);
            sampleRate = BitConverter.ToInt32(wavBytes, 24);

            const int headerOffset = 44; // Standard WAV header size
            var dataSize = wavBytes.Length - headerOffset;
            var sampleCount = dataSize / 2; // 2 bytes per 16-bit sample

            var samples = new float[sampleCount];

            for (var i = 0; i < sampleCount; i++)
            {
                var sampleShort = BitConverter.ToInt16(wavBytes, headerOffset + i * 2);
                samples[i] = sampleShort / 32768.0f; // Convert to float range -1 to 1
            }

            return samples;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error parsing WAV data: {e.Message}");
            channels = 0;
            sampleRate = 0;
            return null;
        }
    }

    public static byte[] ConvertToWav(float[] samples, int channels, int frequency)
    {
        using var memoryStream = new System.IO.MemoryStream();
        using var writer = new System.IO.BinaryWriter(memoryStream, Encoding.UTF8);
        // WAV header
        writer.Write(Encoding.UTF8.GetBytes("RIFF"));
        writer.Write(36 + samples.Length * 2);
        writer.Write(Encoding.UTF8.GetBytes("WAVE"));
        writer.Write(Encoding.UTF8.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1); // Audio format 1 = PCM
        writer.Write((short)channels);
        writer.Write(frequency);
        writer.Write(frequency * channels * 2); // Byte rate
        writer.Write((short)(channels * 2)); // Block align
        writer.Write((short)16); // Bits per sample
        writer.Write(Encoding.UTF8.GetBytes("data"));
        writer.Write(samples.Length * 2);

        // Convert float samples to 16-bit PCM
        foreach (var sample in samples)
        {
            writer.Write((short)(sample * 32767));
        }

        return memoryStream.ToArray();
    }
}
