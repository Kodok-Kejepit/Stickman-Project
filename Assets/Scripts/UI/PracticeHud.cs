using Stickman.Core;
using Stickman.Hero;
using Stickman.Input;
using Stickman.Practice;
using UnityEngine;
using UnityEngine.UI;

namespace Stickman.UI
{
    /// <summary>
    /// Full responsive uGUI HUD for the Practice scene: mode switcher, collapsible Practice Tools drawer,
    /// live cable/drill/course telemetry banner, golden energy bar, minimap, floating damage number pool,
    /// multitouch MOBA joystick + skill buttons, Settings modal, and Layout Editor.
    /// Zero GC allocations during normal gameplay frames.
    /// </summary>
    public sealed class PracticeHud : MonoBehaviour
    {
        struct FloatingDamage
        {
            public Text Label;
            public RectTransform Rect;
            public Vector3 WorldPos;
            public float Timer;
        }

        [SerializeField] HeroController hero;
        [SerializeField] PracticeManager practice;
        [SerializeField] InputRouter inputRouter;

        Font _font;
        UnityEngine.Camera _mainCam;
        RectTransform _safeArea;

        // Top HUD
        Text _fpsText;
        Text _bannerText;
        float _fpsTimer;
        int _fpsFrames;
        float _fpsAccum;

        // Practice Tools drawer
        GameObject _toolsDrawer;
        Text _infEnergyLabel;
        Text _noCdLabel;
        Text _slowLabel;
        Text _diffLabel;

        // Energy bar
        Image _energyFill;
        Image _energyFrame;
        Text _energyText;
        int _lastEnergyInt = -1;

        // Touch controls
        GameObject _touchRoot;
        TouchJoystick _joystick;
        AimStick _aimStick;
        SkillButton _basicBtn;
        SkillButton _spinBtn;
        SkillButton _ultBtn;
        RectTransform[] _layoutTargets;
        Vector2[] _layoutBasePos;

        // Overlays
        SettingsPanel _settingsPanel;
        LayoutEditorOverlay _layoutEditor;

        // Pooled damage numbers
        readonly FloatingDamage[] _dmgPool = new FloatingDamage[16];
        RectTransform _dmgRoot;
        int _dmgCursor;

        // Dirty-checked banner cache
        int _lastChain = -1;
        int _lastDrillAttempts = -1;
        int _lastDrillStreak = -1;
        int _lastCourseSec = -1;
        PracticeMode _lastMode = (PracticeMode)(-1);

        public SettingsPanel Settings => _settingsPanel;
        public GameObject ToolsDrawer => _toolsDrawer;

        public void SetReferences(HeroController h, PracticeManager p, InputRouter ir)
        {
            hero = h;
            practice = p;
            inputRouter = ir;
        }

        void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _mainCam = UnityEngine.Camera.main;
            if (hero == null) hero = FindFirstObjectByType<HeroController>();
            if (practice == null) practice = FindFirstObjectByType<PracticeManager>();
            if (inputRouter == null) inputRouter = FindFirstObjectByType<InputRouter>();

            BuildHud();
            CombatDummy.OnAnyDamageDealt += SpawnDamagePopup;
        }

        void OnDestroy()
        {
            CombatDummy.OnAnyDamageDealt -= SpawnDamagePopup;
        }

        void Start()
        {
            ApplySettingsToHud();
        }

        void BuildHud()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1.0f;

            gameObject.AddComponent<GraphicRaycaster>();

            var safeGo = new GameObject("SafeArea");
            safeGo.transform.SetParent(transform, false);
            _safeArea = safeGo.AddComponent<RectTransform>();
            _safeArea.anchorMin = Vector2.zero;
            _safeArea.anchorMax = Vector2.one;
            _safeArea.offsetMin = _safeArea.offsetMax = Vector2.zero;
            safeGo.AddComponent<SafeAreaFitter>();

