# Kerbal Checklists
![License](https://img.shields.io/badge/License-MIT-green.svg)
![Downloads](https://img.shields.io/github/downloads/averageksp/KerbalChecklists/total.svg)
![Imgur Image](https://imgur.com/l7WQ51u.png)
**Kerbal Checklists** is an in-game checklist system for **Kerbal Space Program (KSP)** to help you manage launch procedures, space missions, and vehicle preparation.

---

## ⚠️ Dependencies
- Requires ClickThroughBlocker.
  If you are using CKAN it will be done automatically, if not then download it from either of these websites:
- SpaceDock: https://spacedock.info/mod/1689/Click%20Through%20Blocker
- GitHub: https://github.com/linuxgurugamer/ClickThroughBlocker
- Forum Post (do not download it here): https://forum.kerbalspaceprogram.com/topic/170747-112x-click-through-blocker-new-dependency/

## ✨ Features

- Create, save, and load checklists for **VAB**, **SPH**, and **Flight** modes
- Auto-saves while editing checklists
- Toolbar icon integration for quick access
- Refresh checklists without restarting the game
- Safe checklist deletion with confirmation prompts

---

## 📦 Installation

1. Download the latest version from the [Releases](https://github.com/averageksp/KerbalChecklists/releases).
2. Extract the zip into your **KSP/GameData/** folder.
3. Your folder structure should look like this:
   ```
   GameData/
     KerbalChecklists/
       SavedChecklists/
         Checklist.txt
       Plugin/
         KerbalChecklists.dll
       Textures/
         icon.png
   ```
4. Launch KSP and click the toolbar icon to open (must be in VAB, SPH or Flight for it to show up) **Kerbal Checklists**.

---

## 🚀 How to Use

- Click the **toolbar icon** to open the checklist UI.
- **Add, check off, and delete items** from your list.
- Use the **Save** button to save your current checklist, and **Load** to retrieve existing ones.
- The **New Checklist** button prompts a save (for default lists) before clearing your items.
- Click **Refresh** to reload checklists during runtime.

---

### 🛠️ Making a Checklist Mod

1. Inside `GameData`, create a folder named after your mod.
2. Create a folder in the folder you just created, it has to be called `Checklists`.
3. Add a `.txt` file for example with one checklist item per line, like:
   ```
   False|SAS
   False|RCS
   False|Solar Panels
   ```
   False would be that it is **not** checked and true would be that it **will** be checked.
4. To make the mod show up please make a `.txt` file inside your folder and call it `KerbalChecklistsConfig.txt`.
   You should put this into the `.txt` file and save it.
   ```
   ModName=ExampleMod
   Author=averageksp
   AddToKerbalChecklists=true
   ```
5. **REQUIRED**: If you want to publish your mod (e.g. to CKAN, Spacedock, etc.) a licence **MUST** be included.
---

## 🛠️ How to Compile

Want to build from source?

1. Open **Visual Studio**.
2. Clone or download this repo.
3. Add the following references:
   ```
   KSPAssets.dll
   UnityEngine.InputModule.dll
   UnityEngine.InputLegacyModule.dll
   UnityEngine.PhysicsModule.dll
   KSPAssets.XmlSerializers.dll
   UnityEngine.AnimationModule.dll
   UnityEngine.CoreModule.dll
   UnityEngine.IMGUIModule.dll
   UnityEngine.UI.dll
   Assembly-CSharp.dll
   Assembly-CSharp-firstpass.dll
   ```
4. Build the project.
5. Copy the compiled `KerbalChecklists.dll` to:
   ```
   GameData/KerbalChecklists/
   ```
6. Add the toolbar icon image:
   ```
   GameData/KerbalChecklists/Textures/icon.png
   ```

---

## 🐞 Issues & Contributions

Found a bug or want to suggest a feature? Head over to the [Issues](https://github.com/averageksp/KerbalChecklists/issues) tab.

Pull requests are welcome!

---

## Additional Resources

For a list of useful links and resources related to this mod, check out [LINKS.md](./LINKS.md).

---

## 📄 License

This project is licensed under the **MIT License**. See the [LICENSE](https://github.com/averageksp/KerbalChecklists/blob/main/LICENSE) file for more information.

