using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public delegate void SpeechGenericHandler(object response);
public delegate void SpeechAudioHandler(AudioClip clip);

public class SpeechManager : SpeechSingleton<SpeechManager>
{
    [Header("Cognitive Services")]
    [SerializeField]
    private string subscriptionKey;
    [SerializeField]
    private string tokenUrl = "https://westeurope.api.cognitive.microsoft.com/sts/v1.0/issuetoken";
    [SerializeField]
    private string sttUrl = "https://westeurope.stt.speech.microsoft.com/speech/recognition/conversation/cognitiveservices/v1";
    [SerializeField]
    private string ttsUrl = "https://westeurope.tts.speech.microsoft.com/cognitiveservices/v1";

    [Header("Text to Speech")]
    [SerializeField]
    private string ttsLanguage = "it-IT";
    [SerializeField]
    private string audioFormat = "riff-24khz-16bit-mono-pcm";
    [SerializeField]
    private string voiceName = "Microsoft Server Speech Text to Speech Voice (it-IT, LuciaRUS)";

    [Header("Speech to Text")]
    [SerializeField]
    private int recordingDuration = 3;
    [SerializeField]
    private string sttLanguage = "it-IT";
    private int _RATE = 16000;
    private AudioClip _recordingClip;

    private string _lastError;

    /// After obtaining a valid token, this class will cache it for this duration.
    /// Use a duration of 5 minutes, which is less than the actual token lifetime of 10 minutes.
    private readonly TimeSpan TokenCacheDuration = new TimeSpan(0, 5, 0);
    /// Cache the values of the last valid tokens obtained from the token service.
    private string _storedTokenValue = string.Empty;
    /// When the last valid token was obtained.
    private DateTime _storedTokenTime = DateTime.MinValue;

    #region Properties
    public string lastError { get { return _lastError; } private set { _lastError = value; } }
    #endregion

    #region Events
    public event SpeechAudioHandler OnAudioReceived;
    public event SpeechGenericHandler OnTextReceived;
    #endregion

    private void Start()
    {

#if UNITY_EDITOR
        System.Net.ServicePointManager.ServerCertificateValidationCallback += (a, b, c, d) => { return true; };
#endif
        GetAccessToken();
    }

    #region Public Methods
    public void Speak(string text)
    {
        StartCoroutine(SpeakCoroutine(text));
    }
    public void Recognize()
    {
        StartCoroutine(RecordingCoroutine(recordingDuration));
    }
    #endregion

    #region Authorization
    private void GetAccessToken()
    {
        StartCoroutine(GetAccessTokenCoroutine());
    }
    private IEnumerator GetAccessTokenCoroutine()
    {
        // Re-use the cached token if there is one.
        if ((DateTime.Now - _storedTokenTime) > TokenCacheDuration)
        {
            var client = UnityWebRequest.Post(tokenUrl, string.Empty);

            client.SetRequestHeader("Ocp-Apim-Subscription-Key", subscriptionKey);

            yield return client.SendWebRequest();

            if (client.isNetworkError || client.isHttpError)
            {
                Debug.Log(client.error);
            }
            else
            {
                string resultContent = client.downloadHandler.text;
                Debug.Log(resultContent);
                _storedTokenTime = DateTime.Now;
                _storedTokenValue = resultContent;
            }

        }
    }
    #endregion

    #region Speech To Text
    private IEnumerator RecordingCoroutine(int seconds)
    {
        Debug.Log("Recording... ");
        _recordingClip = Microphone.Start(null, false, seconds, _RATE);

        Debug.Log("Recording... STARTED");
        yield return new WaitForSeconds(seconds);
        Debug.Log("Recording... DONE");

        Debug.Log("Recognizing...");
        yield return StartCoroutine(RecognizeCoroutine(_recordingClip));

    }
    private IEnumerator RecognizeCoroutine(AudioClip clip)
    {
        var wavData = new WavData(clip);

        var client = UnityWebRequest.Put(sttUrl + "?language=" + sttLanguage, wavData.FullRawBytes);

        client.SetRequestHeader("Ocp-Apim-Subscription-Key", subscriptionKey);
        client.SetRequestHeader("Authorization", _storedTokenValue);
        client.SetRequestHeader("Content-type", "audio/wav; codec=audio/pcm; samplerate=16000");

        client.method = "POST";

        yield return client.SendWebRequest();

        if (client.isNetworkError || client.isHttpError)
        {
            Debug.Log(client.error);
        }
        else
        {
            string resultContent = client.downloadHandler.text;
            Debug.Log(resultContent);
            //OnTextReceived(resultContent);
            Debug.Log("Recognizing...DONE");

            Debug.Log(resultContent.ToString());

            var result = JsonUtility.FromJson<RecognitionResult>(resultContent.ToString());

            OnTextReceived(result);
        }
    }
    #endregion

