#if UNITY_EDITOR
namespace FastPlay
{
    using UnityEditor;
    using UnityEngine;

#if UNITY_6000_3_OR_NEWER
    using System.Linq;
    using System.Reflection;
    using UnityEditor.Overlays;
    using UnityEditor.Toolbars;

    /// <summary>
    /// ⚡ FAST PLAY ⚡ adds a toolbar button to start Play Mode without reloading Domain and Scene.
    /// This can significantly reduce the time it takes to enter Play Mode.
    /// 
    /// In a single click (or keyboard shortcut), it has the same effect as:
    ///   1. Opening the Edit > Project Settings > Editor window,
    ///   2. Selecting "Do not reload Domain or Scene" in the "Enter Play Mode Options" section.
    ///   3. Clicking the Play button,
    ///   4. Restoring your original "Enter Play Mode Options" configuration.
    ///   5. Closing the Project Settings window.
    /// 
    /// Keep in mind that disabling Domain and Scene reload can lead to issues,
    /// especially if your project relies on static variables. 
    /// So, make sure to also test frequently with normal Play Mode!
    ///
    /// This script must be placed in an Editor folder (inside the Assets or Packages folder).
    ///
    /// Created by Jonathan Tremblay, teacher at Cegep de Saint-Jerome.
    /// This project is available for distribution and modification under the MIT License.
    /// https://github.com/JonathanTremblay/UnityFastPlay
    /// </summary>
    [InitializeOnLoad]
    public class FastPlay
    {
        const string VERSION = "Version 1.0.1 (2026-03)";
        const string ELEMENT_ID = "Fast Play";
        const string TOOLTIP_TEXT = "Fast Play                    Shift+Alt+P\n<color=grey>(don't reload Domain and Scene)</color>";
        const string HEX_COLOR_ON = "#ff822d";
        const string HEX_COLOR_NORMAL = "#c4c4c4";
        const string HEX_COLOR_OFF = "#505050";
        const string INITIAL_MESSAGE = "<b><color=" + HEX_COLOR_ON + ">⚡\uFE0E FAST PLAY ⚡\uFE0E</color></b> Remember that static variables persist between plays. Ensure they are reset in your scripts.\n <size=10>** Fast Play is free and open source – For updates and feedback, visit <a href=\"https://github.com/JonathanTremblay/UnityFastPlay\">https://github.com/JonathanTremblay/UnityFastPlay</a> – " + VERSION + " **</size>";
        const string WARNING_TITLE = "Enter Play Mode Settings Warning";
        const string WARNING_MESSAGE = "Your current Enter Play Mode Settings are set to \"Do not reload Domain or Scene\". This is not recommended with Fast Play. After this play session, do you want to reset these options back to \"Reload Domain and Scene\"?";
        const string SHOW_TITLE = "⚡ Show Fast Play Button ⚡";
        const string SHOW_MESSAGE = "The Fast Play Button is hidden. Do you want to show it?\nIf not, you can enable it later by right-clicking the main toolbar.";
        const string CATCH_MESSAGE = "The ⚡ Fast Play ⚡ button visibility cannot be handled automatically. Right-click the main toolbar and select \"Fast Play\" to enable or disable it.";
        const string YES_RECOMMENDED = "Yes (recommended)";
        const string NO = "No";
        static bool _isFastPlayMode = false;
        static bool _didWarnAboutOverlay = false;

        // The 2 following variables are for testing purposes only:
        static bool _shouldClearPrefsForTesting = false;
        static bool _isReflectionUnavailableTest = false;

        /// <summary>
        /// EditorPrefs key: true when FastPlay has modified enterPlayModeOptions and hasn't restored them yet.
        /// </summary>
        static string HasModifiedSettingsKey => $"{Application.productName}_{ELEMENT_ID}_HasModifiedSettings";

        /// <summary>
        /// EditorPrefs key: stores the user's original enterPlayModeOptions (as int) before FastPlay changed them.
        /// </summary>
        static string OriginalOptionsKey => $"{Application.productName}_{ELEMENT_ID}_OriginalOptions";

        /// <summary>
        /// Persistent flag indicating whether FastPlay currently "owns" the enterPlayModeOptions value.
        /// </summary>
        static bool _hasModifiedSettings
        {
            get => EditorPrefs.GetBool(HasModifiedSettingsKey, false);
            set => EditorPrefs.SetBool(HasModifiedSettingsKey, value);
        }

