using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using System.IO;
using System.Collections;

public class AudioComparer : MonoBehaviour
{
    [SerializeField] private Button recordButton1;
    [SerializeField] private Button recordButton2;
    [SerializeField] private Button compareButton;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private TMP_Text progressText;

    private AudioClip recordedAudio1;
    private AudioClip recordedAudio2;
    private List<Complex[]> frequencies1 = new List<Complex[]>();
    private List<Complex[]> frequencies2 = new List<Complex[]>();

    public int SAMPLE_SIZE = 1024;
    private const int SPECTRUM_COUNT = 10;
    private const float MIN_AMPLITUDE = 0.01f;
    private const int RECORDING_DURATION = 5; // 5 seconds recording
    private const int SAMPLE_RATE = 44100; // CD quality
    public float timeStepInSeconds = 0.01f; // Default to 1 millisecond, can be set to nanoseconds (e.g., 1e-9f for 1 nanosecond)

    private bool isPreparationComplete = false;
    private bool isPreparationInProgress = false;
    private bool isRecording = false;
    private string currentDevice;

    private const int FFT_SIZE = 1024;
    private const float FREQUENCY_THRESHOLD = 0.01f;
    private const float UPDATE_INTERVAL = 0.05f;

    private Coroutine frequencyAnalysisCoroutine;
    private float[] spectrum;

    private void Start()
    {
        InitializeUI();
    }

    private void InitializeUI()
    {
        if (recordButton1 == null || recordButton2 == null || compareButton == null || progressSlider == null || progressText == null)
        {
            Debug.LogError("UI elements are not assigned. Please assign them in the inspector.");
            return;
        }

        AddEventTrigger(recordButton1, EventTriggerType.PointerDown, (data) => { StartRecording(1); });
        AddEventTrigger(recordButton1, EventTriggerType.PointerUp, (data) => { StopRecording(1); });

        AddEventTrigger(recordButton2, EventTriggerType.PointerDown, (data) => { StartRecording(2); });
        AddEventTrigger(recordButton2, EventTriggerType.PointerUp, (data) => { StopRecording(2); });

        compareButton.onClick.AddListener(PerformComparison);
        compareButton.interactable = false;
    }