    #region Text To Speech
    private IEnumerator SpeakCoroutine(string text)
    {
        yield return GetAccessTokenCoroutine();

        string ssml = "<speak version='1.0' xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang='" + ttsLanguage + "'>" +
                        "<voice name='" + voiceName + "' >" +
                        text + 
                        "</voice> </speak> ";

        byte[] data = System.Text.Encoding.UTF8.GetBytes(ssml);

        var client = UnityWebRequest.Put(ttsUrl, data);
        client.SetRequestHeader("Authorization", "Bearer " + _storedTokenValue);
        client.SetRequestHeader("Content-Type", "application/ssml+xml");
        client.SetRequestHeader("X-Microsoft-OutputFormat", audioFormat);
        client.SetRequestHeader("User-Agent", "HLSpeech");

        client.method = "POST";

        yield return client.SendWebRequest();

        if (client.isNetworkError || client.isHttpError)
        {
            Debug.Log(client.error);
        }
        else
        {
            var resultContent = client.downloadHandler.data;
            Debug.Log(resultContent);
            AudioClip clip = ParseWAV("audio", resultContent);
            PlayClip(clip);
        }

    }
    private void PlayClip(AudioClip clip)
    {
        if (Application.isPlaying && clip != null)
        {
            GameObject audioObject = new GameObject("AudioObject");
            AudioSource source = audioObject.AddComponent<AudioSource>();
            source.spatialBlend = 0.0f;
            source.loop = false;
            source.clip = clip;
            source.Play();

            Destroy(audioObject, clip.length);
        }
    }
    #endregion

    #region Private Types
    struct IFF_FORM_CHUNK
    {
        public uint form_id;
        public uint form_length;
        public uint id;
    };
    struct IFF_CHUNK
    {
        public uint id;
        public uint length;
    };
    struct WAV_PCM
    {
        public ushort format_tag;
        public ushort channels;
        public uint sample_rate;
        public uint average_data_rate;
        public ushort alignment;
        public ushort bits_per_sample;
    };
    #endregion

    #region Audio Helpers
    private static T ReadType<T>(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(Marshal.SizeOf(typeof(T)));

        GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        T theStructure = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
        handle.Free();

        return theStructure;
    }
    private static void WriteType<T>(BinaryWriter writer, T data)
    {
        int size = Marshal.SizeOf(data);
        byte[] bytes = new byte[size];

        IntPtr ptr = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(data, ptr, true);
        Marshal.Copy(ptr, bytes, 0, size);
        Marshal.FreeHGlobal(ptr);

        writer.Write(bytes, 0, size);
    }
    private static uint MakeID(string id)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(id);
        return BitConverter.ToUInt32(bytes, 0);
    }
    private static string GetID(uint id)
    {
        byte[] bytes = BitConverter.GetBytes(id);
        return new string(new char[] { (char)bytes[0], (char)bytes[1], (char)bytes[2], (char)bytes[3] });
    }
    private AudioClip ParseWAV(string clipName, byte[] data)
    {
        MemoryStream stream = new MemoryStream(data, false);
        BinaryReader reader = new BinaryReader(stream);

        IFF_FORM_CHUNK form = ReadType<IFF_FORM_CHUNK>(reader);
        if (GetID(form.form_id) != "RIFF" || GetID(form.id) != "WAVE")
        {
            _lastError = String.Format("Malformed WAV header: {0} != RIFF || {1} != WAVE", GetID(form.form_id), GetID(form.id));
            return null;
        }

        WAV_PCM header = new WAV_PCM();
        bool bHeaderFound = false;

        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            IFF_CHUNK chunk = ReadType<IFF_CHUNK>(reader);

            int ChunkLength = (int)chunk.length;
            if (ChunkLength < 0)  // HACK: Deal with TextToSpeech bug where the chunk length is not set for the data chunk..
                ChunkLength = (int)(reader.BaseStream.Length - reader.BaseStream.Position);
            if ((ChunkLength & 0x1) != 0)
                ChunkLength += 1;

            long ChunkEnd = reader.BaseStream.Position + ChunkLength;
            if (GetID(chunk.id) == "fmt ")
            {
                bHeaderFound = true;
                header = ReadType<WAV_PCM>(reader);
            }
            else if (GetID(chunk.id) == "data")
            {
                if (!bHeaderFound)
                {
                    _lastError = "Failed to find header.";
                    return null;
                }
                byte[] waveform = reader.ReadBytes(ChunkLength);

                // convert into a float based wave form..
                int channels = (int)header.channels;
                int bps = (int)header.bits_per_sample;
                float divisor = 1 << (bps - 1);
                int bytesps = bps / 8;
                int samples = waveform.Length / bytesps;

                _lastError = string.Format("WAV INFO, channels = {0}, bps = {1}, samples = {2}, rate = {3}",
                    channels, bps, samples, header.sample_rate);

                float[] wf = new float[samples];
                if (bps == 16)
                {
                    for (int s = 0; s < samples; ++s)
                        wf[s] = ((float)BitConverter.ToInt16(waveform, s * bytesps)) / divisor;
                }
                else if (bps == 32)
                {
                    for (int s = 0; s < samples; ++s)
                        wf[s] = ((float)BitConverter.ToInt32(waveform, s * bytesps)) / divisor;
                }
                else if (bps == 8)
                {
                    for (int s = 0; s < samples; ++s)
                        wf[s] = ((float)BitConverter.ToChar(waveform, s * bytesps)) / divisor;
                }
                else
                {
                    _lastError = string.Format("Unspported BPS {0} in WAV data.", bps.ToString());
                    return null;
                }

                AudioClip clip = AudioClip.Create(clipName, samples, channels, (int)header.sample_rate, false);
                clip.SetData(wf, 0);

                return clip;
            }

            reader.BaseStream.Position = ChunkEnd;
        }

        return null;
    }
    #endregion
}