        /// <summary>
        /// Persistent copy of the user's original enterPlayModeOptions, saved before FastPlay overwrites them.
        /// </summary>
        static EnterPlayModeOptions _storedOriginalOptions
        {
            get => (EnterPlayModeOptions)EditorPrefs.GetInt(OriginalOptionsKey, (int)EnterPlayModeOptions.None);
            set => EditorPrefs.SetInt(OriginalOptionsKey, (int)value);
        }

        /// <summary>
        /// Static constructor to subscribe to Play Mode state changes.
        /// Also recovers from abnormal shutdowns (crash, force-quit) that may have
        /// left the enterPlayModeOptions stuck on "fast play".
        /// </summary>
        static FastPlay()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            if (_shouldClearPrefsForTesting) ClearFastPlayButtonPrefs();

            // If the editor was closed (or crashed) while FastPlay owned the settings,
            // restore them now so the regular Play button doesn't silently skip reloads.
            if (_hasModifiedSettings && !EditorApplication.isPlaying) RestorePlayModeOptions();
        }

        /// <summary>
        /// Logs the initial Fast Play message when the editor loads.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void LogFastPlayMessage()
        {
            if (_shouldClearPrefsForTesting) ClearFastPlayButtonPrefs();
            if (_isFastPlayMode) Debug.Log(INITIAL_MESSAGE);
        }

