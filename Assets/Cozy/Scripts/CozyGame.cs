using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Cozy
{
    public sealed partial class CozyGame : MonoBehaviour
    {
        public CozyTuning tuning;
        public Shader referenceShader;
        CozyRun run;
        CozyProfile profile;
        CozySave save;
        RectTransform stage, actors, effects, hud, overlay, terrain;
        Canvas canvas;
        Font font;
        Material penguinMaterial, companionMaterial;
        Texture2D[] idle, move, hit, defeat, capybaraIdle, catIdle;
        Texture2D disc, snowflake, spirit, toughSpirit, heart, rounded, ring, star, letter;
        RawImage penguin, shadow, background, joystick, knob, xpFill;
        Text scoreLabel, timeLabel, hintLabel, statusLabel, healthLabel;
        readonly List<UnityEngine.Object> ownedAssets = new();
        readonly List<RawImage> entityViews = new(), weather = new(), hearts = new();
        readonly List<(RawImage view, Animal animal)> menuPortraits = new();
        AudioSource music, sfx;
        AudioClip chime, hurt;
        bool muted => profile.settings.muted;
        bool english => profile.settings.english;
        static readonly Color Ink = new(.22f,.25f,.40f), Pink = new(.88f,.40f,.57f), Milk = new(.98f,.97f,1f);
        Animal selectedAnimal;
        Region selectedRegion;
        Difficulty selectedDifficulty = Difficulty.Normal;
        bool selectedEndless, dragging, inMenus = true, saveFailed, automatedReview;
        int finger = -1, blockedFrame = -1, usedViews, lastHP, lastKills;
        float accumulator, clock;
        Vector2 stickOrigin, input;
        RunPhase shownPhase = (RunPhase)(-1);
        string settingsReturn = "home", rebind;

        void Awake()
        {
            Application.targetFrameRate = 60;
            if (Application.isMobilePlatform) { Screen.sleepTimeout = SleepTimeout.NeverSleep; Screen.orientation = ScreenOrientation.AutoRotation; }
            automatedReview = Array.IndexOf(Environment.GetCommandLineArgs(),"-cozy-review") >= 0;
            save = new CozySave(automatedReview ? Path.Combine("Temp","cozy-review-bootstrap.json") : Path.Combine(Application.persistentDataPath, "commercial-profile.json"));
            profile = automatedReview ? new CozyProfile() : save.Load(); profile.legacyBest = Mathf.Max(profile.legacyBest, PlayerPrefs.GetInt("Cozy.Penguin.Best", 0));
            font = Resources.Load<Font>("Cozy/NotoSansKR") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            idle = Frames("Idle"); move = Frames("Move"); hit = Frames("Hit"); defeat = Frames("Defeat");
            capybaraIdle = Frames("Capybara/Idle"); catIdle = Frames("Cat/Idle");
            var shader = referenceShader ? referenceShader : Shader.Find("Cozy/ReferenceSprite");
            if (shader)
            {
                penguinMaterial = new Material(shader); ownedAssets.Add(penguinMaterial);
                companionMaterial = new Material(shader); ownedAssets.Add(companionMaterial);
                companionMaterial.SetVector("_BackgroundThresholds",new Vector4(.09f,.14f,0,0));
            }
            CreateIcons(); CreateCommercialIcons(); CreateScene(); CreateAudio();
            if (!automatedReview && profile.InitializeTestUnlocks()) SaveProfile();
            Home();
        }
        string L(string ko, string en) => english ? en : ko;
        string AnimalName(Animal a) => (english ? CozyRules.AnimalsEN : CozyRules.AnimalsKO)[(int)a];
        string RegionName(Region r) => (english ? CozyRules.RegionsEN : CozyRules.RegionsKO)[(int)r];
        string DifficultyName(Difficulty d) => (english ? CozyRules.DifficultyEN : CozyRules.DifficultyKO)[(int)d];
        void Resize()
        {
            Rect safe = Screen.safeArea; if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0,0,Screen.width,Screen.height);
            stage.localScale = Vector3.one * Mathf.Min(safe.width/1280f,safe.height/720f);
            stage.anchoredPosition = safe.center - new Vector2(Screen.width,Screen.height)*.5f;
        }
        public void StartRun()
        {
            if (!profile.AnimalUnlocked((int)selectedAnimal) || !profile.RegionUnlocked((int)selectedRegion) || selectedEndless && !profile.EndlessUnlocked((int)selectedRegion)) { Selection(); return; }
            run = new CozyRun(selectedAnimal,selectedRegion,selectedDifficulty,selectedEndless,Environment.TickCount);
            profile.activeRun = run; SaveProfile(); EnterRun();
        }
        void ContinueRun()
        {
            run = profile.activeRun; if (run == null) return;
            selectedAnimal = run.animal; selectedRegion = run.region; selectedDifficulty = run.difficulty; selectedEndless = run.endless; EnterRun();
        }
        void EnterRun()
        {
            ClearMenu(); inMenus = false; accumulator = 0; shownPhase = (RunPhase)(-1);
            hud.gameObject.SetActive(true); actors.gameObject.SetActive(true); effects.gameObject.SetActive(true);
            SetupTerrain(); lastHP = run.hp; lastKills = run.kills; music.Play(); RenderRun(); ShowPhase();
        }
        public void Pause() { if (run == null || run.Finished || run.phase == RunPhase.Paused) return; run.Pause(); SaveProfile(); ShowPhase(); }
        public void Resume() { if (run == null) return; run.Resume(); ClearMenu(); inMenus = false; accumulator = 0; shownPhase = (RunPhase)(-1); ShowPhase(); }
        void ShowPhase()
        {
            if (run == null || shownPhase == run.phase) return;
            shownPhase = run.phase;
            if (run.Finished) { profile.Settle(run); SaveProfile(); Result(); }
            else if (run.phase == RunPhase.Paused) PauseMenu();
            else if (run.phase == RunPhase.LevelChoice || run.phase == RunPhase.RelicChoice) { SaveProfile(); ChoiceMenu(); }
            else { ClearMenu(); inMenus = false; }
        }
        void AfterChoice() { accumulator = 0; SaveProfile(); shownPhase = (RunPhase)(-1); ShowPhase(); }
        bool SaveProfile()
        {
            try { save.Write(profile); saveFailed = false; return true; }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is NotSupportedException)
            { saveFailed = true; Debug.LogWarning("Cozy save: " + e.Message); if (hintLabel) hintLabel.text = L("저장 실패 · 모험을 유지 중입니다", "Save failed · Your adventure is still open"); return false; }
        }
        void ReleaseInput() { input = Vector2.zero; dragging = false; finger = -1; if (joystick) { joystick.gameObject.SetActive(false); knob.gameObject.SetActive(false); } }
        Vector2 ScreenPoint(Vector2 screen) { RectTransformUtility.ScreenPointToLocalPointInRectangle(stage,screen,null,out var p); return p; }
        bool CanDrag(Vector2 p) => Mathf.Abs(p.x) < 625 && p.y < 260 && p.y > -320;
        bool KeyPressed(string name) { var key = Keyboard.current?[name] as UnityEngine.InputSystem.Controls.ButtonControl; return key != null && key.isPressed; }
        void ReadInput()
        {
            input = Vector2.zero; if (Time.frameCount <= blockedFrame) return;
            var k = Keyboard.current;
            if (k != null) input = new Vector2((KeyPressed(profile.settings.right)||k.rightArrowKey.isPressed?1:0)-(KeyPressed(profile.settings.left)||k.leftArrowKey.isPressed?1:0),(KeyPressed(profile.settings.up)||k.upArrowKey.isPressed?1:0)-(KeyPressed(profile.settings.down)||k.downArrowKey.isPressed?1:0));
            if (Gamepad.current != null) { Vector2 stick = Gamepad.current.leftStick.ReadValue(); if (stick.magnitude > .15f) input = stick; }
            if (Touchscreen.current != null) foreach (var t in Touchscreen.current.touches)
            {
                int id = t.touchId.ReadValue(); Vector2 p = ScreenPoint(t.position.ReadValue());
                if (t.press.wasPressedThisFrame && finger < 0 && CanDrag(p)) { finger = id; stickOrigin = p; dragging = true; }
                if (id != finger) continue;
                if (!t.press.isPressed) { ReleaseInput(); continue; } SetStick(p);
            }
            if (finger < 0 && Mouse.current != null)
            {
                var m = Mouse.current; Vector2 p = ScreenPoint(m.position.ReadValue());
                if (m.leftButton.wasPressedThisFrame && CanDrag(p)) { stickOrigin = p; dragging = true; }
                if (dragging && m.leftButton.isPressed) SetStick(p);
                if (m.leftButton.wasReleasedThisFrame) ReleaseInput();
            }
        }
        void SetStick(Vector2 p)
        {
            Vector2 delta = Vector2.ClampMagnitude(p-stickOrigin,68); input = delta/68;
            if (input.magnitude < .08f) input = Vector2.zero;
            joystick.gameObject.SetActive(true); knob.gameObject.SetActive(true); joystick.rectTransform.anchoredPosition = stickOrigin; knob.rectTransform.anchoredPosition = stickOrigin+delta;
        }
        void Update()
        {
            Resize(); clock += Mathf.Min(Time.unscaledDeltaTime,.05f);
            music.mute = muted; music.volume = profile.settings.music * .24f; sfx.volume = profile.settings.effects;
            if (rebind != null) { ReadRebind(); return; }
            bool pause = (Keyboard.current?.escapeKey.wasPressedThisFrame ?? false) || (Gamepad.current?.startButton.wasPressedThisFrame ?? false);
            if (pause && Time.frameCount > blockedFrame && run != null && !run.Finished) { if (run.phase == RunPhase.Paused) Resume(); else Pause(); }
            if (run != null && run.phase == RunPhase.Playing && !inMenus)
            {
                ReadInput(); accumulator += Mathf.Min(Time.unscaledDeltaTime,.05f);
                while (accumulator >= CozyRules.Step) { run.Tick(CozyRules.Step,input); accumulator -= CozyRules.Step; if (run.phase != RunPhase.Playing) { accumulator = 0; break; } }
                if (run.hp < lastHP) Tone(hurt,.35f); if (run.kills > lastKills) Tone(chime,.16f); lastHP = run.hp; lastKills = run.kills;
                ShowPhase();
            }
            if (run != null) RenderRun();
            foreach (var portrait in menuPortraits) if (portrait.view) portrait.view.texture = AnimalIdle(portrait.animal,clock);
            if (inMenus && EventSystem.current && !EventSystem.current.currentSelectedGameObject) FocusMenu();
        }
        void OnApplicationFocus(bool focus) { if (!focus && !automatedReview) Pause(); }
        void OnApplicationPause(bool paused) { if (paused) Pause(); }
        void OnApplicationQuit() { if (run != null && !run.Finished) { run.Pause(); SaveProfile(); } }
        void OnDestroy() { AudioListener.pause = false; if (canvas) Destroy(canvas.gameObject); foreach (var item in ownedAssets) if (item) Destroy(item); }
        static string TimeText(float t) => $"{(int)t / 60:00}:{(int)t % 60:00}";
#if UNITY_EDITOR
        public string RunSmokeChecks() => CozyChecks.Run();
        public void CaptureStoreScreens() => ScreenCapture.CaptureScreenshot("Temp/CozyGameplay.png");
        public void CaptureStoreFeature() => ScreenCapture.CaptureScreenshot("Temp/CozyFeaturePreview.png");
        public void CheckTouchInput() => StartCoroutine(InputChecks());
#endif
    }
}
