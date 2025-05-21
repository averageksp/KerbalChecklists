using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using KSP;
using KSP.UI.Screens;
using ClickThroughFix;
using KSP.Localization;
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

    private Texture2D arrowUpTex;
    private Texture2D arrowDownTex;

    void Start()
    {
        defaultFolder = Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "KerbalChecklists");
        if (!Directory.Exists(defaultFolder)) Directory.CreateDirectory(defaultFolder);
        defaultSavedFolder = Path.Combine(defaultFolder, "SavedChecklists");
        if (!Directory.Exists(defaultSavedFolder)) Directory.CreateDirectory(defaultSavedFolder);
        currentChecklistFolder = defaultSavedFolder;
        currentModFolder = "";
        activeChecklistPath = Path.Combine(defaultFolder, "activeChecklist.txt");
        settingsPath = Path.Combine(defaultFolder, "settings.txt");

        if (!File.Exists(settingsPath) || new FileInfo(settingsPath).Length == 0)
        {
            deleteNeverAskAgainToggle = false;
            SaveSettings();
        }
        else
        {
            LoadSettings();
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

        LoadActiveChecklist();

        string[] possibleIconPaths = {
            Path.Combine(Path.GetFileName(defaultFolder), "Textures/icon"),
            Path.Combine(Path.GetFileName(defaultFolder) + "/Textures/icon"),
            "KerbalChecklists-1.3/KerbalChecklists/Textures/icon",
            "KerbalChecklists/Textures/icon",
            "KerbalChecklists-1.3/Textures/icon"
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

        arrowUpTex = GameDatabase.Instance.GetTexture("KerbalChecklists/Textures/arrow_up", false);
        arrowDownTex = GameDatabase.Instance.GetTexture("KerbalChecklists/Textures/arrow_down", false);

        appButton = ApplicationLauncher.Instance.AddModApplication(
            OnToggleGUI,
            OnToggleGUI,
            null, null, null, null,
            ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.SPACECENTER |
            ApplicationLauncher.AppScenes.VAB | ApplicationLauncher.AppScenes.SPH,
            icon);
    }

    void OnDestroy()
    {
        if (appButton != null)
            ApplicationLauncher.Instance.RemoveModApplication(appButton);
        SaveActiveChecklist();
        SaveSettings();
    }

    void Update()
    {
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
        else
        {
            autoSaveTimer = 0f;
        }
    }

    private void OnToggleGUI() { showGUI = !showGUI; }

    void OnGUI()
    {
        GUI.skin = HighLogic.Skin;
        if (showGUI)
            windowRect = ClickThruBlocker.GUILayoutWindow("KerbalChecklistsWindow".GetHashCode(), windowRect, DrawWindow, "Kerbal Checklists", GUILayout.Width(350), GUILayout.Height(650));
        if (showConfirmNewChecklistDialog) DrawConfirmNewChecklistDialog();
        if (showSaveBeforeNewChecklistDialog) DrawSaveBeforeNewChecklistDialog();
        if (showSaveChecklistDialog) DrawSaveChecklistDialog();
        if (showModSaveChecklistDialog) DrawModSaveChecklistDialog();
        if (showFileDeleteDialog) DrawFileDeleteConfirmationDialog();
        if (showItemDeleteDialog) DrawItemDeleteConfirmationDialog();
    }

    void DrawWindow(int windowID)
    {
        GUILayout.BeginVertical();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(Localizer.Format("#autoLOC_8102000"), GUILayout.Height(30)))
        {
            isNewChecklistOperation = true;
            if (currentChecklistFolder == defaultSavedFolder)
                showConfirmNewChecklistDialog = true;
            else
                ResetChecklist();
        }

        if (GUILayout.Button(Localizer.Format("#autoLOC_8102001"), GUILayout.Height(30)))
        {
            if (currentChecklistFolder == defaultSavedFolder)
            {
                isNewChecklistOperation = false;
                showSaveChecklistDialog = true;
            }
            else
                showModSaveChecklistDialog = true;
        }

        if (GUILayout.Button(Localizer.Format("#autoLOC_8102002"), GUILayout.Height(30)))
            availableScrollPos = Vector2.zero;

        availableScrollPos = Vector2.zero;
        GUILayout.EndHorizontal();

        GUILayout.Space(5);
        GUILayout.Label(Localizer.Format("#autoLOC_8102003"));

        GUILayout.BeginHorizontal();
        GUIStyle textFieldStyle = new GUIStyle(GUI.skin.textArea) { wordWrap = true };
        float requiredHeight = textFieldStyle.CalcHeight(new GUIContent(newItemText), 300f);
        int clampedLines = Mathf.Clamp(Mathf.CeilToInt(requiredHeight / 22f), 1, 10);
        newItemText = GUILayout.TextField(newItemText, textFieldStyle, GUILayout.Width(300), GUILayout.Height(20 * clampedLines));

        GUILayout.Button(Localizer.Format("#autoLOC_8102004"), GUILayout.Width(80), GUILayout.Height(30));
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
        GUILayout.Label(Localizer.Format("#autoLOC_8102005"));
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

            if (arrowUpTex != null && GUILayout.Button(new GUIContent(arrowUpTex), GUILayout.Width(24), GUILayout.Height(24)))
            {
                if (i > 0)
                {
                    var tmp = checklist[i - 1];
                    checklist[i - 1] = checklist[i];
                    checklist[i] = tmp;
                    MarkDirty();
                    if (!string.IsNullOrEmpty(currentChecklistName))
                    {
                        SaveChecklist(currentChecklistName, currentChecklistFolder);
                    }
                }
            }
            if (arrowDownTex != null && GUILayout.Button(new GUIContent(arrowDownTex), GUILayout.Width(24), GUILayout.Height(24)))
            {
                if (i < checklist.Count - 1)
                {
                    var tmp = checklist[i + 1];
                    checklist[i + 1] = checklist[i];
                    checklist[i] = tmp;
                    MarkDirty();
                    if (!string.IsNullOrEmpty(currentChecklistName))
                    {
                        SaveChecklist(currentChecklistName, currentChecklistFolder);
                    }
                }
            }

           if (GUILayout.Button(Localizer.Format("#autoLOC_8102006"), GUILayout.Width(80)))
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
            if (item.Completed) completedCount++;
        GUILayout.Label(Localizer.Format("#autoLOC_8102007", completedCount, checklist.Count));
        if (!string.IsNullOrEmpty(currentChecklistFolder) && currentChecklistFolder != defaultSavedFolder)
        {
            if (modConfigs.ContainsKey(currentModFolder))
            {
                ModConfig config = modConfigs[currentModFolder];
                GUILayout.Label(Localizer.Format("#autoLOC_8102008", config.ModName));
                GUILayout.Label(Localizer.Format("#autoLOC_8102009", config.Author));
            }
            else
            {
                GUILayout.Label(Localizer.Format("#autoLOC_8102010", Path.GetFileName(currentChecklistFolder)));
            }
        }
        GUILayout.Space(5);
        GUILayout.BeginHorizontal();
        GUILayout.Label(Localizer.Format("#autoLOC_8102011"), GUILayout.Width(150));
        currentChecklistName = GUILayout.TextField(currentChecklistName, GUILayout.Width(200));
        GUILayout.EndHorizontal();

        GUILayout.Space(5);
        GUILayout.Label(Localizer.Format("#autoLOC_8102012"));
        availableScrollPos = GUILayout.BeginScrollView(availableScrollPos, GUILayout.Height(120));
        string[] defaultFiles = Directory.GetFiles(defaultSavedFolder, "*.txt");
        foreach (string file in defaultFiles)
        {
            string fileName = Path.GetFileNameWithoutExtension(file);
            if (ShouldIgnoreFile(fileName))
                continue;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(fileName, GUILayout.Width(250))) { currentChecklistFolder = defaultSavedFolder; currentModFolder = ""; LoadChecklistFromFile(fileName, defaultSavedFolder); }
            if (GUILayout.Button(Localizer.Format("#autoLOC_8102050"), GUILayout.Width(80)))
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
                if (GUILayout.Button(Localizer.Format("#autoLOC_8102050"), GUILayout.Width(80))) { checklistToDelete = fileName; currentChecklistFolder = modChecklistsPath; currentModFolder = modFolder; showFileDeleteDialog = true; }
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

    private void MarkDirty() { isDirty = true; }

    private void ResetChecklist()
    {
        checklist.Clear();
        currentChecklistName = "";
        currentChecklistFolder = defaultSavedFolder;
        currentModFolder = "";
    }

    private void SaveSettings()
    {
        try
        {
            using (var w = new StreamWriter(settingsPath))
                w.WriteLine("NeverAskDelete=" + deleteNeverAskAgainToggle);
        }
        catch (Exception ex) { Debug.LogError("[KerbalChecklists] Error saving settings: " + ex.Message); }
    }

    private void LoadSettings()
    {
        if (!File.Exists(settingsPath)) return;
        try
        {
            foreach (var line in File.ReadAllLines(settingsPath))
                if (line.StartsWith("NeverAskDelete="))
                    bool.TryParse(line.Substring("NeverAskDelete=".Length), out deleteNeverAskAgainToggle);
        }
        catch (Exception ex) { Debug.LogError("[KerbalChecklists] Error loading settings: " + ex.Message); }
    }

    private void SaveActiveChecklist()
    {
        try
        {
            using (var w = new StreamWriter(activeChecklistPath))
                foreach (var it in checklist)
                    w.WriteLine(it.Completed + "|" + it.Text);
            UnityEngine.Debug.Log("[KerbalChecklists] Active checklist saved.");
        }
        catch (Exception ex) { Debug.LogError("[KerbalChecklists] Error saving active checklist: " + ex.Message); }
    }

    private void LoadActiveChecklist()
    {
        checklist.Clear();
        if (!File.Exists(activeChecklistPath)) return;
        try
        {
            foreach (var line in File.ReadAllLines(activeChecklistPath))
            {
                var parts = line.Split('|');
                if (parts.Length == 2)
                    checklist.Add(new ChecklistItem { Completed = bool.Parse(parts[0]), Text = parts[1] });
            }
            UnityEngine.Debug.Log("[KerbalChecklists] Active checklist loaded.");
        }
        catch (Exception ex) { Debug.LogError("[KerbalChecklists] Error loading active checklist: " + ex.Message); }
    }

    private void SaveChecklist(string name, string folder)
    {
        string path = Path.Combine(folder, name + ".txt");
        try
        {
            using (var w = new StreamWriter(path))
                foreach (var it in checklist)
                    w.WriteLine(it.Completed + "|" + it.Text);
            UnityEngine.Debug.Log("[KerbalChecklists] Checklist '" + name + "' saved.");
        }
        catch (Exception ex) { Debug.LogError("[KerbalChecklists] Error saving checklist '" + name + "': " + ex.Message); }
    }

    private void LoadChecklistFromFile(string name, string folder)
    {
        string path = Path.Combine(folder, name + ".txt");
        if (!File.Exists(path)) return;
        checklist.Clear();
        try
        {
            foreach (var line in File.ReadAllLines(path))
            {
                var parts = line.Split('|');
                if (parts.Length == 2)
                    checklist.Add(new ChecklistItem { Completed = bool.Parse(parts[0]), Text = parts[1] });
            }
            currentChecklistName = name;
            UnityEngine.Debug.Log("[KerbalChecklists] Checklist '" + name + "' loaded.");
        }
        catch (Exception ex) { Debug.LogError("[KerbalChecklists] Error loading checklist '" + name + "': " + ex.Message); }
    }

    private void DeleteChecklist(string name, string folder)
    {
        string path = Path.Combine(folder, name + ".txt");
        if (!File.Exists(path)) return;
        try
        {
            File.Delete(path);
            UnityEngine.Debug.Log("[KerbalChecklists] Checklist '" + name + "' deleted.");
        }
        catch (Exception ex) { Debug.LogError("[KerbalChecklists] Error deleting checklist '" + name + "': " + ex.Message); }
    }

    private void DrawConfirmNewChecklistDialog()
    {
        Rect r = new Rect(Screen.width / 2 - 150, Screen.height / 2 - 75, 300, 120);
        ClickThruBlocker.GUILayoutWindow("ConfirmNewChecklistDialog".GetHashCode(), r, id =>
        {
            GUILayout.Label(Localizer.Format("#autoLOC_8102013"));
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Localizer.Format("#autoLOC_8102014"), GUILayout.Width(80)))
            {
                showConfirmNewChecklistDialog = false;
                showSaveBeforeNewChecklistDialog = true;
            }
            if (GUILayout.Button(Localizer.Format("#autoLOC_8102015"), GUILayout.Width(80)))
                showConfirmNewChecklistDialog = false;
            GUILayout.EndHorizontal();
        }, Localizer.Format("#autoLOC_8102016"), GUILayout.Width(300), GUILayout.Height(120));
    }

    private void DrawSaveBeforeNewChecklistDialog()
    {
        Rect r = new Rect(Screen.width / 2 - 150, Screen.height / 2 - 75, 300, 120);
        ClickThruBlocker.GUILayoutWindow("SaveBeforeNewChecklistDialog".GetHashCode(), r, id =>
        {
            GUILayout.Label(Localizer.Format("#autoLOC_8102017"));
            GUILayout.Space(10);
            
            GUILayout.BeginHorizontal();
            
            if (GUILayout.Button(Localizer.Format("#autoLOC_8102018"), GUILayout.Width(80)))
            {
                showSaveBeforeNewChecklistDialog = false;
                pendingNewChecklistAfterSave = true;
                showSaveChecklistDialog = true;
            }
            
            if (GUILayout.Button(Localizer.Format("#autoLOC_8102019"), GUILayout.Width(80)))
            {
                showSaveBeforeNewChecklistDialog = false;
                ResetChecklist();
            }
            
            GUILayout.EndHorizontal();
        }, Localizer.Format("#autoLOC_8102020"), GUILayout.Width(300), GUILayout.Height(120));  // Localize the window title
    }


    private void DrawSaveChecklistDialog()
    {
        if (currentChecklistFolder != defaultSavedFolder) { showSaveChecklistDialog = false; return; }
        Rect r = new Rect(Screen.width / 2 - 150, Screen.height / 2 - 70, 300, 140);
        ClickThruBlocker.GUILayoutWindow("SaveChecklistDialog".GetHashCode(), r, id =>
        {
            GUILayout.Label(Localizer.Format("#autoLOC_8102021"));
            
            currentChecklistName = GUILayout.TextField(currentChecklistName, GUILayout.Width(250));
            GUILayout.Space(10);
            
            GUILayout.BeginHorizontal();
            
            if (GUILayout.Button(Localizer.Format("#autoLOC_8102022"), GUILayout.Width(80)))
            {
                if (!string.IsNullOrEmpty(currentChecklistName))
                {
                    SaveChecklist(currentChecklistName, currentChecklistFolder);
                    showSaveChecklistDialog = false;
                    isNewChecklistOperation = false;
                    if (pendingNewChecklistAfterSave) { pendingNewChecklistAfterSave = false; ResetChecklist(); }
                }
            }
            
            if (GUILayout.Button(Localizer.Format("#autoLOC_8102023"), GUILayout.Width(80)))
            {
                showSaveChecklistDialog = false;
                if (pendingNewChecklistAfterSave) { pendingNewChecklistAfterSave = false; ResetChecklist(); }
            }
            
            GUILayout.EndHorizontal();
        }, Localizer.Format("#autoLOC_8102024"), GUILayout.Width(300), GUILayout.Height(140));  // Localize the window title
    }


    private void DrawModSaveChecklistDialog()
    {
        Rect r = new Rect(Screen.width / 2 - 150, Screen.height / 2 - 60, 300, 120);
        ClickThruBlocker.GUILayoutWindow("ModSaveChecklistDialog".GetHashCode(), r, id =>
        {
            GUILayout.Label(Localizer.Format("#autoLOC_8102025"));
            GUILayout.Space(10);
            
            GUILayout.BeginHorizontal();
            
            if (GUILayout.Button(Localizer.Format("#autoLOC_8102026"), GUILayout.Width(80)))
            {
                if (!string.IsNullOrEmpty(currentChecklistName))
                    SaveChecklist(currentChecklistName, currentChecklistFolder);
                showModSaveChecklistDialog = false;
            }
            
            if (GUILayout.Button(Localizer.Format("#autoLOC_8102027"), GUILayout.Width(80)))
                showModSaveChecklistDialog = false;
            
            GUILayout.EndHorizontal();
        }, Localizer.Format("#autoLOC_8102028"), GUILayout.Width(300), GUILayout.Height(120));
    }


    private void DrawFileDeleteConfirmationDialog()
    {
        Rect r = new Rect(Screen.width / 2 - 150, Screen.height / 2 - 80, 300, 160);
        ClickThruBlocker.GUILayoutWindow("FileDeleteDialog".GetHashCode(), r, id =>
        {
            GUILayout.Label(Localizer.Format("#autoLOC_8102029"));
            GUILayout.Label(checklistToDelete);
            if (currentChecklistFolder == defaultSavedFolder)
            {
                GUILayout.Space(10);
                GUILayout.BeginHorizontal();
                GUILayout.Space(10);
                bool newVal = GUILayout.Toggle(deleteNeverAskAgainToggle, Localizer.Format("#autoLOC_8102030"), GUILayout.Width(140));
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
                GUILayout.Label(string.Format(Localizer.Format("#autoLOC_8102031"), modName, string.IsNullOrEmpty(author) ? "" : author));
            }
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Localizer.Format("#autoLOC_8102032"), GUILayout.Width(80))) { DeleteChecklist(checklistToDelete, currentChecklistFolder); showFileDeleteDialog = false; }
            if (GUILayout.Button(Localizer.Format("#autoLOC_8102033"), GUILayout.Width(80))) { showFileDeleteDialog = false; }
            GUILayout.EndHorizontal();
        }, Localizer.Format("#autoLOC_8102034"), GUILayout.Width(300), GUILayout.Height(160));
    }

    private void DrawItemDeleteConfirmationDialog()
    {
        Rect r = new Rect(Screen.width / 2 - 150, Screen.height / 2 - 80, 300, 140);
        ClickThruBlocker.GUILayoutWindow("ItemDeleteDialog".GetHashCode(), r, id =>
        {
            ModConfig config = modConfigs.ContainsKey(currentModFolder) ? modConfigs[currentModFolder] : null;
            string modName = config != null ? config.ModName : Path.GetFileName(currentChecklistFolder);
            string author = config != null ? config.Author : "";
            GUILayout.Label(string.Format(Localizer.Format("#autoLOC_8102035"), modName, string.IsNullOrEmpty(author) ? "" : author));
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Localizer.Format("#autoLOC_8102036"), GUILayout.Width(80))) { }
            {
                if (itemIndexToDelete >= 0 && itemIndexToDelete < checklist.Count)
                {
                    checklist.RemoveAt(itemIndexToDelete);
                    MarkDirty();
                }
                showItemDeleteDialog = false;
            }
            if (GUILayout.Button("No", GUILayout.Width(80))) { showItemDeleteDialog = false; }
            GUILayout.EndHorizontal();
        }, Localizer.Format("#autoLOC_8102037"), GUILayout.Width(300), GUILayout.Height(140));
    }
}
