using UnityEngine;

public class SpeechPitchDetector : MonoBehaviour
{
    private AudioSource audioSource;
    private float[] samples = new float[8192];
    private float[] spectrum = new float[8192];

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.clip = Microphone.Start(null, true, 1, AudioSettings.outputSampleRate);
        audioSource.loop = true;
        while (!(Microphone.GetPosition(null) > 0)) { }
        audioSource.Play();
    }

    void Update()
    {
        AnalyzeAudio();
    }

    void AnalyzeAudio()
    {
        audioSource.GetOutputData(samples, 0); // Get samples
        FFTWindow window = FFTWindow.BlackmanHarris;
        audioSource.GetSpectrumData(spectrum, 0, window); // Perform FFT

        float maxV = 0;
        int maxN = 0;
        for (int i = 0; i < spectrum.Length; i++)
        {
            if ((spectrum[i] > maxV) && (spectrum[i] > 0.01f))
            {
                maxV = spectrum[i];
                maxN = i; // Find the peak
            }
        }

        float freqN = maxN;
        if (maxN > 0 && maxN < spectrum.Length - 1)
        {
            var dL = spectrum[maxN - 1] / spectrum[maxN];
            var dR = spectrum[maxN + 1] / spectrum[maxN];
            freqN += 0.5f * (dR * dR - dL * dL);
        }

        float frequency = freqN * (AudioSettings.outputSampleRate / 2) / spectrum.Length;
        if(frequency > 0)
            Debug.Log("Detected Frequency: " + frequency);
    }
}
