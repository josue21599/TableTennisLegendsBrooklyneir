using Brooklyneir.Art;
using Brooklyneir.Audio;
using Brooklyneir.CameraSystem;
using Brooklyneir.Config;
using Brooklyneir.Controls;
using Brooklyneir.Gameplay;
using Brooklyneir.Progression;
using Brooklyneir.SaveSystem;
using Brooklyneir.Settings;
using Brooklyneir.Tables;
using Brooklyneir.UI;
using UnityEngine;
using QualityLevel = Brooklyneir.SaveSystem.QualityLevel;

namespace Brooklyneir.Core
{
    /// <summary>
    /// Creates every system in a fixed order when the game starts. The project's only scene is
    /// empty on purpose: <see cref="GameBootstrap"/> creates this object after the scene loads,
    /// so there are no fragile scene references to break.
    /// </summary>
    public class GameRoot : MonoBehaviour
    {
        public static GameRoot Instance { get; private set; }
        static TouchInputController _input;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            // Landscape only (both directions, so two players can flip the phone freely).
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
            Build();
        }

        void Build()
        {
            BalanceConfig balance = BalanceLoader.Load();
            Game.Balance = balance;

            SaveService save = new SaveService(balance.economy.startingCoins, balance.economy.maxCoins);
            save.Load();
            Game.Save = save;
            SaveData data = save.Data;
            if (data.stats.matchesPlayed == 0 && !data.tutorialSeen && data.settings.quality == (int)QualityLevel.Medium)
            {
                data.settings.quality = (int)QualityManager.SuggestForDevice();
                save.MarkDirty();
            }
            Game.Economy = new Economy(data, balance.economy);

            MaterialLibrary.Init();

            AudioManager audio = gameObject.AddComponent<AudioManager>();
            audio.Init(data.settings.musicVolume, data.settings.sfxVolume);
            Game.Audio = audio;

            Haptics haptics = new Haptics();
            haptics.Init();
            haptics.Enabled = data.settings.vibration;
            Game.Haptics = haptics;

            GameObject camGo = new GameObject("MainCamera");
            camGo.transform.SetParent(transform, false);
            CameraDirector director = camGo.AddComponent<CameraDirector>();
            director.Init();
            director.ReducedEffects = data.settings.reducedCameraEffects;
            camGo.AddComponent<AudioListener>();
            Game.Camera = director;

            GameObject arenaGo = new GameObject("Arena");
            arenaGo.transform.SetParent(transform, false);
            ArenaManager arena = arenaGo.AddComponent<ArenaManager>();
            Game.Arena = arena;

            GameObject fxGo = new GameObject("VFX");
            fxGo.transform.SetParent(transform, false);
            VfxSystem vfx = fxGo.AddComponent<VfxSystem>();
            vfx.Init();
            Game.Vfx = vfx;

            GameObject previewGo = new GameObject("PreviewStage");
            previewGo.transform.SetParent(transform, false);
            PreviewStage preview = previewGo.AddComponent<PreviewStage>();
            preview.Init();
            Game.Preview = preview;

            GameObject inputGo = new GameObject("Input");
            inputGo.transform.SetParent(transform, false);
            TouchInputController input = inputGo.AddComponent<TouchInputController>();
            _input = input;
            input.Init(director.Cam);
            input.SetSensitivity(data.settings.touchSensitivity);

            GameObject matchGo = new GameObject("Match");
            matchGo.transform.SetParent(transform, false);
            MatchController match = matchGo.AddComponent<MatchController>();
            match.Init(balance, input, director, vfx);
            Game.Match = match;

            GameObject uiGo = new GameObject("UI");
            uiGo.transform.SetParent(transform, false);
            UIManager ui = uiGo.AddComponent<UIManager>();
            ui.Init();
            Game.UI = ui;

            ApplySettings();

            AppFlow flow = gameObject.AddComponent<AppFlow>();
            Game.Flow = flow;
            flow.Init(input);
        }

        /// <summary>Applies every saved setting to the running systems.</summary>
        public static void ApplySettings()
        {
            SettingsData s = Game.Save.Data.settings;
            if (Game.Audio != null) Game.Audio.SetVolumes(s.musicVolume, s.sfxVolume);
            if (Game.Haptics != null) Game.Haptics.Enabled = s.vibration;
            if (Game.Camera != null) Game.Camera.ReducedEffects = s.reducedCameraEffects;
            QualityProfile p = QualityManager.Apply((QualityLevel)s.quality);
            if (Game.Arena != null) Game.Arena.ApplyQuality(p);
            if (Game.Vfx != null) Game.Vfx.SetQualityMultiplier(p.ParticleMultiplier);
            if (Game.Match != null && Game.Match.Rackets != null)
            {
                Game.Match.Rackets[0].Assist = Game.Match.Human[0] && s.assistP1;
                Game.Match.Rackets[1].Assist = Game.Match.Human[1] && s.assistP2;
            }
            if (_input != null) _input.SetSensitivity(s.touchSensitivity);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && Game.Save != null) Game.Save.SaveIfDirty();
        }

        void OnApplicationQuit()
        {
            if (Game.Save != null) Game.Save.SaveIfDirty();
        }
    }

    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (GameRoot.Instance != null) return;
            GameObject root = new GameObject("TTLB_GameRoot");
            root.AddComponent<GameRoot>();
        }
    }
}
