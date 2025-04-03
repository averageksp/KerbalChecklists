# Kerbal Checklists

**Kerbal Checklists** is a simple and efficient in-game checklist system for **Kerbal Space Program (KSP)** to help you stay on top of your rocket, plane, and flight preparations.

---

## Features
- **Create, save, and load checklists** for **VAB, SPH, and Flight mode**
- **Automatically saves checklists** while editing
- **Toolbar integration** with a custom icon
- **Refresh button** to reload checklists without restarting the game
- **Easy deletion** of old checklists with optional confirmation
- **Character limit warning** (optional)

---

## Installation
1. Download the latest version from the [Releases](https://github.com/averageksp/KerbalChecklists/releases).
2. Extract the contents into your **KSP GameData** folder.
3. Ensure the folder structure looks like this:
   ```
   GameData/
     KerbalChecklists/
       KerbalChecklists.dll
       Textures/icon.png
       SavedChecklists/
   ```
4. Launch **KSP** and start using **Kerbal Checklists**!

---

## How to Use
- Open the **Kerbal Checklists** UI by clicking on the toolbar icon.
- **Add, remove, and mark checklist items** as completed.
- **Save and load checklists** at any time.
- Use the **refresh button** to reload checklists without restarting the game.
- When pressing **"New Checklist"**, you’ll be prompted to save your current checklist first.

---

## How to Compile
How to compile it yourself.

1. Open Visual Studio 2022
2. Copy the source code from this repository
3. Select the references below
   ```
   KSPAssets.dll
   UnityEngine.InputModule.dll
   UnityEngine.InputLegacyModule.dll
   UnityEngine.PhysicsModule.dll
   KSPAssets.XmlSerializers.dll
   UnityEngine.AnimationModulue.dll
   UnityEngine.CoreModule.dll
   UnityEngine.IMGUIModule.dll
   UnityEngine.UI.dll
   Assembly-CSharp.dll
   Assembly-CSharp-firstpass.dll
   ```
4. After that compile it yourself and make a folder called KerbalChecklists in your GameData folder
5. And you're done! There will be no icon for it so you have to copy it from the actual release and create your own folder called Textures in the KerbalChecklists so it should look like this
   ```
   KerbalChecklists/Textures/icon.png
   ```

---

## Issues and Contributions
If you find any issues or have suggestions, feel free to open a ticket in the [Issues](https://github.com/averageksp/KerbalChecklists/issues) section. Contributions are welcome via pull requests!

---

## License
This project is licensed under the **MIT License**. See the [LICENSE](LICENSE) file for details.

