using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CustomAudioFrequencyAlayser : MonoBehaviour
{
    private AudioSource audioSource;
    private const int SAMPLE_RATE = 44100;
    private const int RECORDING_LENGTH = 1; // 1 second
    private const int FFT_SIZE = 8192;
    private const int MOVING_AVERAGE_SIZE = 5;
    private const float NOISE_THRESHOLD = 0.01f;

    private float[] spectrum = new float[FFT_SIZE];
    private Queue<float> frequencyHistory = new Queue<float>();

    [SerializeField]
    private bool showDebugLog = true;

    void Start()
    {
        SetupAudioRecording();
    }

    void Update()
    {
        AnalyzeAudio();
    }

    private void SetupAudioRecording()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = Microphone.Start(null, true, RECORDING_LENGTH, SAMPLE_RATE);
        audioSource.loop = true;

        // Wait until the recording has started
        while (!(Microphone.GetPosition(null) > 0)) { }

        audioSource.Play();
    }

    private void AnalyzeAudio()
    {
        // Get the current spectrum data
        audioSource.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);

        // Find the peak frequency
        float maxAmplitude = spectrum.Max();
        int maxIndex = System.Array.IndexOf(spectrum, maxAmplitude);
        float dominantFrequency = maxIndex * SAMPLE_RATE / 2 / FFT_SIZE;

        // Apply moving average filter
        frequencyHistory.Enqueue(dominantFrequency);
        if (frequencyHistory.Count > MOVING_AVERAGE_SIZE)
        {
            frequencyHistory.Dequeue();
        }
        float averageFrequency = frequencyHistory.Average();

        // Check if the amplitude is above the noise threshold
        if (maxAmplitude > NOISE_THRESHOLD)
        {
            if (showDebugLog)
            {
                Debug.Log($"Dominant frequency: {averageFrequency:F2} Hz (Amplitude: {maxAmplitude:F4})");
            }

            // You can add your own logic here to handle the detected frequency
            HandleDetectedFrequency(averageFrequency, maxAmplitude);
        }
    }

    private void HandleDetectedFrequency(float frequency, float amplitude)
    {
        // Add your own logic here to handle the detected frequency
        // For example, you could change the color of an object based on the frequency
        // or trigger some event when a specific frequency is detected
    }

    void OnDisable()
    {
        // Stop the microphone when the script is disabled
        Microphone.End(null);
    }
}