    private void AddEventTrigger(Button button, EventTriggerType eventType, Action<BaseEventData> action)
    {
        EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = button.gameObject.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = eventType };
        entry.callback.AddListener(new UnityEngine.Events.UnityAction<BaseEventData>(action));
        trigger.triggers.Add(entry);
    }

    public void StartRecording(int clipNumber)
    {
        if (isRecording) return;

        if (Microphone.devices.Length == 0)
        {
            UpdateProgress(0, "Error: No microphone found");
            return;
        }

        isRecording = true;
        UpdateProgress(0, $"Recording clip {clipNumber}...");

        currentDevice = Microphone.devices[0]; // Use the first available microphone
        if (clipNumber == 1)
            recordedAudio1 = Microphone.Start(currentDevice, true, RECORDING_DURATION, SAMPLE_RATE);
        else
            recordedAudio2 = Microphone.Start(currentDevice, true, RECORDING_DURATION, SAMPLE_RATE);

        UpdateProgress(1, $"Clip {clipNumber} recording started");

        // Initialize spectrum array
        spectrum = new float[FFT_SIZE];

        // Start frequency analysis coroutine
        frequencyAnalysisCoroutine = StartCoroutine(AnalyzeFrequenciesCoroutine());
    }

    public void StopRecording(int clipNumber)
    {
        
        if (!isRecording) return;

        isRecording = false;
        Microphone.End(currentDevice);

        // Stop frequency analysis coroutine
        if (frequencyAnalysisCoroutine != null)
        {
            StopCoroutine(frequencyAnalysisCoroutine);
            frequencyAnalysisCoroutine = null;
        }

        isRecording = false;
        Microphone.End(currentDevice);

        AudioClip recordedClip;
        if (clipNumber == 1)
        {
            recordedAudio1 = TrimSilence(recordedAudio1);
            recordedClip = recordedAudio1;
            UpdateProgress(1, "Clip 1 recorded successfully");
        }
        else
        {
            recordedAudio2 = TrimSilence(recordedAudio2);
            recordedClip = recordedAudio2;
            UpdateProgress(1, "Clip 2 recorded successfully");
        }

        if (recordedClip != null)
        {
            string fileName = $"RecordedAudio_{clipNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.wav";
            string filePath = Path.Combine(Application.dataPath, "Audio", fileName);
            SaveAudioClipAsWav(recordedClip, filePath);

            if (recordedAudio1 != null && recordedAudio2 != null)
            {
                compareButton.interactable = true;
            }
        }
        else
        {
            Debug.LogWarning($"Failed to record audio for clip {clipNumber}");
            UpdateProgress(1, $"Failed to record clip {clipNumber}");
        }
    }

    private IEnumerator AnalyzeFrequenciesCoroutine()
    {
        while (isRecording)
        {
            // Get the current audio spectrum
            AudioListener.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);

            // Analyze and print frequencies
            AnalyzeAndPrintFrequencies();

            yield return new WaitForSeconds(UPDATE_INTERVAL);
        }
    }

    private void AnalyzeAndPrintFrequencies()
    {
        string frequenciesOutput = "Detected Frequencies (Hz):";
        bool frequenciesDetected = false;

        for (int i = 0; i < FFT_SIZE / 2; i++)
        {
            float frequency = i * SAMPLE_RATE / FFT_SIZE;
            float amplitude = spectrum[i];

            if (amplitude > FREQUENCY_THRESHOLD)
            {
                frequenciesOutput += $"\n{frequency:F2}";
                frequenciesDetected = true;
            }
        }

        if (frequenciesDetected)
        {
            Debug.Log(frequenciesOutput);
        }
        else
        {
            Debug.Log("No significant frequencies detected.");
        }
    }

    private void SaveAudioClipAsWav(AudioClip clip, string filePath)
    {
        try
        {
            // Ensure the directory exists
            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            float[] samples = new float[clip.samples];
            clip.GetData(samples, 0);

            using (FileStream fs = File.Create(filePath))
            {
                byte[] header = CreateWavHeader(clip);
                fs.Write(header, 0, header.Length);

                byte[] data = ConvertAudioClipDataToInt16ByteArray(samples);
                fs.Write(data, 0, data.Length);
            }

            Debug.Log($"Audio saved successfully: {filePath}");
            UpdateProgress(1, $"Audio saved: {Path.GetFileName(filePath)}");

            // Refresh the AssetDatabase to make the new file visible in the Unity Editor
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif
        }
        catch (Exception e)
        {
            Debug.LogError($"Error saving audio file: {e.Message}");
            UpdateProgress(1, "Error saving audio file");
        }
    }

    private AudioClip TrimSilence(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogError("TrimSilence: Input clip is null");
            return null;
        }

        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        int startSample = 0;
        int endSample = samples.Length - 1;

        // Find the first non-silent sample
        for (int i = 0; i < samples.Length; i++)
        {
            if (Mathf.Abs(samples[i]) > 0.01f)
            {
                startSample = i;
                break;
            }
        }

        // Find the last non-silent sample
        for (int i = samples.Length - 1; i >= 0; i--)
        {
            if (Mathf.Abs(samples[i]) > 0.01f)
            {
                endSample = i;
                break;
            }
        }

        int trimmedLength = endSample - startSample + 1;
        if (trimmedLength <= 0)
        {
            Debug.LogWarning("TrimSilence: Recorded audio is completely silent");
            return null;
        }

        float[] trimmedSamples = new float[trimmedLength];
        Array.Copy(samples, startSample, trimmedSamples, 0, trimmedLength);

        AudioClip trimmedClip = AudioClip.Create(clip.name, trimmedLength / clip.channels, clip.channels, clip.frequency, false);
        trimmedClip.SetData(trimmedSamples, 0);

        return trimmedClip;
    }


    private byte[] CreateWavHeader(AudioClip clip)
    {
        int headerSize = 44;
        int fileSize = 36 + clip.samples * 2;
        int sampleRate = clip.frequency;
        short channels = (short)clip.channels;
        short bitsPerSample = 16;

        byte[] header = new byte[headerSize];

        // RIFF chunk descriptor
        System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(header, 0);
        BitConverter.GetBytes(fileSize).CopyTo(header, 4);
        System.Text.Encoding.ASCII.GetBytes("WAVE").CopyTo(header, 8);

        // "fmt " sub-chunk
        System.Text.Encoding.ASCII.GetBytes("fmt ").CopyTo(header, 12);
        BitConverter.GetBytes(16).CopyTo(header, 16);
        BitConverter.GetBytes((short)1).CopyTo(header, 20);
        BitConverter.GetBytes(channels).CopyTo(header, 22);
        BitConverter.GetBytes(sampleRate).CopyTo(header, 24);
        BitConverter.GetBytes(sampleRate * channels * 2).CopyTo(header, 28);
        BitConverter.GetBytes((short)(channels * 2)).CopyTo(header, 32);
        BitConverter.GetBytes(bitsPerSample).CopyTo(header, 34);

        // "data" sub-chunk
        System.Text.Encoding.ASCII.GetBytes("data").CopyTo(header, 36);
        BitConverter.GetBytes(clip.samples * 2).CopyTo(header, 40);

        return header;
    }

    private byte[] ConvertAudioClipDataToInt16ByteArray(float[] data)
    {
        byte[] byteArray = new byte[data.Length * 2];
        for (int i = 0; i < data.Length; i++)
        {
            short intData = (short)(data[i] * 32767);
            BitConverter.GetBytes(intData).CopyTo(byteArray, i * 2);
        }
        return byteArray;
    }

    private async void PerformComparison()
    {
        compareButton.interactable = false;
        await PrepareAudioDataAsync();
        float similarity = await CompareAudioAsync();
        if (similarity >= 0)
        {
            Debug.Log($"Audio similarity: {similarity:F2}%");
            UpdateProgress(1, $"Similarity: {similarity:F2}%");
        }
        else
        {
            Debug.LogWarning("Comparison failed or is not ready yet.");
            UpdateProgress(1, "Comparison failed");
        }
        compareButton.interactable = true;
    }

    private async Task PrepareAudioDataAsync()
    {
        if (isPreparationInProgress) return;

        isPreparationInProgress = true;
        isPreparationComplete = false;
        UpdateProgress(0, "Starting preparation...");

        if (recordedAudio1 == null || recordedAudio2 == null)
        {
            Debug.LogError("Recorded audio clips are missing. Please record both clips.");
            isPreparationInProgress = false;
            UpdateProgress(0, "Error: Missing audio clips");
            return;
        }

        try
        {
            frequencies1.Clear();
            frequencies2.Clear();
            await PrepareAudioAsync(recordedAudio1, frequencies1, "Preparing audio 1");
            await PrepareAudioAsync(recordedAudio2, frequencies2, "Preparing audio 2");
            isPreparationComplete = true;
            UpdateProgress(1, "Audio preparation complete. Ready for comparison.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error during audio preparation: {e.Message}");
            UpdateProgress(0, "Error during preparation");
        }
        finally
        {
            isPreparationInProgress = false;
        }
    }

    private async Task PrepareAudioAsync(AudioClip clip, List<Complex[]> frequencies, string progressMessage)
    {
        int channels = clip.channels;
        int frequency = clip.frequency;
        float[] samples = new float[clip.samples * channels];
        clip.GetData(samples, 0);

        float[] monoSamples = await Task.Run(() => ConvertToMono(samples, channels));
        float clipDuration = clip.length;
        int totalSteps = Mathf.FloorToInt(clipDuration / timeStepInSeconds);

        for (int step = 0; step < totalSteps; step++)
        {
            await Task.Run(() =>
            {
                Complex[] frequencyData = new Complex[SAMPLE_SIZE];
                float currentTime = step * timeStepInSeconds;
                int startSample = Mathf.FloorToInt(currentTime * frequency);
                List<string> debugMessages = new List<string>();

                CalculateFrequencies(monoSamples, startSample, frequencyData, frequency, debugMessages);

                lock (frequencies)
                {
                    frequencies.Add(frequencyData);
                }

                // Print frequencies for this time step
                /*Debug.Log($"Frequencies at {currentTime:F9} seconds:");
                foreach (var message in debugMessages)
                {
                    Debug.Log(message);
                }*/
            });

            float progress = (float)(step + 1) / totalSteps;
            UpdateProgress(progress, $"{progressMessage} ({(progress * 100):F2}%)");
        }
    }



    private float[] ConvertToMono(float[] samples, int channels)
    {
        if (channels == 1) return samples;

        float[] mono = new float[samples.Length / channels];
        for (int i = 0; i < mono.Length; i++)
        {
            float sum = 0f;
            for (int c = 0; c < channels; c++)
            {
                sum += samples[i * channels + c];
            }
            mono[i] = sum / channels;
        }
        return mono;
    }

    private void CalculateFrequencies(float[] samples, int startSample, Complex[] frequencies, int frequency, List<string> debugMessages)
    {
        float frequencyResolution = (float)frequency / SAMPLE_SIZE;
        int samplesToProcess = Mathf.Min(SAMPLE_SIZE, samples.Length - startSample);

        for (int i = 0; i < SAMPLE_SIZE; i++)
        {
            float real = 0f;
            float imag = 0f;
            for (int j = 0; j < samplesToProcess; j++)
            {
                int sampleIndex = (startSample + j) % samples.Length;
                float angle = 2 * Mathf.PI * i * j / SAMPLE_SIZE;
                real += samples[sampleIndex] * Mathf.Cos(angle);
                imag -= samples[sampleIndex] * Mathf.Sin(angle);
            }
            frequencies[i] = new Complex(real / samplesToProcess, imag / samplesToProcess);

            float magnitude = frequencies[i].Magnitude;
            float freqHz = i * frequencyResolution;

            // Only add to debug messages if magnitude is above a threshold
            /*if (magnitude > MIN_AMPLITUDE)
            {
                debugMessages.Add($"{freqHz:F2} Hz: Magnitude = {magnitude:F4}");
            }*/
        }
    }



    public async Task<float> CompareAudioAsync()
    {
        if (!isPreparationComplete)
        {
            if (!isPreparationInProgress)
            {
                await PrepareAudioDataAsync();
            }
            else
            {
                return -1f;
            }
        }

        if (!isPreparationComplete)
        {
            return -1f;
        }

        UpdateProgress(0.5f, "Starting comparison...");

        if (frequencies1.Count != frequencies2.Count)
        {
            UpdateProgress(0.5f, "Error: Frequency data counts do not match");
            return -1f;
        }

        float totalSimilarity = 0f;
        int validComparisons = 0;

        for (int i = 0; i < SPECTRUM_COUNT; i++)
        {
            float similarity = CompareSingleSpectrum(frequencies1[i], frequencies2[i]);
            if (similarity >= 0)
            {
                totalSimilarity += similarity;
                validComparisons++;
            }
            UpdateProgress(0.5f + (float)(i + 1) / SPECTRUM_COUNT * 0.5f, $"Comparing spectrums ({(i + 1) * 100 / SPECTRUM_COUNT:F0}%)");
        }

        if (validComparisons == 0)
        {
            Debug.LogWarning("No valid comparisons were made. The audio might be too quiet or empty.");
            UpdateProgress(1f, "Warning: No valid comparisons");
            return 0f;
        }

        float result = totalSimilarity / validComparisons;
        UpdateProgress(1f, $"Comparison complete. Similarity: {result:F2}%");
        return result;
    }


    private float CompareSingleSpectrum(Complex[] spectrum1, Complex[] spectrum2)
    {
        float totalDifference = 0f;
        int significantBins = 0;

        for (int i = 0; i < SAMPLE_SIZE; i++)
        {
            float magnitude1 = spectrum1[i].Magnitude;
            float magnitude2 = spectrum2[i].Magnitude;
            float difference = Mathf.Abs(magnitude1 - magnitude2);

            if (magnitude1 > MIN_AMPLITUDE || magnitude2 > MIN_AMPLITUDE)
            {
                totalDifference += difference;
                significantBins++;
            }
        }

        if (significantBins == 0) return -1f;

        float averageDifference = totalDifference / significantBins;
        float similarityPercentage = (1f - averageDifference) * 100f;

        return Mathf.Clamp(similarityPercentage, 0f, 100f);
    }

    
    public async Task PerformComparisonAsync()
    {
        float similarity = await CompareAudioAsync();
        if (similarity >= 0)
        {
            Debug.Log($"Audio similarity: {similarity:F2}%");
        }
        else
        {
            Debug.LogWarning("Comparison failed or is not ready yet.");
        }
    }

    private void UpdateProgress(float progress, string message)
    {
        if (progressSlider != null)
        {
            progressSlider.value = progress;
        }
        if (progressText != null)
        {
            progressText.text = message;
        }
    }

    public struct Complex
    {
        public float Real;
        public float Imaginary;

        public Complex(float real, float imaginary)
        {
            Real = real;
            Imaginary = imaginary;
        }

        
        public float Magnitude => Mathf.Sqrt(Real * Real + Imaginary * Imaginary);
        public override string ToString()
        {
            return $"({Real}, {Imaginary})";
        }

    }
}
