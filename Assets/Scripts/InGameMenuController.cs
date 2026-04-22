using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class InGameMenuController : MonoBehaviour
{
    [Header("Input")]
    public KeyCode ToggleMenuKey = KeyCode.Escape;

    [Header("Root")]
    public GameObject MenuRoot;
    public GameObject MainPage;
    public GameObject SettingsPage;
    public GameObject ControlsPage;

    [Header("Settings")]
    public Slider MasterVolumeSlider;
    public TMP_Text MasterVolumeValueText;
    [Range(0f, 1f)] public float DefaultMasterVolume = 1f;

    [Header("Controls")]
    public TMP_Text ControlsText;
    [TextArea(2, 8)]
    public string AdditionalControls = "Build Place: Left Mouse\nCancel Move: Right Mouse or Escape";

    [Header("Game Integration")]
    public InventoryUI InventoryUI;
    public GridSystem GridSystem;

    [Header("Behavior")]
    public bool PauseTimeWhenOpen = true;
    public bool UnlockCursorWhenOpen = true;
    public MonoBehaviour[] DisableWhileMenuOpen;

    [Header("Scene Actions")]
    public string MainMenuSceneName = "MainMenu";

    private const string MasterVolumePrefKey = "settings.masterVolume";

    private bool isOpen;
    private float previousTimeScale = 1f;
    private CursorLockMode previousCursorLockMode;
    private bool previousCursorVisible;

    private readonly List<DisableState> disabledStates = new List<DisableState>();

    private struct DisableState
    {
        public MonoBehaviour Behaviour;
        public bool WasEnabled;
    }

    private void Awake()
    {
        previousCursorLockMode = Cursor.lockState;
        previousCursorVisible = Cursor.visible;

        SetSavedVolume();
        HookVolumeSlider();
        RefreshControlsText();

        if (MenuRoot != null)
        {
            MenuRoot.SetActive(false);
        }

        SetPages(showMain: true, showSettings: false, showControls: false);
    }

    private void OnDestroy()
    {
        if (isOpen)
        {
            ApplyMenuState(false);
        }

        if (MasterVolumeSlider != null)
        {
            MasterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(ToggleMenuKey))
        {
            SetMenuOpen(!isOpen);
        }
    }

    public void SetMenuOpen(bool open)
    {
        if (isOpen == open)
        {
            return;
        }

        isOpen = open;
        ApplyMenuState(open);
    }

    public void ToggleMenu()
    {
        SetMenuOpen(!isOpen);
    }

    public void OpenMenu()
    {
        SetMenuOpen(true);
    }

    public void CloseMenu()
    {
        SetMenuOpen(false);
    }

    public void ShowMainPage()
    {
        SetPages(showMain: true, showSettings: false, showControls: false);
    }

    public void ShowSettingsPage()
    {
        SetPages(showMain: false, showSettings: true, showControls: false);
    }

    public void ShowControlsPage()
    {
        RefreshControlsText();
        SetPages(showMain: false, showSettings: false, showControls: true);
    }

    public void OnResumePressed()
    {
        CloseMenu();
    }

    public void OnOpenSettingsPressed()
    {
        ShowSettingsPage();
    }

    public void OnOpenControlsPressed()
    {
        ShowControlsPage();
    }

    public void OnBackPressed()
    {
        ShowMainPage();
    }

    public void OnResetGamePressed()
    {
        PrepareForSceneChange();
        var active = SceneManager.GetActiveScene();
        SceneManager.LoadScene(active.name);
    }

    public void OnMainMenuPressed()
    {
        if (string.IsNullOrWhiteSpace(MainMenuSceneName))
        {
            Debug.LogWarning("MainMenuSceneName is empty. Assign a valid scene name.");
            return;
        }

        PrepareForSceneChange();
        SceneManager.LoadScene(MainMenuSceneName);
    }

    public void OnQuitPressed()
    {
        PrepareForSceneChange();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void RefreshControlsText()
    {
        if (ControlsText == null)
        {
            return;
        }

        var builder = new StringBuilder(256);
        builder.AppendLine("Controls");
        builder.AppendLine();
        builder.AppendLine("Pause Menu: " + ToggleMenuKey);

        if (InventoryUI != null)
        {
            builder.AppendLine("Inventory / Build Toggle: " + InventoryUI.ToggleKey);
        }

        if (GridSystem != null)
        {
            if (GridSystem.RotateWithQAndE)
            {
                builder.AppendLine("Rotate Build: Q / E");
            }

            if (GridSystem.RotateWithR)
            {
                builder.AppendLine("Rotate Build Step: R");
            }
        }

        if (!string.IsNullOrWhiteSpace(AdditionalControls))
        {
            builder.AppendLine(AdditionalControls.Trim());
        }

        ControlsText.text = builder.ToString();
    }

    private void SetPages(bool showMain, bool showSettings, bool showControls)
    {
        if (MainPage != null)
        {
            MainPage.SetActive(showMain);
        }

        if (SettingsPage != null)
        {
            SettingsPage.SetActive(showSettings);
        }

        if (ControlsPage != null)
        {
            ControlsPage.SetActive(showControls);
        }
    }

    private void ApplyMenuState(bool open)
    {
        if (MenuRoot != null)
        {
            MenuRoot.SetActive(open);
        }

        if (open)
        {
            ShowMainPage();
        }

        if (PauseTimeWhenOpen)
        {
            if (open)
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale = previousTimeScale;
            }
        }

        if (UnlockCursorWhenOpen)
        {
            if (open)
            {
                previousCursorLockMode = Cursor.lockState;
                previousCursorVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = previousCursorLockMode;
                Cursor.visible = previousCursorVisible;
            }
        }

        ApplyGameplayDisables(open);
    }

    private void ApplyGameplayDisables(bool open)
    {
        if (DisableWhileMenuOpen == null || DisableWhileMenuOpen.Length == 0)
        {
            return;
        }

        if (open)
        {
            disabledStates.Clear();
            for (int i = 0; i < DisableWhileMenuOpen.Length; i++)
            {
                var behaviour = DisableWhileMenuOpen[i];
                if (behaviour == null || behaviour == this)
                {
                    continue;
                }

                disabledStates.Add(new DisableState
                {
                    Behaviour = behaviour,
                    WasEnabled = behaviour.enabled
                });

                behaviour.enabled = false;
            }

            return;
        }

        for (int i = 0; i < disabledStates.Count; i++)
        {
            var state = disabledStates[i];
            if (state.Behaviour != null)
            {
                state.Behaviour.enabled = state.WasEnabled;
            }
        }

        disabledStates.Clear();
    }

    private void HookVolumeSlider()
    {
        if (MasterVolumeSlider == null)
        {
            return;
        }

        MasterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
        MasterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);

        var current = GetSavedVolume();
        MasterVolumeSlider.SetValueWithoutNotify(current);
        UpdateVolumeLabel(current);
    }

    private void OnMasterVolumeChanged(float value)
    {
        value = Mathf.Clamp01(value);
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(MasterVolumePrefKey, value);
        PlayerPrefs.Save();
        UpdateVolumeLabel(value);
    }

    private void UpdateVolumeLabel(float value)
    {
        if (MasterVolumeValueText != null)
        {
            MasterVolumeValueText.text = Mathf.RoundToInt(value * 100f) + "%";
        }
    }

    private void SetSavedVolume()
    {
        AudioListener.volume = GetSavedVolume();
    }

    private float GetSavedVolume()
    {
        var fallback = Mathf.Clamp01(DefaultMasterVolume);
        return PlayerPrefs.GetFloat(MasterVolumePrefKey, fallback);
    }

    private void PrepareForSceneChange()
    {
        if (PauseTimeWhenOpen)
        {
            Time.timeScale = 1f;
        }

        if (UnlockCursorWhenOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        isOpen = false;
        if (MenuRoot != null)
        {
            MenuRoot.SetActive(false);
        }

        ApplyGameplayDisables(false);
    }
}
