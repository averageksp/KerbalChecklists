using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using KSP;
using KSP.UI.Screens;
using ClickThroughFix;

public class ModConfig
{
    public string ModName;
    public string Author;
    public bool AddToKerbalChecklists;
}

public class ChecklistItem
{
    public bool Completed;
    public string Text;
}

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

    private string defaultFolder;
    private string defaultSavedFolder;
    private Dictionary<string, ModConfig> modConfigs = new Dictionary<string, ModConfig>();
    private const string ModConfigFileName = "KerbalChecklistsConfig.txt";
    private const string ModChecklistsSubfolder = "Checklists";
    private string currentChecklistFolder = "";
    private string currentModFolder = "";

    private string activeChecklistPath;
    private string settingsPath;
    private ApplicationLauncherButton appButton;

    private bool showConfirmNewChecklistDialog = false;
    private bool showSaveBeforeNewChecklistDialog = false;
    private bool showNewChecklistDialog = false;
    private bool showSaveChecklistDialog = false;
    private bool showModSaveChecklistDialog = false;
    private bool showFileDeleteDialog = false;
    private bool showItemDeleteDialog = false;
    private string checklistToDelete = "";
    private bool deleteNeverAskAgainToggle = false;
    private int itemIndexToDelete = -1;
    private bool pendingNewChecklistAfterSave = false;
    private bool isNewChecklistOperation = false;
    private bool isDirty = false;
    private float autoSaveTimer = 0f;
    private const float autoSaveInterval = 2.0f;

    void Start()
    {
        defaultFolder = Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "KerbalChecklists");
        if (!Directory.Exists(defaultFolder))
            Directory.CreateDirectory(defaultFolder);
        defaultSavedFolder = Path.Combine(defaultFolder, "SavedChecklists");
        if (!Directory.Exists(defaultSavedFolder))
            Directory.CreateDirectory(defaultSavedFolder);
        currentChecklistFolder = defaultSavedFolder;
        currentModFolder = "";
        activeChecklistPath = Path.Combine(defaultFolder, "activeChecklist.txt");
        settingsPath = Path.Combine(defaultFolder, "settings.txt");
        if (!File.Exists(settingsPath) || new FileInfo(settingsPath).Length == 0)
        {
            deleteNeverAskAgainToggle = false;
            SaveSettings();
        }
        string gameDataPath = Path.Combine(KSPUtil.ApplicationRootPath, "GameData");
        foreach (string dir in Directory.GetDirectories(gameDataPath))
        {
            if (dir.Equals(defaultFolder, StringComparison.InvariantCultureIgnoreCase))
                continue;
            string configPath = Path.Combine(dir, ModConfigFileName);
            if (File.Exists(configPath))
            {
                ModConfig config = new ModConfig();
                try
                {
                    foreach (string line in File.ReadAllLines(configPath))
                    {
                        if (line.StartsWith("ModName="))
                            config.ModName = line.Substring("ModName=".Length).Trim();
                        else if (line.StartsWith("Author="))
                            config.Author = line.Substring("Author=".Length).Trim();
                        else if (line.StartsWith("AddToKerbalChecklists="))
                            config.AddToKerbalChecklists = line.Substring("AddToKerbalChecklists=".Length).Trim().ToLower() == "true";
                    }
                    if (config.AddToKerbalChecklists)
                        modConfigs[dir] = config;
                }
                catch { }
            }
        }
        LoadSettings();
        LoadActiveChecklist();
        string[] possibleIconPaths = {
            Path.Combine(Path.GetFileName(defaultFolder), "Textures/icon"),
            Path.Combine(Path.GetFileName(defaultFolder) + "/Textures/icon"),
            "KerbalChecklists-1.2/KerbalChecklists/Textures/icon",
            "KerbalChecklists/Textures/icon",
            "KerbalChecklists-1.2/Textures/icon"
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
            null,
            null,
            null,
            null,
            ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.SPACECENTER |
            ApplicationLauncher.AppScenes.VAB | ApplicationLauncher.AppScenes.SPH,
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
            windowRect = ClickThruBlocker.GUILayoutWindow("KerbalChecklistsWindow".GetHashCode(), windowRect, DrawWindow, "Kerbal Checklists", GUILayout.Width(350), GUILayout.Height(650));
        if (showConfirmNewChecklistDialog)
            DrawConfirmNewChecklistDialog();
        if (showSaveBeforeNewChecklistDialog)
            DrawSaveBeforeNewChecklistDialog();
        if (showSaveChecklistDialog)
            DrawSaveChecklistDialog();
        if (showModSaveChecklistDialog)
            DrawModSaveChecklistDialog();
        if (showFileDeleteDialog)
            DrawFileDeleteConfirmationDialog();
        if (showItemDeleteDialog)
            DrawItemDeleteConfirmationDialog();
    }

    void DrawWindow(int windowID)
    {
        GUILayout.BeginVertical();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("New Checklist", GUILayout.Height(30)))
        {
            isNewChecklistOperation = true;
            if (currentChecklistFolder == defaultSavedFolder)
                showConfirmNewChecklistDialog = true;
            else
                ResetChecklist();
        }
        if (GUILayout.Button("Save Checklist", GUILayout.Height(30)))
        {
            if (currentChecklistFolder == defaultSavedFolder)
            {
                isNewChecklistOperation = false;
                showSaveChecklistDialog = true;
            }
            else
            {
                showModSaveChecklistDialog = true;
            }
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
            if (curr != prev) { checklist[i].Completed = curr; MarkDirty(); }
            GUILayout.Space(5);
            GUILayout.Label(checklist[i].Text, GUILayout.Width(200));
            if (GUILayout.Button("Remove", GUILayout.Width(80)))
            {
                if (currentChecklistFolder == defaultSavedFolder)
                {
                    checklist.RemoveAt(i);
                    MarkDirty();
                    i--;
                    continue;
                }
                else
                {
                    itemIndexToDelete = i;
                    showItemDeleteDialog = true;
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(2);
        }
        GUILayout.EndScrollView();
        int completedCount = 0;
        foreach (var item in checklist)
            if (item.Completed)
                completedCount++;
        GUILayout.Label("Completed: " + completedCount + " / " + checklist.Count);
        GUILayout.Space(5);
        if (!string.IsNullOrEmpty(currentChecklistFolder) && currentChecklistFolder != defaultSavedFolder)
        {
            if (modConfigs.ContainsKey(currentModFolder))
            {
                ModConfig config = modConfigs[currentModFolder];
                GUILayout.Label("Checklist Mod from: " + config.ModName);
                GUILayout.Label("Author: " + config.Author);
            }
            else
            {
                GUILayout.Label("Checklist Mod from: " + Path.GetFileName(currentChecklistFolder));
            }
        }
        GUILayout.BeginHorizontal();
        GUILayout.Label("Current Checklist Name:", GUILayout.Width(150));
        currentChecklistName = GUILayout.TextField(currentChecklistName, GUILayout.Width(200));
        GUILayout.EndHorizontal();
        GUILayout.Space(5);
        GUILayout.Label("Available Checklists:");
        availableScrollPos = GUILayout.BeginScrollView(availableScrollPos, GUILayout.Height(120));
        string[] defaultFiles = Directory.GetFiles(defaultSavedFolder, "*.txt");
        foreach (string file in defaultFiles)
        {
            string fileName = Path.GetFileNameWithoutExtension(file);
            if (ShouldIgnoreFile(fileName))
                continue;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(fileName, GUILayout.Width(250))) { currentChecklistFolder = defaultSavedFolder; currentModFolder = ""; LoadChecklistFromFile(fileName, defaultSavedFolder); }
            if (GUILayout.Button("Remove", GUILayout.Width(80)))
            {
                if (deleteNeverAskAgainToggle) { DeleteChecklist(fileName, defaultSavedFolder); }
                else { checklistToDelete = fileName; currentChecklistFolder = defaultSavedFolder; currentModFolder = ""; showFileDeleteDialog = true; }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(2);
        }
        foreach (KeyValuePair<string, ModConfig> kvp in modConfigs)
        {
            string modFolder = kvp.Key;
            string modChecklistsPath = Path.Combine(modFolder, ModChecklistsSubfolder);
            if (!Directory.Exists(modChecklistsPath))
                continue;
            string[] files = Directory.GetFiles(modChecklistsPath, "*.txt");
            foreach (string file in files)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                if (ShouldIgnoreFile(fileName))
                    continue;
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(fileName, GUILayout.Width(250))) { currentChecklistFolder = modChecklistsPath; currentModFolder = modFolder; LoadChecklistFromFile(fileName, modChecklistsPath); }
                if (GUILayout.Button("Remove", GUILayout.Width(80))) { checklistToDelete = fileName; currentChecklistFolder = modChecklistsPath; currentModFolder = modFolder; showFileDeleteDialog = true; }
                GUILayout.EndHorizontal();
                GUILayout.Space(2);
            }
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();
        GUI.DragWindow();
    }

    private bool ShouldIgnoreFile(string fileName)
    {
        string lower = fileName.ToLower();
        return lower == "license" || lower == "kerbalchecklistconfig";
    }

    void Update()
    {
        if (isDirty)
        {
            autoSaveTimer += Time.deltaTime;
            if (autoSaveTimer >= autoSaveInterval) { SaveActiveChecklist(); isDirty = false; autoSaveTimer = 0f; }
        }
        else
        {
            autoSaveTimer = 0f;
        }
    }

    private void MarkDirty() { isDirty = true; }

    private void DrawItemDeleteConfirmationDialog()
    {
        Rect dialogRect = new Rect(Screen.width / 2 - 150, Screen.height / 2 - 80, 300, 140);
        ClickThruBlocker.GUILayoutWindow("ItemDeleteDialog".GetHashCode(), dialogRect, id => {
            ModConfig config = modConfigs.ContainsKey(currentModFolder) ? modConfigs[currentModFolder] : null;
            string modName = config != null ? config.ModName : Path.GetFileName(currentChecklistFolder);
            string author = config != null ? config.Author : "";
            GUILayout.Label("Delete this item from the " + modName + " mod" + (string.IsNullOrEmpty(author) ? "" : " by " + author) + "?");
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Yes", GUILayout.Width(80)))
            {
                if (itemIndexToDelete >= 0 && itemIndexToDelete < checklist.Count) { checklist.RemoveAt(itemIndexToDelete); MarkDirty(); }
                showItemDeleteDialog = false;
            }
            if (GUILayout.Button("No", GUILayout.Width(80))) { showItemDeleteDialog = false; }
            GUILayout.EndHorizontal();
        }, "Delete Checklist Item", GUILayout.Width(300), GUILayout.Height(140));
    }

    private void DrawFileDeleteConfirmationDialog()
    {
        Rect dialogRect = new Rect(Screen.width / 2 - 150, Screen.height / 2 - 80, 300, 160);
        ClickThruBlocker.GUILayoutWindow("FileDeleteDialog".GetHashCode(), dialogRect, id => {
            GUILayout.Label("Delete checklist file:");
            GUILayout.Label(checklistToDelete);
            if (currentChecklistFolder == defaultSavedFolder)
            {
                GUILayout.Space(10);
                GUILayout.BeginHorizontal();
                GUILayout.Space(10);
                bool newVal = GUILayout.Toggle(deleteNeverAskAgainToggle, "Never Show Again", GUILayout.Width(140));
                if (newVal != deleteNeverAskAgainToggle) { deleteNeverAskAgainToggle = newVal; SaveSettings(); }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            else
            {
                ModConfig config = modConfigs.ContainsKey(currentModFolder) ? modConfigs[currentModFolder] : null;
                string modName = config != null ? config.ModName : Path.GetFileName(currentChecklistFolder);
                string author = config != null ? config.Author : "";
                GUILayout.Space(10);
                GUILayout.Label("Deleting this file will affect the " + modName + " mod" + (string.IsNullOrEmpty(author) ? "" : " by " + author) + ". Continue?");
            }
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Yes", GUILayout.Width(80))) { DeleteChecklist(checklistToDelete, currentChecklistFolder); showFileDeleteDialog = false; }
            if (GUILayout.Button("No", GUILayout.Width(80))) { showFileDeleteDialog = false; }
            GUILayout.EndHorizontal();
        }, "Delete Checklist File", GUILayout.Width(300), GUILayout.Height(160));
    }

    private void DrawConfirmNewChecklistDialog()
    {
        Rect dialogRect = new Rect(Screen.width / 2 - 150, Screen.height / 2 - 75, 300, 120);
        ClickThruBlocker.GUILayoutWindow("ConfirmNewChecklistDialog".GetHashCode(), dialogRect, id => {
            GUILayout.Label("Do you want to create a new checklist?");
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Yes", GUILayout.Width(80)))
            {
                showConfirmNewChecklistDialog = false;
                showSaveBeforeNewChecklistDialog = true;
            }
            if (GUILayout.Button("No", GUILayout.Width(80)))
            {
                showConfirmNewChecklistDialog = false;
            }
            GUILayout.EndHorizontal();
        }, "New Checklist", GUILayout.Width(300), GUILayout.Height(120));
    }


    private void DrawSaveBeforeNewChecklistDialog()
    {
        Rect dialogRect = new Rect(Screen.width / 2 - 150, Screen.height / 2 - 75, 300, 120);
        ClickThruBlocker.GUILayoutWindow("SaveBeforeNewChecklistDialog".GetHashCode(), dialogRect, id => {
            GUILayout.Label("Save before creating a new checklist?");
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Yes", GUILayout.Width(80)))
            {
                showSaveBeforeNewChecklistDialog = false;
                pendingNewChecklistAfterSave = true;
                showSaveChecklistDialog = true;
            }
            if (GUILayout.Button("No", GUILayout.Width(80)))
            {
                showSaveBeforeNewChecklistDialog = false;
                ResetChecklist();
            }
            GUILayout.EndHorizontal();
        }, "Save Checklist", GUILayout.Width(300), GUILayout.Height(120));
    }


    private void DrawSaveChecklistDialog()
    {
        if (currentChecklistFolder != defaultSavedFolder) { showSaveChecklistDialog = false; return; }
        Rect dialogRect = new Rect(Screen.width / 2 - 150, Screen.height / 2 - 70, 300, 140);
        ClickThruBlocker.GUILayoutWindow("SaveChecklistDialog".GetHashCode(), dialogRect, id => {
            GUILayout.Label("Enter name for checklist:");
            string nameInput = GUILayout.TextField(currentChecklistName, GUILayout.Width(250));
            currentChecklistName = nameInput;
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save", GUILayout.Width(80)))
            {
                if (!string.IsNullOrEmpty(currentChecklistName))
                {
                    SaveChecklist(currentChecklistName, currentChecklistFolder);
                    showSaveChecklistDialog = false;
                    isNewChecklistOperation = false;
                    if (pendingNewChecklistAfterSave) { pendingNewChecklistAfterSave = false; ResetChecklist(); }
                }
            }
            if (GUILayout.Button("Cancel", GUILayout.Width(80)))
            {
                showSaveChecklistDialog = false;
                if (pendingNewChecklistAfterSave) { pendingNewChecklistAfterSave = false; ResetChecklist(); }
            }
            GUILayout.EndHorizontal();
        }, "Save Checklist", GUILayout.Width(300), GUILayout.Height(140));
    }

    private void DrawModSaveChecklistDialog()
    {
        Rect dialogRect = new Rect(Screen.width / 2 - 150, Screen.height / 2 - 60, 300, 120);
        ClickThruBlocker.GUILayoutWindow("ModSaveChecklistDialog".GetHashCode(), dialogRect, id => {
            GUILayout.Label("Save changes to current mod checklist?");
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save", GUILayout.Width(80)))
            {
                if (!string.IsNullOrEmpty(currentChecklistName))
                    SaveChecklist(currentChecklistName, currentChecklistFolder);
                showModSaveChecklistDialog = false;
            }
            if (GUILayout.Button("Cancel", GUILayout.Width(80))) { showModSaveChecklistDialog = false; }
            GUILayout.EndHorizontal();
        }, "Save Mod Checklist", GUILayout.Width(300), GUILayout.Height(120));
    }


    private void ResetChecklist()
    {
        checklist.Clear();
        currentChecklistName = "";
        currentChecklistFolder = defaultSavedFolder;
        currentModFolder = "";
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
            string path = Path.Combine(defaultFolder, "activeChecklist.txt");
            using (StreamWriter writer = new StreamWriter(path))
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
        {
            UnityEngine.Debug.Log("[KerbalChecklists] No active checklist found, starting new.");
        }
    }

    void SaveChecklist(string checklistName, string folder)
    {
        string checklistPath = Path.Combine(folder, checklistName + ".txt");
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

    void LoadChecklistFromFile(string checklistName, string folder)
    {
        string checklistPath = Path.Combine(folder, checklistName + ".txt");
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

    void DeleteChecklist(string checklistName, string folder)
    {
        string checklistPath = Path.Combine(folder, checklistName + ".txt");
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
