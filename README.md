# UnitySpeechManager
Unity3d component for Microsoft Speech APIs.

This project is aimed to make your life easy if you want to use Microsoft Speech APIs in Unity3d.

To run the example:

1) Create a Speech resource in your Azure subscription in order to generate a valid key
![Azure](azure.png)

2) Open the SpeechScene

3) Copy the KEY 1 (or KEY 2) value from Azure portal, and paste into the "Subscription Key" field of SpeechManager prefab
![File](subscription_key.png)

4) Run and enjoy!

# How to use in your own project
If you want to use the component in your own project, download the **.unitypackage** file from [Releases](https://github.com/mamarche/UnitySpeechManager/releases), import it into your project, and drag the **SpeechManager** prefab into your scene.

At this point, if you want to synthetize speech to text, you have just to subscribe the **OnTextReceived** event and then call the **Recognize** method:

```csharp
private void Start()
{
    SpeechManager.Instance.OnTextReceived += OnTextReceived;
    SpeechManager.Instance.Recognize();
}
        
private void OnTextReceived(object result )
{
    RecognitionResult res = (RecognitionResult)result;
    Debug.Log(res.RecognitionStatus + ": " + res.DisplayText;);
}
```

if you need **Text to Speech**, you have just to call the **Speak** method passing your text:
```csharp
SpeechManager.Instance.Speak("your text here!");`
```
