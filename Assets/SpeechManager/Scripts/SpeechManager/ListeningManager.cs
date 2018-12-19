using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public delegate void ListeningHandler(AudioClip clip);

public class ListeningManager : MonoBehaviour
{
    public AudioSource audioSource { get; set; }
    public float minimumLevel { get; set; }
    public float silenceTimeTreshold { get; set; }

    private float[] clipSampleData = new float[1024];
    private bool isListening = false;
    private float silenceTime = 0;

    public event ListeningHandler OnUtteranceEnded;

    public void StartListening()
    {
        Debug.Log("Recording... ");
        audioSource.clip = Microphone.Start(string.Empty, true, 60, 16000);
        Debug.Log("Recording... STARTED");

        while (!(Microphone.GetPosition(null) > 0)) { }
        audioSource.Play();
        silenceTime = 0;
        isListening = true;
    }

    void Update()
    {
        if (!isListening) return;

        silenceTime += Time.deltaTime;

        audioSource.GetSpectrumData(clipSampleData, 0, FFTWindow.Rectangular);
        float currentAverageVolume = clipSampleData.Average();

        if (currentAverageVolume > minimumLevel)
        {
            silenceTime = 0;
        }
        else
        {
            if (silenceTime >= silenceTimeTreshold)
            {
                Debug.Log("Recording... DONE");
                isListening = false;
                if (OnUtteranceEnded != null) OnUtteranceEnded(audioSource.clip);
            }
        }
    }
}
