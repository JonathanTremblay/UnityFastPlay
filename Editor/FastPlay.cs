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
        const string VERSION = "Version 1.0.0 (2026-01)";
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
        static EnterPlayModeOptions _lastPlayModeOptions = EnterPlayModeOptions.None;
        static bool _isFastPlayMode = false;
        static bool _didWarnAboutOverlay = false;

        // The 2 following variables are for testing purposes only:
        static bool _shouldClearPrefsForTesting = false;
        static bool _isReflectionUnavailableTest = false;

        /// <summary>
        /// Static constructor to subscribe to Play Mode state changes.
        /// </summary>
        static FastPlay()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            if (_shouldClearPrefsForTesting) ClearFastPlayButtonPrefs();
        }

        /// <summary>
        /// Logs the initial Fast Play message when the editor loads.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void LogFastPlayMessage()
        {
            if (_isFastPlayMode) Debug.Log(INITIAL_MESSAGE);
        }

        /// <summary>
        /// Handles Play Mode state changes to restore settings or reset state.
        /// </summary>
        /// <param name="state">The new Play Mode state.</param>
        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && _isFastPlayMode) RestorePlayModeOptions();
            else if (state == PlayModeStateChange.ExitingPlayMode) _isFastPlayMode = false; // Reset state on exit
            MainToolbar.Refresh(ELEMENT_ID); // Refresh the toolbar button state
        }

        /// <summary>
        /// Creates the Fast Play button in the main toolbar.
        /// </summary>
        /// <returns>An instance of the MainToolbarElement class representing the Fast Play button.</returns>
        [MainToolbarElement(ELEMENT_ID, defaultDockPosition = MainToolbarDockPosition.Middle, defaultDockIndex = 1)]
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
                _lastPlayModeOptions = EditorSettings.enterPlayModeOptions;
                bool isAlreadyFastPlay = _lastPlayModeOptions == (EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload);
                if (isAlreadyFastPlay)
                {
                    bool shouldReset = EditorUtility.DisplayDialog(WARNING_TITLE, WARNING_MESSAGE, YES_RECOMMENDED, NO);
                    if (shouldReset) _lastPlayModeOptions = EnterPlayModeOptions.None;
                }
                EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;

                EditorApplication.ExecuteMenuItem("Edit/Play Mode/Play"); // Start playing
            }
            // Disable the next line if you don't want the button to also act as a stop button
            else EditorApplication.isPlaying = false;
        }

        /// <summary>
        /// Restores the last saved Play Mode options (as soon as Play Mode is entered).
        /// </summary>
        static void RestorePlayModeOptions() => EditorSettings.enterPlayModeOptions = _lastPlayModeOptions;

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
        /// Tracks whether the user has already been prompted to show the Fast Play button.
        /// </summary>
        static string HasCheckedInitialVisibilityKey => $"{Application.productName}_{ELEMENT_ID}_HasCheckedInitialVisibility";

        /// <summary>
        /// Initially false; set to true the first time we show (or intentionally skip) the prompt.
        /// </summary>
        static bool _hasCheckedInitialVisibility
        {
            get => EditorPrefs.GetBool(HasCheckedInitialVisibilityKey, false);
            set => EditorPrefs.SetBool(HasCheckedInitialVisibilityKey, value);
        }

        /// <summary>
        /// Menu item to toggle the Fast Play button visibility.
        /// </summary>
        [MenuItem("Edit/Play Mode/Show Fast Play Button", false, 1101)] // Add a menu item to toggle the Fast Play button visibility
        static void ToggleFastPlayButton()
        {
            _didWarnAboutOverlay = false;
            // 1. Determine current state based on actual overlay if possible
            Overlay myOverlay = GetFastPlayOverlay();
            bool isCurrentlyVisible;
            if (myOverlay != null) isCurrentlyVisible = myOverlay.displayed;
            else isCurrentlyVisible = false; // If overlay can't be queried, assume invisible

            // 2. Toggle the state
            bool shouldShow = !isCurrentlyVisible;
            SetOverlayVisibility(shouldShow);
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
        /// Checks on startup if the overlay should be shown, and prompts the user if it is hidden.
        /// </summary>
        static void CheckOverlayOnStartup()
        {
            if (_hasCheckedInitialVisibility) return;
            _hasCheckedInitialVisibility = true;

            Overlay myOverlay = GetFastPlayOverlay();

            if (myOverlay == null)
            {
                LogOverlayWarningIfNeeded();
                return;
            }

            if (!myOverlay.displayed)
            {
                // First time we detect it's hidden: ask once.
                bool shouldShowBtn = EditorUtility.DisplayDialog(SHOW_TITLE, SHOW_MESSAGE, YES_RECOMMENDED, NO);
                if (shouldShowBtn) myOverlay.displayed = true;
            }
        }

        /// <summary>
        /// Sets the visibility of the Fast Play overlay.
        /// </summary>
        /// <param name="shouldBeVisible">True to show the overlay, false to hide it.</param>
        static void SetOverlayVisibility(bool shouldBeVisible)
        {
            Overlay myOverlay = GetFastPlayOverlay();
            if (myOverlay != null) myOverlay.displayed = shouldBeVisible;
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
            EditorPrefs.DeleteKey(HasCheckedInitialVisibilityKey);
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