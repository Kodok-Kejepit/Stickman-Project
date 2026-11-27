using System;
using Stickman.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Stickman.UI
{
    /// <summary>Modal settings panel for FPS, camera pitch/zoom, aim sensitivity, deadzone, toggles, and layout editor.</summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        Font _font;
        Action _onChanged;
        Action _onOpenLayoutEditor;
        Action _onClosed;

        Text _titleText;
        Text _fpsBtnText;
        Text _langBtnText;
        Slider _pitchSlider;
        Text _pitchValText;
        Slider _zoomSlider;
        Text _zoomValText;
        Slider _panSensSlider;
        Text _panSensValText;
        Slider _sensSlider;
        Text _sensValText;
        Slider _dzSlider;
        Text _dzValText;

        Toggle _aimGuideToggle;
        Toggle _autoSpinToggle;
        Toggle _shakeToggle;
        Toggle _fpsToggle;

        bool _suppressEvents;

        public void Initialize(Font font, Action onChanged, Action onOpenLayoutEditor, Action onClosed)
        {
            _font = font;
            _onChanged = onChanged;
            _onOpenLayoutEditor = onOpenLayoutEditor;
            _onClosed = onClosed;
            BuildUi();
            gameObject.SetActive(false);
        }

        public void Open()
        {
            gameObject.SetActive(true);
            SyncFromSettings();
        }

        public void Close()
        {
            gameObject.SetActive(false);
            _onClosed?.Invoke();
        }

        void BuildUi()
        {
            var rt = GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var dim = gameObject.AddComponent<Image>();
            dim.color = new Color(0.03f, 0.05f, 0.08f, 0.72f);

            var cardGo = new GameObject("SettingsCard");
            cardGo.transform.SetParent(transform, false);
            var cardRt = cardGo.AddComponent<RectTransform>();
            cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(780f, 690f);
            var cardImg = cardGo.AddComponent<Image>();
            cardImg.color = new Color(0.09f, 0.12f, 0.16f, 0.96f);

            var vlg = cardGo.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(28, 28, 20, 20);
            vlg.spacing = 8f;
            vlg.childControlHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;

            _titleText = AddLabel(cardRt, "SETTINGS", 26, TextAnchor.MiddleCenter, Color.white, 36f);

            _fpsBtnText = AddCycleRow(cardRt, "Target FPS", CycleFps);
            AddSliderRow(cardRt, "Camera Pitch", 45f, 65f, out _pitchSlider, out _pitchValText, val =>
            {
                SettingsStore.Current.cameraPitch = val;
                SaveAndNotify();
            });
            AddSliderRow(cardRt, "Camera Distance", 20f, 36f, out _zoomSlider, out _zoomValText, val =>
            {
                SettingsStore.Current.cameraDistance = val;
                SaveAndNotify();
            });
            AddSliderRow(cardRt, "Camera Pan Sensitivity", 0.4f, 2.5f, out _panSensSlider, out _panSensValText, val =>
            {
                SettingsStore.Current.cameraPanSensitivity = val;
                SaveAndNotify();
            });
            AddSliderRow(cardRt, "Aim Sensitivity", 0.5f, 2.0f, out _sensSlider, out _sensValText, val =>
            {
                SettingsStore.Current.aimSensitivity = val;
                SaveAndNotify();
            });
            AddSliderRow(cardRt, "Joystick Deadzone", 0.05f, 0.30f, out _dzSlider, out _dzValText, val =>
            {
                SettingsStore.Current.joystickDeadZone = val;
                SaveAndNotify();
            });

            _aimGuideToggle = AddToggleRow(cardRt, "Show Aim Guide", v => { SettingsStore.Current.showAimGuide = v; SaveAndNotify(); });
            _autoSpinToggle = AddToggleRow(cardRt, "Auto Spin (Gesek)", v => { SettingsStore.Current.autoSpinOnFlyThrough = v; SaveAndNotify(); });
            _shakeToggle = AddToggleRow(cardRt, "Landing Shake", v => { SettingsStore.Current.screenShake = v; SaveAndNotify(); });
            _fpsToggle = AddToggleRow(cardRt, "Show FPS", v => { SettingsStore.Current.showFps = v; SaveAndNotify(); });

            _langBtnText = AddCycleRow(cardRt, "Language", ToggleLanguage);

            // Bottom action buttons
            var btnRow = new GameObject("ActionButtons");
            btnRow.transform.SetParent(cardRt, false);
            var rowRt = btnRow.AddComponent<RectTransform>();
            rowRt.sizeDelta = new Vector2(0f, 52f);
            var hlg = btnRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 14f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;

            AddButton(rowRt, "Edit Layout", new Color(0.16f, 0.48f, 0.72f, 1f), () => { Close(); _onOpenLayoutEditor?.Invoke(); });
            AddButton(rowRt, "Reset", new Color(0.55f, 0.25f, 0.22f, 1f), () => { SettingsStore.ResetToDefaults(); SyncFromSettings(); _onChanged?.Invoke(); });
            AddButton(rowRt, "Close", new Color(0.24f, 0.58f, 0.34f, 1f), Close);
        }

        void SyncFromSettings()
        {
            _suppressEvents = true;
            var s = SettingsStore.Current;
            _titleText.text = Loc.Get("settings.title", s.language);
            _fpsBtnText.text = s.targetFps == 0 ? "AUTO" : s.targetFps.ToString();
            _langBtnText.text = s.language == "en" ? "English" : "Indonesia";
            _pitchSlider.value = s.cameraPitch;
            _pitchValText.text = s.cameraPitch.ToString("0") + "°";
            _zoomSlider.value = s.cameraDistance;
            _zoomValText.text = s.cameraDistance.ToString("0.0") + "u";
            _panSensSlider.value = s.cameraPanSensitivity;
            _panSensValText.text = s.cameraPanSensitivity.ToString("0.00") + "x";
            _sensSlider.value = s.aimSensitivity;
            _sensValText.text = s.aimSensitivity.ToString("0.00") + "x";
            _dzSlider.value = s.joystickDeadZone;
            _dzValText.text = s.joystickDeadZone.ToString("0.00");
            _aimGuideToggle.isOn = s.showAimGuide;
            _autoSpinToggle.isOn = s.autoSpinOnFlyThrough;
            _shakeToggle.isOn = s.screenShake;
            _fpsToggle.isOn = s.showFps;
            _suppressEvents = false;
        }

        void CycleFps()
        {
            var s = SettingsStore.Current;
            s.targetFps = s.targetFps == 0 ? 60 : (s.targetFps == 60 ? 90 : (s.targetFps == 90 ? 120 : 0));
            SaveAndNotify();
        }

        void ToggleLanguage()
        {
            var s = SettingsStore.Current;
            s.language = s.language == "id" ? "en" : "id";
            SaveAndNotify();
        }

        void SaveAndNotify()
        {
            if (_suppressEvents) return;
            SettingsStore.Save(SettingsStore.Current);
            SyncFromSettings();
            _onChanged?.Invoke();
        }

        Text AddLabel(RectTransform parent, string text, int size, TextAnchor align, Color color, float height)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, height);
            var t = go.AddComponent<Text>();
            t.font = _font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            return t;
        }

        Text AddCycleRow(RectTransform parent, string label, Action onClick)
        {
            var row = CreateRow(parent, 40f);
            AddRowLabel(row, label, 0.55f);

            var btnGo = new GameObject("CycleBtn");
            btnGo.transform.SetParent(row, false);
            var rt = btnGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.58f, 0.1f);
            rt.anchorMax = new Vector2(1f, 0.9f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = btnGo.AddComponent<Image>();
            img.color = new Color(0.18f, 0.25f, 0.34f, 1f);
            var btn = btnGo.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());

            var txtGo = new GameObject("Val");
            txtGo.transform.SetParent(rt, false);
            var trt = txtGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            var t = txtGo.AddComponent<Text>();
            t.font = _font;
            t.fontSize = 18;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            return t;
        }

        void AddSliderRow(RectTransform parent, string label, float min, float max, out Slider slider, out Text valText, Action<float> onVal)
        {
            var row = CreateRow(parent, 34f);
            AddRowLabel(row, label, 0.42f);

            var sGo = new GameObject("Slider");
            sGo.transform.SetParent(row, false);
            var srt = sGo.AddComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.44f, 0.12f);
            srt.anchorMax = new Vector2(0.84f, 0.88f);
            srt.offsetMin = srt.offsetMax = Vector2.zero;

            var bg = new GameObject("Bg").AddComponent<Image>();
            bg.transform.SetParent(srt, false);
            bg.color = new Color(0.16f, 0.20f, 0.26f, 1f);
            var bgRt = bg.rectTransform;
            bgRt.anchorMin = new Vector2(0f, 0.35f); bgRt.anchorMax = new Vector2(1f, 0.65f);
            bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;

            var fillArea = new GameObject("FillArea").AddComponent<RectTransform>();
            fillArea.SetParent(srt, false);
            fillArea.anchorMin = new Vector2(0f, 0.35f); fillArea.anchorMax = new Vector2(1f, 0.65f);
            fillArea.offsetMin = fillArea.offsetMax = Vector2.zero;

            var fill = new GameObject("Fill").AddComponent<Image>();
            fill.transform.SetParent(fillArea, false);
            fill.color = new Color(0.22f, 0.78f, 0.96f, 1f);
            var fillRt = fill.rectTransform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            var handleArea = new GameObject("HandleArea").AddComponent<RectTransform>();
            handleArea.SetParent(srt, false);
            handleArea.anchorMin = Vector2.zero; handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = handleArea.offsetMax = Vector2.zero;

            var handle = new GameObject("Handle").AddComponent<Image>();
            handle.transform.SetParent(handleArea, false);
            handle.color = Color.white;
            var hRt = handle.rectTransform;
            hRt.anchorMin = new Vector2(0f, 0.5f);
            hRt.anchorMax = new Vector2(0f, 0.5f);
            hRt.sizeDelta = new Vector2(20f, 24f);

            slider = sGo.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.fillRect = fillRt;
            slider.handleRect = hRt;
            slider.targetGraphic = handle;
            slider.onValueChanged.AddListener(v => onVal(v));

            var vGo = new GameObject("Val");
            vGo.transform.SetParent(row, false);
            var vrt = vGo.AddComponent<RectTransform>();
            vrt.anchorMin = new Vector2(0.86f, 0f); vrt.anchorMax = new Vector2(1f, 1f);
            vrt.offsetMin = vrt.offsetMax = Vector2.zero;
            valText = vGo.AddComponent<Text>();
            valText.font = _font;
            valText.fontSize = 18;
            valText.alignment = TextAnchor.MiddleRight;
            valText.color = new Color(0.85f, 0.95f, 1f, 1f);
        }

        Toggle AddToggleRow(RectTransform parent, string label, Action<bool> onVal)
        {
            var row = CreateRow(parent, 34f);
            AddRowLabel(row, label, 0.72f);

            var tGo = new GameObject("Toggle");
            tGo.transform.SetParent(row, false);
            var trt = tGo.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.88f, 0.1f); trt.anchorMax = new Vector2(1f, 0.9f);
            trt.offsetMin = trt.offsetMax = Vector2.zero;

            var bg = new GameObject("Bg").AddComponent<Image>();
            bg.transform.SetParent(trt, false);
            bg.color = new Color(0.20f, 0.24f, 0.30f, 1f);
            bg.rectTransform.anchorMin = Vector2.zero; bg.rectTransform.anchorMax = Vector2.one;
            bg.rectTransform.offsetMin = bg.rectTransform.offsetMax = Vector2.zero;

            var check = new GameObject("Check").AddComponent<Image>();
            check.transform.SetParent(bg.rectTransform, false);
            check.color = new Color(0.22f, 0.86f, 0.56f, 1f);
            check.rectTransform.anchorMin = new Vector2(0.18f, 0.18f); check.rectTransform.anchorMax = new Vector2(0.82f, 0.82f);
            check.rectTransform.offsetMin = check.rectTransform.offsetMax = Vector2.zero;

            var tog = tGo.AddComponent<Toggle>();
            tog.targetGraphic = bg;
            tog.graphic = check;
            tog.onValueChanged.AddListener(v => onVal(v));
            return tog;
        }

        void AddButton(RectTransform parent, string label, Color bg, Action onClick)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = bg;
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());

            var tGo = new GameObject("Text");
            tGo.transform.SetParent(go.transform, false);
            var rt = tGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = tGo.AddComponent<Text>();
            t.font = _font;
            t.text = label;
            t.fontSize = 18;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
        }

        RectTransform CreateRow(RectTransform parent, float height)
        {
            var go = new GameObject("Row");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, height);
            return rt;
        }

        void AddRowLabel(RectTransform row, string label, float rightFrac)
        {
            var go = new GameObject("Name");
            go.transform.SetParent(row, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(rightFrac, 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<Text>();
            t.font = _font;
            t.text = label;
            t.fontSize = 18;
            t.alignment = TextAnchor.MiddleLeft;
            t.color = new Color(0.90f, 0.92f, 0.96f, 1f);
        }
    }
}
