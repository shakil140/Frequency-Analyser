using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class FrequencyPrinter : MonoBehaviour
{
    public float updateInterval = 0.1f;
    public float volumeThreshold = 0.01f;
    public float silenceThreshold = 0.5f;
    public TMP_Text frequencyText;
    public LineRenderer lineRenderer;

    private AudioClip microphoneClip;
    private int sampleRate;
    private const int SAMPLE_SIZE = 4096; // This should be a power of 2

    private List<float> currentSpeechBuffer = new List<float>();
    private float lastSpeechTime;
    private bool isSpeaking = false;

    void Start()
    {
        sampleRate = AudioSettings.outputSampleRate;
        Debug.Log($"Sample rate: {sampleRate}");
        StartCoroutine(ContinuouslyRecordAndAnalyze());

        lineRenderer.positionCount = SAMPLE_SIZE;
        lineRenderer.startWidth = 0.01f;
        lineRenderer.endWidth = 0.01f;
    }

    IEnumerator ContinuouslyRecordAndAnalyze()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("No microphone detected!");
            yield break;
        }

        string microphoneName = Microphone.devices[0];
        Debug.Log($"Using microphone: {microphoneName}");

        microphoneClip = Microphone.Start(microphoneName, true, 1, sampleRate);

        yield return new WaitForSeconds(0.1f);

        float[] samples = new float[SAMPLE_SIZE];

        while (true)
        {
            int position = Microphone.GetPosition(microphoneName);
            if (position < SAMPLE_SIZE) continue;

            microphoneClip.GetData(samples, position - SAMPLE_SIZE);
            ProcessAudioSamples(samples);
            yield return new WaitForSeconds(updateInterval);
        }
    }

    void ProcessAudioSamples(float[] samples)
    {
        float maxAmplitude = samples.Max(Mathf.Abs);

        for (int i = 0; i < samples.Length; i++)
        {
            lineRenderer.SetPosition(i, new Vector3(i / (float)SAMPLE_SIZE, samples[i], 0));
        }

        if (maxAmplitude > volumeThreshold)
        {
            if (!isSpeaking)
            {
                isSpeaking = true;
                currentSpeechBuffer.Clear();
            }
            currentSpeechBuffer.AddRange(samples);
            lastSpeechTime = Time.time;
        }
        else if (isSpeaking && Time.time - lastSpeechTime > silenceThreshold)
        {
            isSpeaking = false;
            CalculateAndPrintFrequency(currentSpeechBuffer.ToArray());
            currentSpeechBuffer.Clear();
        }
    }

    void CalculateAndPrintFrequency(float[] samples)
    {
        if (samples.Length == 0)
        {
            Debug.Log("No speech detected");
            return;
        }

        try
        {
            float frequency = EstimateFrequency(samples, sampleRate);
            Debug.Log($"Detected frequency: {frequency:F2} Hz");
            frequencyText.text = $"Frequency: {frequency:F2} Hz";
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in frequency estimation: {e.Message}");
        }
    }

    float EstimateFrequency(float[] data, int sampleRate)
    {
        int n = Mathf.NextPowerOfTwo(data.Length);
        float[] paddedData = new float[n];
        Array.Copy(data, paddedData, data.Length);

        float[] fft = new float[n * 2]; // Real and imaginary parts
        FFT(paddedData, fft);

        float[] magnitude = new float[n / 2];
        for (int i = 0; i < n / 2; i++)
        {
            magnitude[i] = Mathf.Sqrt(fft[2 * i] * fft[2 * i] + fft[2 * i + 1] * fft[2 * i + 1]);
        }

        int peakIndex = Array.IndexOf(magnitude, magnitude.Max());
        float frequency = peakIndex * sampleRate / n;

        return frequency;
    }

    void FFT(float[] data, float[] fft)
    {
        int n = data.Length;
        int m = (int)Mathf.Log(n, 2);

        for (int i = 0; i < n; i++)
        {
            int j = BitReverse(i, m);
            fft[2 * j] = data[i];
            fft[2 * j + 1] = 0;
        }

        for (int s = 1; s <= m; s++)
        {
            int m2 = 1 << s;
            int m1 = m2 >> 1;
            float wReal = 1;
            float wImag = 0;
            float theta = Mathf.PI / m1;
            float wTempReal = Mathf.Cos(theta);
            float wTempImag = -Mathf.Sin(theta);

            for (int j = 0; j < m1; j++)
            {
                for (int k = j; k < n; k += m2)
                {
                    int k1 = k + m1;
                    float tReal = wReal * fft[2 * k1] - wImag * fft[2 * k1 + 1];
                    float tImag = wReal * fft[2 * k1 + 1] + wImag * fft[2 * k1];
                    fft[2 * k1] = fft[2 * k] - tReal;
                    fft[2 * k1 + 1] = fft[2 * k + 1] - tImag;
                    fft[2 * k] += tReal;
                    fft[2 * k + 1] += tImag;
                }

                float tempReal = wReal * wTempReal - wImag * wTempImag;
                wImag = wReal * wTempImag + wImag * wTempReal;
                wReal = tempReal;
            }
        }
    }

    int BitReverse(int x, int bits)
    {
        int y = 0;
        for (int i = 0; i < bits; i++)
        {
            y = (y << 1) | (x & 1);
            x >>= 1;
        }
        return y;
    }
}
