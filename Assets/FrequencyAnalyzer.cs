using UnityEngine;
using System.Linq;

public class FrequencyAnalyzer : MonoBehaviour
{
    public int sampleSize = 1024;
    public float updateInterval = 0.1f;

    private AudioSource audioSource;
    private float[] samples;
    private float[] spectrum;
    private float timer;

    void Start()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = Microphone.Start(null, true, 1, AudioSettings.outputSampleRate);
        audioSource.loop = true;
        audioSource.Play();

        samples = new float[sampleSize];
        spectrum = new float[sampleSize];
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= updateInterval)
        {
            AnalyzeFrequency();
            timer = 0f;
        }
    }

    void AnalyzeFrequency()
    {
        audioSource.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);

        float totalFrequency = 0f;
        int maxIndex = 0;
        float maxValue = 0f;

        for (int i = 0; i < spectrum.Length; i++)
        {
            float frequency = i * AudioSettings.outputSampleRate / 2 / spectrum.Length;
            totalFrequency += frequency * spectrum[i];

            if (spectrum[i] > maxValue)
            {
                maxValue = spectrum[i];
                maxIndex = i;
            }
        }

        float dominantFrequency = maxIndex * AudioSettings.outputSampleRate / 2 / spectrum.Length;

        
        if (dominantFrequency > 0f && totalFrequency > 10)
        {
            Debug.Log($"Dominant frequency: {dominantFrequency} Hz");
            Debug.Log($"Total frequency: {totalFrequency} Hz");
        }
            
    }
}
