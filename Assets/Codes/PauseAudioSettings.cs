using UnityEngine;
using Slider = UnityEngine.UI.Slider;
using Text = UnityEngine.UI.Text;
using Image = UnityEngine.UI.Image;
using Button = UnityEngine.UI.Button;

[DisallowMultipleComponent]
public sealed class PauseAudioSettings : MonoBehaviour
{
    private GameAudio audioSettings;
    private Slider sfxSlider;
    private Slider musicSlider;
    private Text sfxValue;
    private Text musicValue;

    public static void Attach(GameObject overlay)
    {
        if (overlay == null || overlay.GetComponent<PauseAudioSettings>() != null) return;
        PauseAudioSettings controls = overlay.AddComponent<PauseAudioSettings>();
        controls.Build();
    }

    private void Build()
    {
        audioSettings = GameAudio.GetOrCreate();
        RectTransform panel = transform.Find("PauseMenuPanel") as RectTransform;
        if (panel == null) return;
        Font font = panel.Find("PauseTitle").GetComponent<Text>().font;
        panel.sizeDelta = new Vector2(panel.sizeDelta.x, 650f);
        sfxSlider = BuildRow(panel, font, "SfxVolume", "효과음", 142f, out sfxValue);
        musicSlider = BuildRow(panel, font, "MusicVolume", "음악", 222f, out musicValue);
        sfxSlider.SetValueWithoutNotify(Mathf.Round(audioSettings.SfxVolume * 100f));
        musicSlider.SetValueWithoutNotify(Mathf.Round(audioSettings.MusicVolume * 100f));
        sfxSlider.onValueChanged.AddListener(value => { audioSettings.SetSfxVolume(value / 100f); RefreshLabels(); });
        musicSlider.onValueChanged.AddListener(value => { audioSettings.SetMusicVolume(value / 100f); RefreshLabels(); });
        RefreshLabels();
        Text hint = panel.Find("PauseHint").GetComponent<Text>();
        hint.text = "볼륨: 드래그 / −·+ / ←·→ · ESC: 계속 · 자동 저장";
        hint.rectTransform.sizeDelta = new Vector2(550f, 26f);
    }

    private void RefreshLabels()
    {
        sfxValue.text = sfxSlider.value == 0 ? "음소거" : $"{sfxSlider.value:0}%";
        musicValue.text = musicSlider.value == 0 ? "음소거" : $"{musicSlider.value:0}%";
    }

    private void OnDisable() { if (audioSettings != null) audioSettings.SaveSettings(); }
    private void OnEnable() { if (sfxSlider != null) sfxSlider.Select(); }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static Text Label(string name, Transform parent, Font font, string text, TextAnchor alignment,
        Vector2 position, Vector2 size)
    {
        Text label = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
        label.font = font;
        label.fontSize = 18;
        label.color = new Color32(235, 244, 255, 255);
        label.alignment = alignment;
        label.text = text;
        label.raycastTarget = false;
        return label;
    }

    private static Slider BuildRow(RectTransform panel, Font font, string name, string label,
        float top, out Text value)
    {
        RectTransform row = Rect(name + "Row", panel, new Vector2(0f, -top), new Vector2(480f, 68f));
        Text title = Label("Label", row, font, label, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(480f, 26f));
        value = Label("Value", row, font, "", TextAnchor.MiddleRight, Vector2.zero, new Vector2(480f, 26f));
        StretchWidth(title.rectTransform, 0, 0);
        StretchWidth(value.rectTransform, 0, 0);
        RectTransform sliderRect = Rect(name + "Slider", row, new Vector2(0, -30f), new Vector2(384f, 32f));
        StretchWidth(sliderRect, 48, 48);
        Image background = sliderRect.gameObject.AddComponent<Image>();
        background.color = new Color32(30, 47, 72, 255);
        Slider slider = sliderRect.gameObject.AddComponent<Slider>();
        slider.minValue = 0f; slider.maxValue = 100f; slider.wholeNumbers = true;
        RectTransform fillArea = Rect("FillArea", sliderRect, Vector2.zero, Vector2.zero);
        fillArea.anchorMin = Vector2.zero; fillArea.anchorMax = Vector2.one;
        fillArea.offsetMin = new Vector2(12, 12); fillArea.offsetMax = new Vector2(-12, -12);
        Image fill = Rect("Fill", fillArea, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        fill.color = new Color32(89, 209, 255, 255); fill.raycastTarget = false;
        fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
        RectTransform handleArea = Rect("HandleArea", sliderRect, Vector2.zero, Vector2.zero);
        handleArea.anchorMin = Vector2.zero; handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = new Vector2(12, 0); handleArea.offsetMax = new Vector2(-12, 0);
        Image handle = Rect("Handle", handleArea, Vector2.zero, new Vector2(18, 32)).gameObject.AddComponent<Image>();
        handle.color = new Color32(235, 244, 255, 255);
        slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        StepButton(row, font, "Decrease", "−", false, slider);
        StepButton(row, font, "Increase", "+", true, slider);
        return slider;
    }

    private static void StretchWidth(RectTransform rect, float left, float right)
    {
        rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, rect.offsetMin.y);
        rect.offsetMax = new Vector2(-right, rect.offsetMax.y);
    }

    private static void StepButton(Transform row, Font font, string name, string label, bool increase, Slider slider)
    {
        RectTransform rect = Rect(name, row, new Vector2(increase ? -18 : 18, -30), new Vector2(36, 32));
        rect.anchorMin = rect.anchorMax = new Vector2(increase ? 1 : 0, 1);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color32(49, 78, 116, 255);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => slider.value = Mathf.Clamp(slider.value + (increase ? 5 : -5), 0, 100));
        Label("Label", rect, font, label, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(36, 32));
    }
}
