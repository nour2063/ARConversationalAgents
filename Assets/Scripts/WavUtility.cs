using UnityEngine;
using System;

public static class WavUtility
{
    public static AudioClip ToAudioClip(byte[] wavBytes)
    {
        int headerSize = 44; 
        if (wavBytes.Length < headerSize)
        {
            Debug.LogError("WavUtility: Invalid WAV file - header too short.");
            return null;
        }

        int sampleRate = BitConverter.ToInt32(wavBytes, 24);
        int channels = BitConverter.ToInt16(wavBytes, 22);
        int bitsPerSample = BitConverter.ToInt16(wavBytes, 34);

        if (bitsPerSample != 16)
        {
            Debug.LogError("WavUtility: Only 16-bit WAV is supported by this utility. Received " + bitsPerSample + " bits.");
            return null;
        }

        int dataSize = BitConverter.ToInt32(wavBytes, 40); 
        if (wavBytes.Length < headerSize + dataSize)
        {
            Debug.LogError("WavUtility: Invalid WAV file - data size mismatch.");
            return null;
        }

        float[] samples = new float[dataSize / 2]; 

        for (int i = 0; i < samples.Length; i++)
        {
            short sample = BitConverter.ToInt16(wavBytes, headerSize + i * 2);
            samples[i] = sample / 32768f; // Normalize to -1.0 to 1.0 range
        }

        AudioClip audioClip = AudioClip.Create("SynthesizedSpeech", samples.Length, channels, sampleRate, false);
        audioClip.SetData(samples, 0);
        return audioClip;
    }
}