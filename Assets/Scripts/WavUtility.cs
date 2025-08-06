using System;
using UnityEngine;

public static class WavUtility
{
    // This is the thread-safe method for background processing
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
}