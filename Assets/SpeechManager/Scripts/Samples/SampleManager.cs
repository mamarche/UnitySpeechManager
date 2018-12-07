using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SampleManager : MonoBehaviour {

    [SerializeField]
    private InputField inputField;
    [SerializeField]
    private Text statusText;

    private void Start()
    {
        SpeechManager.Instance.OnTextReceived += OnTextReceived;
    }
    public void SpeakCommand()
    {
        SpeechManager.Instance.Speak(inputField.text);
    }

    public void RecognizeCommand()
    {
        statusText.text = "Recording...";
        SpeechManager.Instance.Recognize();
    }

    private void OnTextReceived(object result )
    {
        RecognitionResult res = (RecognitionResult)result;
        statusText.text = res.RecognitionStatus + ": " + res.DisplayText;
    }
}
