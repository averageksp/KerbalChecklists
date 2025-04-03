using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using KSP;
using KSP.UI.Screens;

[KSPAddon(KSPAddon.Startup.FlightEditorAndKSC, false)]
public class KerbalChecklists : MonoBehaviour
{
    private Rect windowRect = new Rect(100, 100, 350, 650);
    private bool showGUI = false;
    private string newItemText = "";
    private List<ChecklistItem> checklist = new List<ChecklistItem>();
    private string currentChecklistName = "";
    private Vector2 checklistScrollPos;
    private Vector2 availableScrollPos;
    private string baseDir;
    private string activeChecklistPath;
    private string savedChecklistsDir;
    private string settingsPath;
    private ApplicationLauncherButton appButton;
    private bool showNewChecklistDialog = false;
    private bool showSaveChecklistDialog = false;
    private bool showDeleteConfirmationDialog = false;
    private bool showCharWarningDialog = false;
    private string checklistToDelete = "";
    private bool deleteNeverAskAgainToggle = false;
    private bool isNewChecklistOperation = false;
    private bool neverShowCharWarning = false;
    private bool charWarningDismissed = false;
    private bool isDirty = false;
    private float autoSaveTimer = 0f;
    private const float autoSaveInterval = 2.0f;

    void Start()
    {
        string modFolder = "";
        string[] possibleFolders = { "KerbalChecklists-1.0/KerbalChecklists", "KerbalChecklists", "KerbalChecklists-1.0" };
        foreach (string folder in possibleFolders)
        {
            string testPath = Path.Combine(KSPUtil.ApplicationRootPath, "GameData", folder);
            if (Directory.Exists(testPath))
            {
                modFolder = testPath;
                break;
            }
        }
        if (string.IsNullOrEmpty(modFolder))
            modFolder = Path.Combine(KSPUtil.ApplicationRootPath, "GameData/KerbalChecklists");
        baseDir = modFolder;
        activeChecklistPath = Path.Combine(baseDir, "activeChecklist.txt");
        savedChecklistsDir = Path.Combine(baseDir, "SavedChecklists");
        Directory.CreateDirectory(savedChecklistsDir);
        settingsPath = Path.Combine(baseDir, "settings.txt");
        LoadSettings();
        LoadActiveChecklist();

        string[] possibleIconPaths = {
            Path.Combine(Path.GetFileName(baseDir), "Textures/icon"),
            Path.Combine(Path.GetFileName(baseDir) + "/Textures/icon"),
            "KerbalChecklists-1.0/KerbalChecklists/Textures/icon",
            "KerbalChecklists/Textures/icon",
            "KerbalChecklists-1.0/Textures/icon"
        };
        Texture2D icon = null;
        foreach (string path in possibleIconPaths)
        {
            icon = GameDatabase.Instance.GetTexture(path, false);
            if (icon != null)
                break;
        }
        if (icon == null)
            UnityEngine.Debug.LogError("[KerbalChecklists] Could not find icon.png at expected paths.");
        appButton = ApplicationLauncher.Instance.AddModApplication(
            OnToggleGUI,
            OnToggleGUI,
            null, null, null, null,
            ApplicationLauncher.AppScenes.FLIGHT |
            ApplicationLauncher.AppScenes.SPACECENTER |
            ApplicationLauncher.AppScenes.VAB |
            ApplicationLauncher.AppScenes.SPH,
            icon);
    }