        /// <summary>
        /// Handles Play Mode state changes to restore settings or reset state.
        /// </summary>
        /// <param name="state">The new Play Mode state.</param>
        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && _isFastPlayMode)
            {
                RestorePlayModeOptions();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                // Safety net: if settings were not restored yet (e.g. user stopped play
                // before EnteredPlayMode fired), restore them now.
                if (_hasModifiedSettings) RestorePlayModeOptions();
                _isFastPlayMode = false;
            }
            MainToolbar.Refresh(ELEMENT_ID); // Refresh the toolbar button state
        }

        /// <summary>
        /// Creates the Fast Play button in the main toolbar.
        /// </summary>
        /// <returns>An instance of the MainToolbarElement class representing the Fast Play button.</returns>
        [MainToolbarElement(ELEMENT_ID, defaultDockPosition = MainToolbarDockPosition.Middle, defaultDockIndex = 0)]  
        public static MainToolbarElement FastPlayButton()
        {
            MainToolbarContent content = new();
            bool isOn = _isFastPlayMode && EditorApplication.isPlaying;
            string color = isOn ? HEX_COLOR_ON : (EditorApplication.isPlaying ? HEX_COLOR_OFF : HEX_COLOR_NORMAL);
            content.text = $"<color={color}>⚡\uFE0E</color>";
            content.tooltip = TOOLTIP_TEXT;
            MainToolbarToggle element = new(content, isOn, OnFastPlayToggle);
            return element;
        }

        /// <summary>
        /// Handles the Fast Play toggle button click.
        /// </summary>
        /// <param name="isOn">True if the button is toggled on; otherwise, false.</param>
        static void OnFastPlayToggle(bool isOn)
        {
            if (isOn && !EditorApplication.isPlaying)
            {
                _isFastPlayMode = true;

                EnterPlayModeOptions currentOptions = EditorSettings.enterPlayModeOptions;
                bool isAlreadyFastPlay = currentOptions == (EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload);

                if (isAlreadyFastPlay && _hasModifiedSettings)
                {
                    // FastPlay itself left these settings behind (e.g. the user stopped play
                    // before they could be restored). Silently keep the stored original options.
                }
                else if (isAlreadyFastPlay)
                {
                    // The user (not FastPlay) set these options manually. Warn them.
                    bool shouldReset = EditorUtility.DisplayDialog(WARNING_TITLE, WARNING_MESSAGE, YES_RECOMMENDED, NO);
                    _storedOriginalOptions = shouldReset ? EnterPlayModeOptions.None : currentOptions;
                }
                else
                {
                    _storedOriginalOptions = currentOptions;
                }

                // Mark that FastPlay now owns the enterPlayModeOptions value.
                _hasModifiedSettings = true;

                EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;

                EditorApplication.ExecuteMenuItem("Edit/Play Mode/Play"); // Start playing
            }
            // Disable the next line if you don't want the button to also act as a stop button
            else EditorApplication.isPlaying = false;
        }

        /// <summary>
        /// Restores the user's original Play Mode options and clears the persistent "modified" flag.
        /// Uses the persisted EditorPrefs value so the restore works even after a domain reload
        /// or editor restart (where the static field _lastPlayModeOptions would have been reset).
        /// </summary>
        static void RestorePlayModeOptions()
        {
            EditorSettings.enterPlayModeOptions = _storedOriginalOptions;
            _hasModifiedSettings = false;
        }

        /// <summary>
        /// Menu item to trigger Fast Play via keyboard shortcut.
        /// </summary>
        [MenuItem("Edit/Play Mode/Fast Play #&p", false, 1100)] // Add a menu item with the shortcut Shift+Alt+P
        static void FastPlayShortcut() => OnFastPlayToggle(!EditorApplication.isPlaying);

        /// <summary>
        /// Initializes the overlay visibility check on load.
        /// </summary>
        [InitializeOnLoadMethod]
        public static void InitializeOverlayVisibility() => EditorApplication.delayCall += CheckOverlayOnStartup;

        /// <summary>
        /// Path to the project-level settings file.
        /// Stored in ProjectSettings/ so it is shared across machines via version control.
        /// </summary>
        const string SETTINGS_PATH = "ProjectSettings/FastPlaySettings.json";

        /// <summary>
        /// Serialisable container for project-level Fast Play preferences.
        /// </summary>
        [System.Serializable]
        class FastPlaySettings
        {
            public bool showButton = true;
        }

        /// <summary>
        /// Loads the project-level settings file.
        /// </summary>
        /// <returns>The deserialized settings, or null if the file does not exist yet.</returns>
        static FastPlaySettings LoadSettings()
        {
            try
            {
                if (!System.IO.File.Exists(SETTINGS_PATH)) return null;
                string json = System.IO.File.ReadAllText(SETTINGS_PATH);
                return JsonUtility.FromJson<FastPlaySettings>(json);
            }
            catch (System.Exception)
            {
                return null; // Treat corrupted or inaccessible file as "no settings"
            }
        }

        /// <summary>
        /// Saves the project-level settings file.
        /// </summary>
        static void SaveSettings(FastPlaySettings settings)
        {
            try
            {
                string json = JsonUtility.ToJson(settings, true);
                System.IO.File.WriteAllText(SETTINGS_PATH, json);
            }
            catch (System.Exception)
            {
                // Silently ignore write failures (read-only filesystem, locked file, etc.)
            }
        }

        /// <summary>
        /// Legacy EditorPrefs key used before project-level settings (v1.0.0 and earlier).
        /// Used for one-time migration only.
        /// </summary>
        static string LegacyHasCheckedVisibilityKey => $"{Application.productName}_{ELEMENT_ID}_HasCheckedInitialVisibility";

        /// <summary>
        /// EditorPrefs key: true once the project-level settings have been applied on this machine.
        /// Prevents CheckOverlayOnStartup from re-forcing the overlay visible on every domain reload.
        /// </summary>
        static string HasAppliedSettingsKey => $"{Application.productName}_{ELEMENT_ID}_HasAppliedSettings";

        /// <summary>
        /// Cross-project EditorPrefs key (not scoped to a product name) that remembers
        /// the user's last button-visibility preference. When the user adds FastPlay to a
        /// new project, this pref lets us honour their previous choice and skip the prompt
        /// if they already opted in. If no preference is stored, the user is always asked.
        /// </summary>
        const string CROSS_PROJECT_SHOW_BUTTON_KEY = "FastPlay_ShowButton";

        /// <summary>
        /// Persists the user's button-visibility preference in a cross-project EditorPrefs key.
        /// When the user opts in ("show"), the preference is recorded so future projects
        /// can honour it without prompting. When the user opts out ("hide"), the key is
        /// removed so the user will be asked again in new projects.
        /// </summary>
        static void RecordCrossProjectChoice(bool showButton)
        {
            if (showButton) EditorPrefs.SetBool(CROSS_PROJECT_SHOW_BUTTON_KEY, true);
            else EditorPrefs.DeleteKey(CROSS_PROJECT_SHOW_BUTTON_KEY);
        }

        /// <summary>
        /// Migrates the old per-machine EditorPrefs visibility flag to the new project-level settings file.
        /// If the old key exists and no settings file has been created yet, the current overlay state
        /// is saved as the user's choice so they are never re-prompted.
        /// </summary>
        /// <param name="overlay">The Fast Play overlay instance, used to read current visibility.</param>
        /// <returns>True if migration occurred (settings file was created), false otherwise.</returns>
        static bool TryMigrateLegacyPrefs(Overlay overlay)
        {
            if (!EditorPrefs.HasKey(LegacyHasCheckedVisibilityKey)) return false;

            // Old key exists — user already went through the prompt in a previous version.
            // Migrate: use the overlay's current displayed state as the recorded choice.
            if (!System.IO.File.Exists(SETTINGS_PATH))
            {
                bool isVisible = overlay != null && overlay.displayed;
                SaveSettings(new FastPlaySettings { showButton = isVisible });
                RecordCrossProjectChoice(isVisible);
            }

            // Clean up the legacy key so this migration only runs once.
            EditorPrefs.DeleteKey(LegacyHasCheckedVisibilityKey);
            return true;
        }

        /// <summary>
        /// Menu item to toggle the Fast Play button visibility.
        /// </summary>
        [MenuItem("Edit/Play Mode/Show Fast Play Button", false, 1101)] // Add a menu item to toggle the Fast Play button visibility
        static void ToggleFastPlayButton()
        {
            _didWarnAboutOverlay = false;
            Overlay myOverlay = GetFastPlayOverlay();

            if (myOverlay == null)
            {
                LogOverlayWarningIfNeeded();
                return; // Cannot toggle: overlay is inaccessible, so no preference is saved
            }

            // Toggle the actual overlay state, then persist the choice
            bool shouldShow = !myOverlay.displayed;
            myOverlay.displayed = shouldShow;
            SaveSettings(new FastPlaySettings { showButton = shouldShow });
            RecordCrossProjectChoice(shouldShow);
        }

        /// <summary>
        /// Menu validation to set the checkmark based on current visibility state.
        /// </summary>
        /// <returns>Always returns true to keep the menu item enabled.</returns>
        [MenuItem("Edit/Play Mode/Show Fast Play Button", true)]
        static bool ToggleFastPlayButtonValidate()
        {
            // 1. Try to get the actual overlay status
            Overlay myOverlay = GetFastPlayOverlay();
            bool isActuallyVisible = myOverlay != null && myOverlay.displayed;

            // 2. Fallback to prefs if overlay isn't found (e.g. during initialization)
            if (myOverlay == null)
            {
                // If overlay can't be queried, assume invisible.
                isActuallyVisible = false;
            }
            Menu.SetChecked("Edit/Play Mode/Show Fast Play Button", isActuallyVisible);
            return true; // The item is always active/clickable
        }

        /// <summary>
        /// Checks on startup whether the overlay should be shown, based on the user's
        /// previously recorded preference. If no preference exists yet, prompts the user
        /// once and records their answer for future sessions and projects.
        /// </summary>
        static void CheckOverlayOnStartup()
        {
            Overlay myOverlay = GetFastPlayOverlay();

            if (myOverlay == null)
            {
                LogOverlayWarningIfNeeded();
                return;
            }

            // Migrate old per-machine EditorPrefs to the new project-level settings file.
            // If migration happened, the settings file now exists, so fall through to the normal path.
            TryMigrateLegacyPrefs(myOverlay);

            FastPlaySettings settings = LoadSettings();

            if (settings != null)
            {
                bool hasApplied = EditorPrefs.GetBool(HasAppliedSettingsKey, false);

                if (!hasApplied)
                {
                    // First time on this machine: apply the project-level preference.
                    EditorPrefs.SetBool(HasAppliedSettingsKey, true);
                    if (settings.showButton && !myOverlay.displayed) myOverlay.displayed = true;
                }
                else if (myOverlay.displayed != settings.showButton)
                {
                    // The overlay state drifted from the settings (e.g. the user hid the
                    // button via the toolbar right-click menu, which bypasses our code).
                    // Update the settings file to match the actual overlay state.
                    SaveSettings(new FastPlaySettings { showButton = myOverlay.displayed });
                    RecordCrossProjectChoice(myOverlay.displayed);
                }

                return;
            }

            // No settings file yet. If the overlay is already visible, record that and move on.
            if (myOverlay.displayed)
            {
                SaveSettings(new FastPlaySettings { showButton = true });
                RecordCrossProjectChoice(true);
                return;
            }

            // Honour the user's preference from another project, if they previously opted in.
            if (EditorPrefs.GetBool(CROSS_PROJECT_SHOW_BUTTON_KEY, false))
            {
                myOverlay.displayed = true;
                SaveSettings(new FastPlaySettings { showButton = true });
                return;
            }

            // No stored preference: ask the user for their choice.
            bool shouldShow = EditorUtility.DisplayDialog(SHOW_TITLE, SHOW_MESSAGE, YES_RECOMMENDED, NO);
            if (shouldShow) myOverlay.displayed = true;
            SaveSettings(new FastPlaySettings { showButton = shouldShow });
            RecordCrossProjectChoice(shouldShow);
        }

        /// <summary>
        /// Retrieves the Fast Play overlay using reflection.
        /// </summary>
        /// <returns>The Fast Play overlay if found; otherwise, null.</returns>
        static Overlay GetFastPlayOverlay()
        {
            // The following code uses reflection to access Unity's internal API to find the overlay.
            // However, since Unity's internal API may change, it is wrapped in try-catch blocks for safety.
            // PROTECTION #1 : Try-Catch
            try
            {
                if (_isReflectionUnavailableTest) throw new System.Exception("Simulated reflection failure");

                Assembly editorAssembly = typeof(Editor).Assembly;

                // PROTECTION #2: Check for null types
                System.Type mainToolbarWindowType = editorAssembly.GetType("UnityEditor.MainToolbarWindow");
                if (mainToolbarWindowType == null) return null; // Exit if type not found

                UnityEngine.Object[] windows = Resources.FindObjectsOfTypeAll(mainToolbarWindowType);
                if (windows == null || windows.Length == 0) return null; // Exit if no windows found

                var window = windows[0];

                // Search for the OverlayCanvas property:
                PropertyInfo overlayCanvasProp = mainToolbarWindowType.GetProperty("overlayCanvas", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (overlayCanvasProp == null) return null; // Exit if property not found

                // Calling GetValue on the property to get the OverlayCanvas instance:
                OverlayCanvas canvas = overlayCanvasProp.GetValue(window) as OverlayCanvas;

                if (canvas == null) return null; // Exit if canvas is null

                // Searching in the overlays list for our ELEMENT_ID
                // Using ?. to avoid an error if the 'overlays' list is null
                return canvas.overlays?.FirstOrDefault(o => o.id == ELEMENT_ID);
            }
            catch (System.Exception)
            {
                // Unable to access Overlays via reflection. Unity's API probably changed.
                LogOverlayWarningIfNeeded();
                return null;
            }
        }

        /// <summary>
        /// Logs a warning message stating that the overlay visibility cannot be managed automatically.
        /// Ensures the message is only logged once.
        /// </summary>
        static void LogOverlayWarningIfNeeded()
        {
            if (!_didWarnAboutOverlay) 
            {
                _didWarnAboutOverlay = true;
                Debug.Log(CATCH_MESSAGE);
            }
        }

        /// <summary>
        /// Clears the stored preference for hiding the Fast Play button. [FOR TESTING PURPOSES ONLY]
        /// </summary>
        static void ClearFastPlayButtonPrefs()
        {
            _didWarnAboutOverlay = false;
            if (System.IO.File.Exists(SETTINGS_PATH)) System.IO.File.Delete(SETTINGS_PATH);
            EditorPrefs.DeleteKey(HasModifiedSettingsKey);
            EditorPrefs.DeleteKey(OriginalOptionsKey);
            EditorPrefs.DeleteKey(CROSS_PROJECT_SHOW_BUTTON_KEY);
            EditorPrefs.DeleteKey(HasAppliedSettingsKey);
            Debug.Log("Cleared Fast Play button preferences.");
        }
    }
#else

[InitializeOnLoad]
public class FastPlay
{
    static FastPlay()
    {
        string icon = "<color=#ff822d>◆</color>"; // Previous Unity versions did not support emojis in console
        Debug.LogWarning($"{icon} Fast Play {icon} requires Unity 6000.3 or newer to function properly.\n <size=10>** Please update your Unity version or consider using my older similar tool called <b>FastPlayToggler</b>: <color=white><a href=\"https://github.com/JonathanTremblay/UnityFastPlayToggler\">https://github.com/JonathanTremblay/UnityFastPlayToggler</a></color> **</size>");
    }
}
#endif
}
#endif