using System;
using Stickman.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Stickman.UI
{
    /// <summary>Drag-and-drop touch control layout editor persisting position offsets and scale multipliers.</summary>
    public sealed class LayoutEditorOverlay : MonoBehaviour
    {
        Font _font;
        RectTransform[] _targets;
        Vector2[] _basePositions;
        DraggableSlot[] _handles;
        SettingsData _working;
        Action _onApply;
        Action _onCancel;

        int _selectedIdx;
        Slider _scaleSlider;
        Text _scaleText;
        Text _selectedNameText;

        static readonly string[] SlotNames = { "Joystick", "Basic", "S1 Spin", "S2 Cable", "Ult" };

        public void Initialize(Font font, RectTransform[] targets, Vector2[] basePositions, Action onApply, Action onCancel)
        {
            _font = font;
            _targets = targets;
            _basePositions = basePositions;
            _onApply = onApply;
            _onCancel = onCancel;
            BuildUi();
            gameObject.SetActive(false);
        }

        public void Open()
        {
            _working = SettingsStore.Current.Clone();
            gameObject.SetActive(true);
            SyncHandlesToTargets();
            SelectSlot(3); // Default highlight: Cable button
        }

        void BuildUi()
        {
            var rt = GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.04f, 0.06f, 0.50f);

            // Top bar with title, scale slider, Save/Reset/Cancel
            var bar = new GameObject("TopBar");
            bar.transform.SetParent(transform, false);
            var brt = bar.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.15f, 0.84f);
            brt.anchorMax = new Vector2(0.85f, 0.96f);
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            bar.AddComponent<Image>().color = new Color(0.08f, 0.11f, 0.15f, 0.95f);

            var hlg = bar.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(16, 16, 10, 10);
            hlg.spacing = 12f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;

            _selectedNameText = CreateBarText(brt, "CABLE", 18);

            var sliderGo = new GameObject("ScaleSlider");
            sliderGo.transform.SetParent(brt, false);
            var sBg = new GameObject("Bg").AddComponent<Image>();
            sBg.transform.SetParent(sliderGo.transform, false);
            sBg.color = new Color(0.2f, 0.25f, 0.3f, 1f);
            sBg.rectTransform.anchorMin = new Vector2(0f, 0.35f); sBg.rectTransform.anchorMax = new Vector2(1f, 0.65f);
            sBg.rectTransform.offsetMin = sBg.rectTransform.offsetMax = Vector2.zero;

            var handle = new GameObject("Handle").AddComponent<Image>();
            handle.transform.SetParent(sliderGo.transform, false);
            handle.color = Color.white;
            handle.rectTransform.sizeDelta = new Vector2(22f, 28f);

            _scaleSlider = sliderGo.AddComponent<Slider>();
            _scaleSlider.minValue = 0.6f;
            _scaleSlider.maxValue = 1.6f;
            _scaleSlider.handleRect = handle.rectTransform;
            _scaleSlider.targetGraphic = handle;
            _scaleSlider.onValueChanged.AddListener(OnScaleChanged);

            _scaleText = CreateBarText(brt, "1.00x", 18);

            CreateBarBtn(brt, "Reset", new Color(0.55f, 0.25f, 0.22f, 1f), ResetWorking);
            CreateBarBtn(brt, "Cancel", new Color(0.32f, 0.36f, 0.42f, 1f), Cancel);
            CreateBarBtn(brt, "Save", new Color(0.20f, 0.64f, 0.34f, 1f), SaveAndClose);

            _handles = new DraggableSlot[_targets.Length];
            for (int i = 0; i < _targets.Length; i++)
            {
                var hGo = new GameObject("Handle_" + i);
                hGo.transform.SetParent(transform, false);
                var hRt = hGo.AddComponent<RectTransform>();
                hRt.anchorMin = _targets[i].anchorMin;
                hRt.anchorMax = _targets[i].anchorMax;
                hRt.sizeDelta = _targets[i].sizeDelta;

                var img = hGo.AddComponent<Image>();
                img.color = new Color(0.25f, 0.85f, 1.0f, 0.32f);

                var slot = hGo.AddComponent<DraggableSlot>();
                slot.Index = i;
                slot.Target = _targets[i];
                slot.OnSelected = SelectSlot;
                slot.OnMoved = HandleMoved;
                _handles[i] = slot;
            }
        }

        void SyncHandlesToTargets()
        {
            for (int i = 0; i < _handles.Length; i++)
            {
                _handles[i].GetComponent<RectTransform>().anchoredPosition = _targets[i].anchoredPosition;
                _handles[i].GetComponent<RectTransform>().localScale = _targets[i].localScale;
            }
        }

        void SelectSlot(int idx)
        {
            _selectedIdx = idx;
            _selectedNameText.text = SlotNames[idx];
            float sc = GetScale(idx);
            _scaleSlider.SetValueWithoutNotify(sc);
            _scaleText.text = sc.ToString("0.00") + "x";
        }

        void HandleMoved(int idx, Vector2 anchoredPos)
        {
            _handles[idx].GetComponent<RectTransform>().anchoredPosition = anchoredPos;
            Vector2 delta = anchoredPos - _basePositions[idx];
            switch (idx)
            {
                case 0: _working.joystickOffset = delta; break;
                case 1: _working.basicOffset = delta; break;
                case 2: _working.spinOffset = delta; break;
                case 3: _working.cableOffset = delta; break;
                case 4: _working.ultOffset = delta; break;
            }
        }

        void OnScaleChanged(float val)
        {
            switch (_selectedIdx)
            {
                case 0: _working.joystickScale = val; break;
                case 1: _working.basicScale = val; break;
                case 2: _working.spinScale = val; break;
                case 3: _working.cableScale = val; break;
                case 4: _working.ultScale = val; break;
            }
            _targets[_selectedIdx].localScale = Vector3.one * val;
            _handles[_selectedIdx].GetComponent<RectTransform>().localScale = Vector3.one * val;
            _scaleText.text = val.ToString("0.00") + "x";
        }

        float GetScale(int idx)
        {
            switch (idx)
            {
                case 0: return _working.joystickScale;
                case 1: return _working.basicScale;
                case 2: return _working.spinScale;
                case 3: return _working.cableScale;
                case 4: return _working.ultScale;
                default: return 1f;
            }
        }

        void ResetWorking()
        {
            _working.joystickOffset = Vector2.zero; _working.joystickScale = 1f;
            _working.basicOffset = Vector2.zero; _working.basicScale = 1f;
            _working.spinOffset = Vector2.zero; _working.spinScale = 1f;
            _working.cableOffset = Vector2.zero; _working.cableScale = 1f;
            _working.ultOffset = Vector2.zero; _working.ultScale = 1f;
            for (int i = 0; i < _targets.Length; i++)
            {
                _targets[i].anchoredPosition = _basePositions[i];
                _targets[i].localScale = Vector3.one;
            }
            SyncHandlesToTargets();
            SelectSlot(_selectedIdx);
        }

        void SaveAndClose()
        {
            SettingsStore.Save(_working);
            gameObject.SetActive(false);
            _onApply?.Invoke();
        }

        void Cancel()
        {
            gameObject.SetActive(false);
            _onCancel?.Invoke();
        }

        Text CreateBarText(RectTransform parent, string txt, int size)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = _font;
            t.text = txt;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            return t;
        }

        void CreateBarBtn(RectTransform parent, string txt, Color col, Action onClick)
        {
            var go = new GameObject("Btn_" + txt);
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = col;
            go.AddComponent<Button>().onClick.AddListener(() => onClick());
            var tGo = new GameObject("Text");
            tGo.transform.SetParent(go.transform, false);
            var rt = tGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = tGo.AddComponent<Text>();
            t.font = _font; t.text = txt; t.fontSize = 18; t.alignment = TextAnchor.MiddleCenter; t.color = Color.white;
        }
    }
}