    private void LoadSettings()
    {
        if (File.Exists(settingsPath))
        {
            try
            {
                string[] lines = File.ReadAllLines(settingsPath);
                foreach (string line in lines)
                {
                    if (line.StartsWith("NeverAskDelete="))
                        bool.TryParse(line.Substring("NeverAskDelete=".Length), out deleteNeverAskAgainToggle);
                    if (line.StartsWith("NeverShowCharWarning="))
                        bool.TryParse(line.Substring("NeverShowCharWarning=".Length), out neverShowCharWarning);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[KerbalChecklists] Error loading settings: " + ex.Message);
            }
        }
    }

    private void SaveSettings()
    {
        try
        {
            using (StreamWriter writer = new StreamWriter(settingsPath))
            {
                writer.WriteLine("NeverAskDelete=" + deleteNeverAskAgainToggle);
                writer.WriteLine("NeverShowCharWarning=" + neverShowCharWarning);
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError("[KerbalChecklists] Error saving settings: " + ex.Message);
        }
    }

    private void OnToggleGUI() { showGUI = !showGUI; }

    void OnGUI()
    {
        GUI.skin = HighLogic.Skin;
        if (showGUI)
            windowRect = GUILayout.Window("KerbalChecklistsWindow".GetHashCode(), windowRect, DrawWindow, "Kerbal Checklists", GUILayout.Width(350), GUILayout.Height(650));
        if (showNewChecklistDialog)
            DrawNewChecklistDialog();
        if (showSaveChecklistDialog)
            DrawSaveChecklistDialog();
        if (showDeleteConfirmationDialog)
            DrawDeleteConfirmationDialog();
        if (showCharWarningDialog)
            DrawCharWarningDialog();
    }

    void DrawWindow(int windowID)
    {
        GUILayout.BeginVertical();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("New Checklist", GUILayout.Height(30)))
        {
            isNewChecklistOperation = true;
            showNewChecklistDialog = true;
        }
        if (GUILayout.Button("Save Checklist", GUILayout.Height(30)))
        {
            isNewChecklistOperation = false;
            showNewChecklistDialog = true;
        }
        if (GUILayout.Button("Refresh", GUILayout.Height(30)))
            availableScrollPos = Vector2.zero;
        GUILayout.EndHorizontal();
        GUILayout.Space(5);
        GUILayout.Label("Add New Checklist Item:");
        GUILayout.BeginHorizontal();
        newItemText = GUILayout.TextField(newItemText, GUILayout.Width(250));
        if (GUILayout.Button("Add", GUILayout.Width(80), GUILayout.Height(24)))
        {
            if (!string.IsNullOrEmpty(newItemText))
            {
                checklist.Add(new ChecklistItem { Text = newItemText, Completed = false });
                newItemText = "";
                MarkDirty();
                charWarningDismissed = false;
            }
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(5);
        GUILayout.Label("Checklist Items:");
        checklistScrollPos = GUILayout.BeginScrollView(checklistScrollPos, GUILayout.Height(250));
        for (int i = 0; i < checklist.Count; i++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(5);
            bool prev = checklist[i].Completed;
            bool curr = GUILayout.Toggle(prev, "", GUILayout.Width(20));
            if (curr != prev)
            {
                checklist[i].Completed = curr;
                MarkDirty();
            }
            GUILayout.Space(5);
            GUILayout.Label(checklist[i].Text, GUILayout.Width(200));
            if (GUILayout.Button("Remove", GUILayout.Width(80)))
            {
                checklist.RemoveAt(i);
                MarkDirty();
                i--;
                continue;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(2);
        }
        GUILayout.EndScrollView();
        int completedCount = 0;
        foreach (var item in checklist)
            if (item.Completed) completedCount++;
        GUILayout.Label("Completed: " + completedCount + " / " + checklist.Count);
        GUILayout.Space(5);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Current Checklist Name:", GUILayout.Width(150));
        currentChecklistName = GUILayout.TextField(currentChecklistName, GUILayout.Width(200));
        GUILayout.EndHorizontal();
        GUILayout.Space(5);
        GUILayout.Label("Available Checklists:");
        availableScrollPos = GUILayout.BeginScrollView(availableScrollPos, GUILayout.Height(120));
        string[] savedFiles = Directory.GetFiles(savedChecklistsDir, "*.txt");
        foreach (string file in savedFiles)
        {
            GUILayout.BeginHorizontal();
            string fileName = Path.GetFileNameWithoutExtension(file);
            if (GUILayout.Button(fileName, GUILayout.Width(250)))
                LoadChecklistFromFile(fileName);
            if (GUILayout.Button("Remove", GUILayout.Width(80)))
            {
                if (deleteNeverAskAgainToggle)
                    DeleteChecklist(fileName);
                else
                {
                    checklistToDelete = fileName;
                    showDeleteConfirmationDialog = true;
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(2);
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();
        GUI.DragWindow();
    }

    void Update()
    {
        if (!neverShowCharWarning && !showCharWarningDialog && !charWarningDismissed && newItemText.Length > 50)
            showCharWarningDialog = true;
        if (newItemText.Length <= 50)
            charWarningDismissed = false;
        if (isDirty)
        {
            autoSaveTimer += Time.deltaTime;
            if (autoSaveTimer >= autoSaveInterval)
            {
                SaveActiveChecklist();
                isDirty = false;
                autoSaveTimer = 0f;
            }
        }
        else autoSaveTimer = 0f;
    }

    private void MarkDirty() { isDirty = true; }

    void DrawNewChecklistDialog()
    {
        Rect dialogRect = new Rect(Screen.width / 2 - 150, Screen.height / 2 - 60, 300, 120);
        GUILayout.Window("NewChecklistDialog".GetHashCode(), dialogRect, id =>
        {
            GUILayout.Label("Save current checklist before continuing?");
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Yes", GUILayout.Width(80)))
            {
                showNewChecklistDialog = false;
                showSaveChecklistDialog = true;
            }
            if (GUILayout.Button("No", GUILayout.Width(80)))
            {
                showNewChecklistDialog = false;
                if (isNewChecklistOperation)
                {
                    checklist.Clear();
                    currentChecklistName = "";
                    MarkDirty();
                }
            }
            GUILayout.EndHorizontal();
        }, "Confirm Save", GUILayout.Width(300), GUILayout.Height(120));
    }

    void DrawSaveChecklistDialog()
    {
        Rect dialogRect = new Rect(Screen.width / 2 - 150, Screen.height / 2 - 70, 300, 140);
        GUILayout.Window("SaveChecklistDialog".GetHashCode(), dialogRect, id =>
        {
            GUILayout.Label("Enter name for current checklist:");
            string nameInput = GUILayout.TextField(currentChecklistName, GUILayout.Width(250));
            currentChecklistName = nameInput;
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save", GUILayout.Width(80)))
            {
                if (!string.IsNullOrEmpty(currentChecklistName))
                {
                    SaveChecklist(currentChecklistName);
                    showSaveChecklistDialog = false;
                    if (isNewChecklistOperation)
                    {
                        checklist.Clear();
                        currentChecklistName = "";
                        MarkDirty();
                    }
                }
            }
            if (GUILayout.Button("Cancel", GUILayout.Width(80)))
                showSaveChecklistDialog = false;
            GUILayout.EndHorizontal();
        }, "Save Checklist", GUILayout.Width(300), GUILayout.Height(140));
    }

    void DrawDeleteConfirmationDialog()
    {
        Rect dialogRect = new Rect(Screen.width / 2 - 150, Screen.height / 2 - 80, 300, 160);
        GUILayout.Window("DeleteConfirmationDialog".GetHashCode(), dialogRect, id =>
        {
            GUILayout.Label("Are you sure you want to delete:");
            GUILayout.Label(checklistToDelete);
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            bool newVal = GUILayout.Toggle(deleteNeverAskAgainToggle, "Never Ask Again", GUILayout.Width(140));
            if (newVal != deleteNeverAskAgainToggle)
            {
                deleteNeverAskAgainToggle = newVal;
                SaveSettings();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Yes", GUILayout.Width(80)))
            {
                DeleteChecklist(checklistToDelete);
                showDeleteConfirmationDialog = false;
            }
            if (GUILayout.Button("No", GUILayout.Width(80)))
                showDeleteConfirmationDialog = false;
            GUILayout.EndHorizontal();
        }, "Delete Checklist", GUILayout.Width(300), GUILayout.Height(160));
    }

    void DrawCharWarningDialog()
    {
        Rect dialogRect = new Rect(Screen.width / 2 - 175, Screen.height / 2 - 70, 350, 140);
        GUILayout.Window("CharWarningDialog".GetHashCode(), dialogRect, id =>
        {
            GUILayout.Label("Warning: You can continue, but you already have a lot of characters.");
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            bool newVal = GUILayout.Toggle(neverShowCharWarning, "Never show again", GUILayout.Width(140));
            if (newVal != neverShowCharWarning)
            {
                neverShowCharWarning = newVal;
                SaveSettings();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("OK", GUILayout.Width(80)))
            {
                showCharWarningDialog = false;
                charWarningDismissed = true;
            }
            GUILayout.EndHorizontal();
        }, "Character Limit Warning", GUILayout.Width(350), GUILayout.Height(140));
    }

    void OnDestroy()
    {
        if (ApplicationLauncher.Instance != null && appButton != null)
            ApplicationLauncher.Instance.RemoveModApplication(appButton);
        SaveActiveChecklist();
    }

    void SaveActiveChecklist()
    {
        try
        {
            using (StreamWriter writer = new StreamWriter(activeChecklistPath))
            {
                foreach (var item in checklist)
                    writer.WriteLine(item.Completed + "|" + item.Text);
            }
            UnityEngine.Debug.Log("[KerbalChecklists] Active checklist saved.");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError("[KerbalChecklists] Error saving active checklist: " + ex.Message);
        }
    }

    void LoadActiveChecklist()
    {
        checklist.Clear();
        currentChecklistName = "";
        if (File.Exists(activeChecklistPath))
        {
            try
            {
                using (StreamReader reader = new StreamReader(activeChecklistPath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        string[] parts = line.Split('|');
                        if (parts.Length == 2)
                            checklist.Add(new ChecklistItem { Completed = bool.Parse(parts[0]), Text = parts[1] });
                    }
                }
                UnityEngine.Debug.Log("[KerbalChecklists] Active checklist loaded.");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[KerbalChecklists] Error loading active checklist: " + ex.Message);
            }
        }
        else
            UnityEngine.Debug.Log("[KerbalChecklists] No active checklist found, starting new.");
    }

    void SaveChecklist(string checklistName)
    {
        string checklistPath = Path.Combine(savedChecklistsDir, checklistName + ".txt");
        try
        {
            using (StreamWriter writer = new StreamWriter(checklistPath))
            {
                foreach (var item in checklist)
                    writer.WriteLine(item.Completed + "|" + item.Text);
            }
            UnityEngine.Debug.Log("[KerbalChecklists] Checklist '" + checklistName + "' saved.");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError("[KerbalChecklists] Error saving checklist '" + checklistName + "': " + ex.Message);
        }
    }

    void LoadChecklistFromFile(string checklistName)
    {
        string checklistPath = Path.Combine(savedChecklistsDir, checklistName + ".txt");
        if (File.Exists(checklistPath))
        {
            checklist.Clear();
            try
            {
                using (StreamReader reader = new StreamReader(checklistPath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        string[] parts = line.Split('|');
                        if (parts.Length == 2)
                            checklist.Add(new ChecklistItem { Completed = bool.Parse(parts[0]), Text = parts[1] });
                    }
                }
                currentChecklistName = checklistName;
                UnityEngine.Debug.Log("[KerbalChecklists] Checklist '" + checklistName + "' loaded.");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[KerbalChecklists] Error loading checklist '" + checklistName + "': " + ex.Message);
            }
        }
    }

    void DeleteChecklist(string checklistName)
    {
        string checklistPath = Path.Combine(savedChecklistsDir, checklistName + ".txt");
        if (File.Exists(checklistPath))
        {
            try
            {
                File.Delete(checklistPath);
                UnityEngine.Debug.Log("[KerbalChecklists] Checklist '" + checklistName + "' deleted.");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[KerbalChecklists] Error deleting checklist '" + checklistName + "': " + ex.Message);
            }
        }
    }
}

public class ChecklistItem
{
    public bool Completed;
    public string Text;
}