            // Bottom-most layer: empty-space camera pan pad -> touch controls -> HUD panels -> modals
            BuildCameraPanPad();
            BuildTouchControls();
            BuildDamagePool();
            BuildEnergyBar();
            BuildTopCenterBanner();
            BuildTopLeftModesAndTools();
            BuildTopRightMinimapAndSettings();
            BuildOverlays();
            ApplySettingsToHud();
        }

        void BuildCameraPanPad()
        {
            var panGo = new GameObject("CameraPanPad");
            panGo.transform.SetParent(_safeArea, false);
            var rt = panGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = panGo.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            panGo.AddComponent<CameraPanPad>().Initialize(inputRouter);
        }

        void BuildTopLeftModesAndTools()
        {
            var topBar = CreatePanel("ModeBar", _safeArea, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -16f), new Vector2(650f, 52f));
            var hlg = topBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;

            AddHudButton(topBar, "Free", new Color(0.16f, 0.44f, 0.66f, 0.92f), () => practice?.SetMode(PracticeMode.FreePractice));
            AddHudButton(topBar, "Drill", new Color(0.18f, 0.52f, 0.44f, 0.92f), () => practice?.SetMode(PracticeMode.StraightDrill));
            AddHudButton(topBar, "Course", new Color(0.56f, 0.42f, 0.16f, 0.92f), () => practice?.SetMode(PracticeMode.ChainCourse));
            AddHudButton(topBar, "Tools", new Color(0.24f, 0.28f, 0.36f, 0.92f), () =>
            {
                if (_toolsDrawer != null) _toolsDrawer.SetActive(!_toolsDrawer.activeSelf);
            });

            var fpsGo = new GameObject("FpsCounter");
            fpsGo.transform.SetParent(topBar, false);
            _fpsText = fpsGo.AddComponent<Text>();
            _fpsText.font = _font;
            _fpsText.fontSize = 18;
            _fpsText.alignment = TextAnchor.MiddleLeft;
            _fpsText.color = new Color(0.55f, 1f, 0.65f, 1f);
            _fpsText.text = "60 FPS";

            // Collapsible Practice Tools drawer
            var drawerRt = CreatePanel("ToolsDrawer", _safeArea, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -76f), new Vector2(215f, 560f));
            drawerRt.gameObject.AddComponent<Image>().color = new Color(0.07f, 0.10f, 0.14f, 0.88f);
            var vlg = drawerRt.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.spacing = 6f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = true;

            _infEnergyLabel = AddHudButton(drawerRt, "Inf Energy: OFF", new Color(0.18f, 0.32f, 0.44f, 1f), ToggleInfEnergy);
            _noCdLabel = AddHudButton(drawerRt, "No CD: OFF", new Color(0.18f, 0.32f, 0.44f, 1f), ToggleNoCd);
            _slowLabel = AddHudButton(drawerRt, "Slow 0.5x: OFF", new Color(0.18f, 0.32f, 0.44f, 1f), ToggleSlowMo);
            _diffLabel = AddHudButton(drawerRt, "Drill: Easy", new Color(0.20f, 0.40f, 0.36f, 1f), CycleDifficulty);
            AddHudButton(drawerRt, "+ Dummy", new Color(0.45f, 0.28f, 0.24f, 1f), () => practice?.SpawnDummyNearHero(false));
            AddHudButton(drawerRt, "+ Patrol", new Color(0.45f, 0.28f, 0.24f, 1f), () => practice?.SpawnDummyNearHero(true));
            AddHudButton(drawerRt, "Clear Bots", new Color(0.32f, 0.22f, 0.22f, 1f), () => practice?.ClearDummies());
            AddHudButton(drawerRt, "TP Mid", new Color(0.22f, 0.28f, 0.36f, 1f), () => practice?.TeleportToSpot("MidLane"));
            AddHudButton(drawerRt, "TP Blue Buff", new Color(0.20f, 0.26f, 0.44f, 1f), () => practice?.TeleportToSpot("BlueBuff"));
            AddHudButton(drawerRt, "TP Red Buff", new Color(0.44f, 0.24f, 0.22f, 1f), () => practice?.TeleportToSpot("RedBuff"));
            AddHudButton(drawerRt, "TP EXP", new Color(0.22f, 0.28f, 0.36f, 1f), () => practice?.TeleportToSpot("ExpLane"));
            AddHudButton(drawerRt, "TP Gold", new Color(0.22f, 0.28f, 0.36f, 1f), () => practice?.TeleportToSpot("GoldLane"));
            AddHudButton(drawerRt, "TP Base", new Color(0.18f, 0.36f, 0.48f, 1f), () => practice?.TeleportToSpot("BlueBase"));

            _toolsDrawer = drawerRt.gameObject;
            _toolsDrawer.SetActive(false);
        }

        void BuildTopCenterBanner()
        {
            var banner = CreatePanel("TelemetryBanner", _safeArea, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(540f, 52f));
            banner.pivot = new Vector2(0.5f, 1f);
            banner.gameObject.AddComponent<Image>().color = new Color(0.06f, 0.09f, 0.13f, 0.80f);

            var tGo = new GameObject("Text");
            tGo.transform.SetParent(banner, false);
            var trt = tGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(12f, 4f); trt.offsetMax = new Vector2(-12f, -4f);
            _bannerText = tGo.AddComponent<Text>();
            _bannerText.font = _font;
            _bannerText.fontSize = 18;
            _bannerText.alignment = TextAnchor.MiddleCenter;
            _bannerText.color = new Color(0.92f, 0.96f, 1f, 1f);
            _bannerText.text = "FREE PRACTICE | WASD Move · E Cable · Q Spin · R Ult";
        }

        void BuildTopRightMinimapAndSettings()
        {
            const float mapSize = 216f;
            var mapGo = new GameObject("Minimap");
            mapGo.transform.SetParent(_safeArea, false);
            var mrt = mapGo.AddComponent<RectTransform>();
            mrt.anchorMin = mrt.anchorMax = new Vector2(1f, 1f);
            mrt.pivot = new Vector2(1f, 1f);
            mrt.anchoredPosition = new Vector2(-16f, -16f);
            mrt.sizeDelta = new Vector2(mapSize, mapSize);
            mapGo.AddComponent<MinimapView>().Initialize(hero, practice, mapSize, inputRouter);

            var setRt = CreatePanel("SettingsBtn", _safeArea, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-246f, -16f), new Vector2(120f, 46f));
            setRt.pivot = new Vector2(1f, 1f);
            AddHudButton(setRt, "Settings", new Color(0.18f, 0.24f, 0.32f, 0.92f), () =>
            {
                if (_settingsPanel == null) return;
                if (inputRouter != null) inputRouter.BlockGameplayInput = true;
                _settingsPanel.Open();
            });
        }

        void BuildEnergyBar()
        {
            var bar = CreatePanel("EnergyBar", _safeArea, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 38f), new Vector2(440f, 36f));
            bar.pivot = new Vector2(0.5f, 0f);
            _energyFrame = bar.gameObject.AddComponent<Image>();
            _energyFrame.color = new Color(0.08f, 0.10f, 0.14f, 0.90f);

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(bar, false);
            var frt = fillGo.AddComponent<RectTransform>();
            frt.anchorMin = new Vector2(0.015f, 0.14f);
            frt.anchorMax = new Vector2(0.985f, 0.86f);
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            _energyFill = fillGo.AddComponent<Image>();
            _energyFill.type = Image.Type.Filled;
            _energyFill.fillMethod = Image.FillMethod.Horizontal;
            _energyFill.color = new Color(0.96f, 0.74f, 0.16f, 1f);

            var txtGo = new GameObject("Value");
            txtGo.transform.SetParent(bar, false);
            var trt = txtGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            _energyText = txtGo.AddComponent<Text>();
            _energyText.font = _font;
            _energyText.fontSize = 19;
            _energyText.fontStyle = FontStyle.Bold;
            _energyText.alignment = TextAnchor.MiddleCenter;
            _energyText.color = Color.white;
        }

        void BuildTouchControls()
        {
            var touchGo = new GameObject("TouchControls");
            touchGo.transform.SetParent(_safeArea, false);
            var trt = touchGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            _touchRoot = touchGo;

            // Left floating joystick hit zone (leaves top-left & upper center free for CameraPanPad)
            var joyZone = new GameObject("JoystickZone");
            joyZone.transform.SetParent(trt, false);
            var jzRt = joyZone.AddComponent<RectTransform>();
            jzRt.anchorMin = Vector2.zero;
            jzRt.anchorMax = new Vector2(0.36f, 0.60f);
            jzRt.offsetMin = jzRt.offsetMax = Vector2.zero;
            var jzImg = joyZone.AddComponent<Image>();
            jzImg.color = new Color(0f, 0f, 0f, 0f);

            Vector2 joyBase = new Vector2(210f, 195f);
            var joyRing = CreateCircleControl("JoystickRing", trt, Vector2.zero, Vector2.zero, joyBase, 220f, new Color(1f, 1f, 1f, 0.25f));
            var joyKnob = CreateCircleControl("JoystickKnob", joyRing, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 94f, new Color(0.32f, 0.88f, 1f, 0.65f));
            joyKnob.pivot = new Vector2(0.5f, 0.5f);
            joyRing.GetComponent<Image>().sprite = UiSprites.Ring;
            joyRing.GetComponent<Image>().raycastTarget = false;
            joyKnob.GetComponent<Image>().raycastTarget = false;

            _joystick = joyZone.AddComponent<TouchJoystick>();
            _joystick.Initialize(trt, joyRing, joyKnob, joyBase, 105f);

            // Right-hand MOBA skill arc
            Vector2 basicBase = new Vector2(-135f, 130f);
            Vector2 spinBase = new Vector2(-325f, 115f);
            Vector2 cableBase = new Vector2(-265f, 275f);
            Vector2 ultBase = new Vector2(-115f, 325f);

            var basicRt = CreateSkillButton("BasicBtn", trt, basicBase, 154f, new Color(0.86f, 0.36f, 0.24f, 0.84f), "ATK", out _basicBtn);
            var spinRt = CreateSkillButton("SpinBtn", trt, spinBase, 126f, new Color(0.22f, 0.62f, 0.88f, 0.84f), "S1", out _spinBtn);
            var ultRt = CreateSkillButton("UltBtn", trt, ultBase, 126f, new Color(0.84f, 0.58f, 0.16f, 0.84f), "ULT", out _ultBtn);

            // Skill 2 Cable AimStick + Cancel Zone
            var cableRt = CreateCircleControl("CableBtn", trt, new Vector2(1f, 0f), new Vector2(1f, 0f), cableBase, 152f, new Color(0.14f, 0.62f, 0.92f, 0.85f));
            var cableKnob = CreateCircleControl("Knob", cableRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 66f, new Color(0.85f, 0.96f, 1f, 0.85f));
            cableKnob.pivot = new Vector2(0.5f, 0.5f);
            cableKnob.GetComponent<Image>().raycastTarget = false;

            var cLabel = new GameObject("Label").AddComponent<Text>();
            cLabel.transform.SetParent(cableRt, false);
            cLabel.font = _font; cLabel.text = "CABLE"; cLabel.fontSize = 22; cLabel.fontStyle = FontStyle.Bold;
            cLabel.alignment = TextAnchor.MiddleCenter; cLabel.color = Color.white; cLabel.raycastTarget = false;
            cLabel.rectTransform.anchorMin = Vector2.zero; cLabel.rectTransform.anchorMax = Vector2.one;
            cLabel.rectTransform.offsetMin = cLabel.rectTransform.offsetMax = Vector2.zero;

            var cancelRt = CreateCircleControl("CancelZone", trt, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-125f, 470f), 116f, new Color(0.45f, 0.16f, 0.18f, 0.72f));
            var cancelTxt = new GameObject("Label").AddComponent<Text>();
            cancelTxt.transform.SetParent(cancelRt, false);
            cancelTxt.font = _font; cancelTxt.text = "X"; cancelTxt.fontSize = 30; cancelTxt.fontStyle = FontStyle.Bold;
            cancelTxt.alignment = TextAnchor.MiddleCenter; cancelTxt.color = Color.white; cancelTxt.raycastTarget = false;
            cancelTxt.rectTransform.anchorMin = Vector2.zero; cancelTxt.rectTransform.anchorMax = Vector2.one;
            cancelTxt.rectTransform.offsetMin = cancelTxt.rectTransform.offsetMax = Vector2.zero;

            _aimStick = cableRt.gameObject.AddComponent<AimStick>();
            _aimStick.Initialize(cableRt, cableKnob, cancelRt, cableRt.GetComponent<Image>(), cancelRt.GetComponent<Image>());

            if (inputRouter != null)
            {
                inputRouter.Setup(_joystick, _aimStick, _mainCam, hero != null ? hero.transform : null);
                _basicBtn.OnPressed += inputRouter.TriggerTouchBasic;
                _spinBtn.OnPressed += inputRouter.TriggerTouchSpin;
                _ultBtn.OnPressed += inputRouter.TriggerTouchUlt;
            }

            _layoutTargets = new[] { joyRing, basicRt, spinRt, cableRt, ultRt };
            _layoutBasePos = new[] { joyBase, basicBase, spinBase, cableBase, ultBase };
        }

        void BuildDamagePool()
        {
            _dmgRoot = CreatePanel("DamagePopups", _safeArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _dmgRoot.offsetMin = _dmgRoot.offsetMax = Vector2.zero;
            for (int i = 0; i < _dmgPool.Length; i++)
            {
                var go = new GameObject("Dmg_" + i);
                go.transform.SetParent(_dmgRoot, false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(180f, 44f);
                var t = go.AddComponent<Text>();
                t.font = _font;
                t.fontSize = 24;
                t.fontStyle = FontStyle.Bold;
                t.alignment = TextAnchor.MiddleCenter;
                t.raycastTarget = false;
                go.SetActive(false);
                _dmgPool[i] = new FloatingDamage { Label = t, Rect = rt };
            }
        }

        void BuildOverlays()
        {
            var settingsGo = new GameObject("SettingsPanel");
            settingsGo.transform.SetParent(_safeArea, false);
            settingsGo.AddComponent<RectTransform>();
            _settingsPanel = settingsGo.AddComponent<SettingsPanel>();
            _settingsPanel.Initialize(_font, ApplySettingsToHud, OpenLayoutEditor, () =>
            {
                if (inputRouter != null) inputRouter.BlockGameplayInput = false;
            });

            var layoutGo = new GameObject("LayoutEditorOverlay");
            layoutGo.transform.SetParent(_safeArea, false);
            layoutGo.AddComponent<RectTransform>();
            _layoutEditor = layoutGo.AddComponent<LayoutEditorOverlay>();
            _layoutEditor.Initialize(_font, _layoutTargets, _layoutBasePos,
                onApply: () =>
                {
                    if (inputRouter != null) inputRouter.BlockGameplayInput = false;
                    ApplySettingsToHud();
                },
                onCancel: () =>
                {
                    if (inputRouter != null) inputRouter.BlockGameplayInput = false;
                    ApplySettingsToHud();
                });
        }

        void OpenLayoutEditor()
        {
            if (_touchRoot != null) _touchRoot.SetActive(true);
            if (inputRouter != null) inputRouter.BlockGameplayInput = true;
            _layoutEditor?.Open();
        }

        public void ApplySettingsToHud()
        {
            var s = SettingsStore.Current;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = FrameRate.ChooseTarget(Screen.currentResolution.refreshRateRatio.value, s.targetFps);

            if (_fpsText != null) _fpsText.gameObject.SetActive(s.showFps);

            bool showTouch = inputRouter != null && inputRouter.TouchUiEnabled;
            if (_touchRoot != null) _touchRoot.SetActive(showTouch);

            if (_layoutTargets != null && _layoutBasePos != null)
            {
                _joystick?.SetRestAnchoredPos(_layoutBasePos[0] + s.joystickOffset, s.joystickScale);
                ApplyButtonLayout(1, _layoutBasePos[1] + s.basicOffset, s.basicScale);
                ApplyButtonLayout(2, _layoutBasePos[2] + s.spinOffset, s.spinScale);
                ApplyButtonLayout(3, _layoutBasePos[3] + s.cableOffset, s.cableScale);
                ApplyButtonLayout(4, _layoutBasePos[4] + s.ultOffset, s.ultScale);
            }
        }

        void ApplyButtonLayout(int idx, Vector2 pos, float scale)
        {
            if (_layoutTargets[idx] == null) return;
            _layoutTargets[idx].anchoredPosition = pos;
            _layoutTargets[idx].localScale = Vector3.one * scale;
        }

        void Update()
        {
            float unscaled = Time.unscaledDeltaTime;

            // Throttled FPS counter (no string alloc every frame)
            _fpsAccum += unscaled;
            _fpsFrames++;
            _fpsTimer += unscaled;
            if (_fpsTimer >= 0.25f && _fpsText != null && _fpsText.gameObject.activeSelf)
            {
                int fps = Mathf.RoundToInt(_fpsFrames / Mathf.Max(0.001f, _fpsAccum));
                _fpsText.text = fps + " FPS";
                _fpsTimer = 0f;
                _fpsFrames = 0;
                _fpsAccum = 0f;
            }

            if (hero != null && hero.Energy != null)
            {
                int eInt = Mathf.RoundToInt(hero.Energy.Current);
                if (eInt != _lastEnergyInt)
                {
                    _lastEnergyInt = eInt;
                    if (_energyFill != null) _energyFill.fillAmount = hero.Energy.Ratio;
                    if (_energyText != null) _energyText.text = eInt + " / " + Mathf.RoundToInt(hero.Energy.Max);
                }

                bool denied = (Time.time - hero.Energy.LastDeniedTime) < 0.22f;
                if (_energyFrame != null)
                    _energyFrame.color = denied ? new Color(0.75f, 0.14f, 0.14f, 0.95f) : new Color(0.08f, 0.10f, 0.14f, 0.90f);

                _basicBtn?.UpdateCooldown(hero.Basic.CooldownRatio, hero.Basic.CooldownRemaining);
                _spinBtn?.UpdateCooldown(hero.Spin.CooldownRatio, hero.Spin.CooldownRemaining);
                _ultBtn?.UpdateCooldown(hero.Dash.CooldownRatio, hero.Dash.CooldownRemaining);
            }

            UpdateBanner();
            UpdateDamagePopups(unscaled);
        }

        void UpdateBanner()
        {
            if (_bannerText == null || practice == null) return;

            if (practice.Mode == PracticeMode.StraightDrill && practice.Drill != null)
            {
                var sc = practice.Drill.Scorer;
                if (_lastMode != PracticeMode.StraightDrill || sc.Attempts != _lastDrillAttempts || sc.Streak != _lastDrillStreak)
                {
                    _lastMode = PracticeMode.StraightDrill;
                    _lastDrillAttempts = sc.Attempts;
                    _lastDrillStreak = sc.Streak;
                    var last = practice.Drill.LastResult;
                    _bannerText.text = $"DRILL | Acc {sc.AccuracyPercent:0}% · Streak {sc.Streak} · Avg {sc.AverageDeviationDeg:0.0}° (Last {last.AngleDeviationDeg:0.0}°)";
                }
            }
            else if (practice.Mode == PracticeMode.ChainCourse && practice.Course != null)
            {
                int sec10 = Mathf.FloorToInt(practice.Course.ElapsedSeconds * 10f);
                if (_lastMode != PracticeMode.ChainCourse || sec10 != _lastCourseSec)
                {
                    _lastMode = PracticeMode.ChainCourse;
                    _lastCourseSec = sec10;
                    if (practice.Course.IsFinished)
                        _bannerText.text = $"COURSE DONE | {practice.Course.ElapsedSeconds:0.00}s · Medal: {practice.Course.LastMedal}";
                    else
                        _bannerText.text = $"COURSE {practice.Course.CourseIndex + 1} | {practice.Course.ElapsedSeconds:0.0}s · CP {practice.Course.NextWaypoint}/{practice.Course.TotalWaypoints}";
                }
            }
            else
            {
                int chain = practice.CurrentChainCount;
                if (_lastMode != PracticeMode.FreePractice || chain != _lastChain)
                {
                    _lastMode = PracticeMode.FreePractice;
                    _lastChain = chain;
                    var r = practice.LastStraightReport;
                    if (r.Valid)
                        _bannerText.text = $"STRAIGHT CABLE | Angle {r.AngleBetweenDeg:0}° · Δt {r.DeltaTimeMs:0}ms · Chain {chain}";
                    else if (inputRouter != null && inputRouter.TouchUiEnabled)
                        _bannerText.text = "FREE PRACTICE | Ketuk Kabel = Quick · Geser = Aim · Geser Layar = Pan";
                    else
                        _bannerText.text = "FREE PRACTICE | WASD Move · E Cable · Q Spin · R Ult · RMB Pan";
                }
            }
        }

        void SpawnDamagePopup(Vector3 worldPos, int dmg, bool airborneBonus)
        {
            var slot = _dmgPool[_dmgCursor];
            _dmgCursor = (_dmgCursor + 1) % _dmgPool.Length;
            slot.WorldPos = worldPos;
            slot.Timer = 0.75f;
            slot.Label.text = airborneBonus ? dmg + "!" : dmg.ToString();
            slot.Label.color = airborneBonus ? new Color(0.35f, 0.94f, 1f, 1f) : new Color(1f, 0.86f, 0.32f, 1f);
            slot.Label.gameObject.SetActive(true);
            _dmgPool[(_dmgCursor - 1 + _dmgPool.Length) % _dmgPool.Length] = slot;
        }

        void UpdateDamagePopups(float dt)
        {
            if (_mainCam == null) _mainCam = UnityEngine.Camera.main;
            if (_mainCam == null || _dmgRoot == null) return;

            for (int i = 0; i < _dmgPool.Length; i++)
            {
                if (_dmgPool[i].Timer <= 0f) continue;
                var d = _dmgPool[i];
                d.Timer -= dt;
                d.WorldPos.y += dt * 1.6f;
                if (d.Timer <= 0f)
                {
                    d.Label.gameObject.SetActive(false);
                }
                else
                {
                    Vector3 sp = _mainCam.WorldToScreenPoint(d.WorldPos);
                    if (sp.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(_dmgRoot, sp, null, out Vector2 lp))
                    {
                        d.Rect.anchoredPosition = lp;
                    }
                }
                _dmgPool[i] = d;
            }
        }

        void ToggleInfEnergy()
        {
            if (hero == null) return;
            hero.InfiniteEnergy = !hero.InfiniteEnergy;
            if (_infEnergyLabel != null) _infEnergyLabel.text = "Inf Energy: " + (hero.InfiniteEnergy ? "ON" : "OFF");
        }

        void ToggleNoCd()
        {
            if (hero == null) return;
            hero.NoCooldown = !hero.NoCooldown;
            if (_noCdLabel != null) _noCdLabel.text = "No CD: " + (hero.NoCooldown ? "ON" : "OFF");
        }

        void ToggleSlowMo()
        {
            if (practice == null) return;
            practice.SetSlowMotion(!practice.SlowMotion);
            if (_slowLabel != null) _slowLabel.text = "Slow 0.5x: " + (practice.SlowMotion ? "ON" : "OFF");
        }

        void CycleDifficulty()
        {
            if (practice == null || practice.Drill == null || hero == null) return;
            var next = (DrillDifficulty)(((int)practice.Drill.Difficulty + 1) % 3);
            practice.Drill.SetDifficulty(next, hero.transform.position);
            if (_diffLabel != null) _diffLabel.text = "Drill: " + next;
        }

        static RectTransform CreatePanel(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static RectTransform CreateCircleControl(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, float diameter, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(diameter, diameter);
            var img = go.AddComponent<Image>();
            img.sprite = UiSprites.Circle;
            img.color = color;
            return rt;
        }

        RectTransform CreateSkillButton(string name, RectTransform parent, Vector2 pos, float diameter, Color color, string title, out SkillButton btn)
        {
            var rt = CreateCircleControl(name, parent, new Vector2(1f, 0f), new Vector2(1f, 0f), pos, diameter, color);

            var cdGo = new GameObject("CooldownFill");
            cdGo.transform.SetParent(rt, false);
            var cdRt = cdGo.AddComponent<RectTransform>();
            cdRt.anchorMin = Vector2.zero; cdRt.anchorMax = Vector2.one;
            cdRt.offsetMin = cdRt.offsetMax = Vector2.zero;
            var cdImg = cdGo.AddComponent<Image>();
            cdImg.sprite = UiSprites.Circle;
            cdImg.type = Image.Type.Filled;
            cdImg.fillMethod = Image.FillMethod.Radial360;
            cdImg.color = new Color(0f, 0f, 0f, 0.62f);
            cdImg.raycastTarget = false;

            var lbl = new GameObject("Label").AddComponent<Text>();
            lbl.transform.SetParent(rt, false);
            lbl.font = _font; lbl.text = title; lbl.fontSize = 22; lbl.fontStyle = FontStyle.Bold;
            lbl.alignment = TextAnchor.MiddleCenter; lbl.color = Color.white; lbl.raycastTarget = false;
            lbl.rectTransform.anchorMin = Vector2.zero; lbl.rectTransform.anchorMax = Vector2.one;
            lbl.rectTransform.offsetMin = lbl.rectTransform.offsetMax = Vector2.zero;

            var cdTxt = new GameObject("CdText").AddComponent<Text>();
            cdTxt.transform.SetParent(rt, false);
            cdTxt.font = _font; cdTxt.fontSize = 26; cdTxt.fontStyle = FontStyle.Bold;
            cdTxt.alignment = TextAnchor.MiddleCenter; cdTxt.color = new Color(1f, 0.9f, 0.3f, 1f); cdTxt.raycastTarget = false;
            cdTxt.rectTransform.anchorMin = Vector2.zero; cdTxt.rectTransform.anchorMax = Vector2.one;
            cdTxt.rectTransform.offsetMin = cdTxt.rectTransform.offsetMax = Vector2.zero;

            btn = rt.gameObject.AddComponent<SkillButton>();
            btn.Initialize(cdImg, cdTxt);
            return rt;
        }

        Text AddHudButton(RectTransform parent, string label, Color bg, System.Action onClick)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = UiSprites.Rounded;
            img.type = Image.Type.Sliced;
            img.color = bg;
            go.AddComponent<Button>().onClick.AddListener(() => onClick());

            var tGo = new GameObject("Text");
            tGo.transform.SetParent(go.transform, false);
            var rt = tGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = tGo.AddComponent<Text>();
            t.font = _font;
            t.text = label;
            t.fontSize = 17;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            return t;
        }
    }
}
