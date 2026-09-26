using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class AudioVolumeControlUI : MonoBehaviour
{
    [SerializeField] private AudioVolumeChannel channel = AudioVolumeChannel.Master;
    [SerializeField] private Scrollbar volumeScrollbar;
    [SerializeField] private Text titleText;
    [SerializeField] private Text valueText;
    [SerializeField] private string title = "Volume";

    private AudioManager subscribedManager;
    private bool suppressScrollbarCallback;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        WireScrollbar();
        SubscribeToAudioManager();
        SyncFromAudioManager();
    }

    private void OnDisable()
    {
        if (volumeScrollbar != null)
            volumeScrollbar.onValueChanged.RemoveListener(OnScrollbarValueChanged);

        if (subscribedManager != null)
            subscribedManager.VolumeChanged -= OnAudioVolumeChanged;

        subscribedManager = null;
    }

    private void OnValidate()
    {
        ResolveReferences();
        UpdateLabels(volumeScrollbar != null ? volumeScrollbar.value : 1f);
    }

    public void SetChannel(AudioVolumeChannel newChannel)
    {
        channel = newChannel;
        SyncFromAudioManager();
    }

    private void ResolveReferences()
    {
        if (volumeScrollbar == null)
            volumeScrollbar = GetComponentInChildren<Scrollbar>(true);

        Text[] texts = GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null)
                continue;

            if (titleText == null && texts[i].name == "TitleText")
                titleText = texts[i];
            else if (valueText == null && texts[i].name == "ValueText")
                valueText = texts[i];
        }
    }

    private void WireScrollbar()
    {
        if (volumeScrollbar == null)
            return;

        volumeScrollbar.onValueChanged.RemoveListener(OnScrollbarValueChanged);
        volumeScrollbar.onValueChanged.AddListener(OnScrollbarValueChanged);
    }

    private void SubscribeToAudioManager()
    {
        if (subscribedManager != null)
            subscribedManager.VolumeChanged -= OnAudioVolumeChanged;

        subscribedManager = AudioManager.Instance;
        subscribedManager.VolumeChanged += OnAudioVolumeChanged;
    }

    private void SyncFromAudioManager()
    {
        AudioManager manager = AudioManager.Instance;
        float volume = manager.GetVolume(channel);

        if (volumeScrollbar != null)
        {
            suppressScrollbarCallback = true;
            volumeScrollbar.value = volume;
            suppressScrollbarCallback = false;
        }

        UpdateLabels(volume);
    }

    private void OnScrollbarValueChanged(float value)
    {
        if (suppressScrollbarCallback)
            return;

        AudioManager.Instance.SetVolume(channel, value);
        UpdateLabels(value);
    }

    private void OnAudioVolumeChanged(AudioVolumeChannel changedChannel, float value)
    {
        if (changedChannel != channel)
            return;

        if (volumeScrollbar != null)
        {
            suppressScrollbarCallback = true;
            volumeScrollbar.value = value;
            suppressScrollbarCallback = false;
        }

        UpdateLabels(value);
    }

    private void UpdateLabels(float volume)
    {
        if (titleText != null)
            titleText.text = title;

        if (valueText != null)
            valueText.text = Mathf.RoundToInt(Mathf.Clamp01(volume) * 100f) + "%";
    }
}
