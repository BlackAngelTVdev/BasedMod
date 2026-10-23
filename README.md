# 🚀 BasedMod
![Stars](https://img.shields.io/github/stars/BlackAngelTVdev/BasedMod?style=for-the-badge&color=yellow)
![Commits](https://img.shields.io/github/commit-activity/m/BlackAngelTVdev/BasedMod?style=for-the-badge&color=blue)
![Issues](https://img.shields.io/github/issues/BlackAngelTVdev/BasedMod?style=for-the-badge&color=orange)
![Forks](https://img.shields.io/github/forks/BlackAngelTVdev/BasedMod?style=for-the-badge&color=808080)
![Last Commit](https://img.shields.io/github/last-commit/BlackAngelTVdev/BasedMod?style=for-the-badge&color=blue)

> **Un mod BepInEx 5 pour Supermarket Together qui ajuste les fonds, la vitesse de déplacement et la hauteur de saut.**

---

## 🧐 Aperçu
![BasedMod](Asset/Img/banner.png)

## ✨ Fonctionnalités
- ✅ **Multiplicateur de fonds** : modifie les montants ajoutés ou retirés via `GameData.CmdAlterFunds`, sans toucher au reste de l'économie.
- ✅ **Vitesse ajustable** : multiplicateur appliqué à la marche, au sprint et au déplacement accroupi.
- ✅ **Hauteur de saut ajustable** : multiplicateur appliqué à `JumpHeight`, pour un saut plus haut sans modifier la gravité ni le `CharacterController`.
- ✅ **Configuration sans recompilation** : toutes les valeurs se règlent dans un fichier `.cfg` généré par BepInEx.
- ✅ **Build automatique** : chaque `dotnet build` copie le DLL du mod directement dans `BepInEx\plugins`.

## 🛠 Tech Stack
| Technologie | Usage |
| :--- | :--- |
| ![C#](https://img.shields.io/badge/C%23-net472-512BD4?style=flat-square&logo=csharp&logoColor=white) | Logique du mod |
| ![BepInEx](https://img.shields.io/badge/BepInEx-5.4.23.5-2f855a?style=flat-square) | Chargement du plugin, logs et configuration |
| ![Harmony](https://img.shields.io/badge/HarmonyX-2.9-7c3aed?style=flat-square) | Patches runtime sur le code du jeu |
| ![Unity](https://img.shields.io/badge/Unity-Mono-000000?style=flat-square&logo=unity&logoColor=white) | Moteur du jeu ciblé |

## 🚀 Installation & Lancement

### Prérequis
- **Windows** et la version **Steam** de Supermarket Together.
- **.NET SDK 8** (ou plus récent) avec le **targeting pack .NET Framework 4.7.2** pour compiler.
- **BepInEx 5 x64** installé dans le dossier du jeu.

### 1. Installer BepInEx
Le script fourni télécharge BepInEx **5.4.23.5 x64** et l'extrait dans le dossier du jeu. Il ne fait rien si BepInEx est déjà présent.

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-BepInEx.ps1 -GamePath "E:\SteamLibrary\steamapps\common\Supermarket Together"
```

Sans `-GamePath`, le script utilise `E:\SteamLibrary\steamapps\common\Supermarket Together`. Adapte le chemin à ton installation Steam.

Installation manuelle possible : télécharge `BepInEx_win_x64_5.4.23.5.zip` depuis les releases BepInEx et extrais son contenu à la racine du jeu (à côté de `Supermarket Together.exe`).

### 2. Cloner le projet
```bash
git clone https://github.com/BlackAngelTVdev/BasedMod.git
cd BasedMod
```

### 3. Configurer le chemin du jeu
Ouvre `MonSupermarketMod/MonSupermarketMod.csproj` et adapte la propriété `GameDir` :

```xml
<GameDir>E:\SteamLibrary\steamapps\common\Supermarket Together\</GameDir>
```

Les DLL de référence (`BepInEx.dll`, `0Harmony.dll`, `Assembly-CSharp.dll`, `Unity.StarterAssets.dll`, etc.) sont lues depuis ce dossier.

### 4. Compiler et installer le plugin
```bash
dotnet build -c Release
```

La cible post-build copie automatiquement `MonSupermarketMod.dll` dans :

```
[GameDir]\BepInEx\plugins\MonSupermarketMod.dll
```

Si tu préfères installer un DLL déjà compilé, copie simplement `MonSupermarketMod.dll` dans `BepInEx\plugins\`.

### 5. Lancer le jeu
Démarre Supermarket Together, puis vérifie `BepInEx\LogOutput.log`. Tu dois y voir :

```
Mon Super Mod v1.0.0 chargé.
[Joueur] Vitesse x2 — marche 10, sprint 20, accroupi 8 | saut x1.8 — hauteur 2.16 m.
```

## 📖 Utilisation
Au premier lancement, BepInEx crée le fichier :

```
BepInEx\config\com.username.supermarkettogethermod.cfg
```

Modifie les valeurs puis relance le jeu (la configuration est lue au démarrage) :

```ini
[Joueur]
ActiverModifications = true
MultiplicateurVitesse = 2
MultiplicateurSaut = 1.8

[Général]
MultiplicateurDeFonds = 1
```

| Clé | Section | Défaut | Effet |
| :--- | :--- | :---: | :--- |
| `ActiverModifications` | `Joueur` | `true` | Active ou désactive les réglages de vitesse et de saut |
| `MultiplicateurVitesse` | `Joueur` | `2` | Multiplie marche, sprint et vitesse accroupie |
| `MultiplicateurSaut` | `Joueur` | `1.8` | Multiplie la hauteur de saut |
| `MultiplicateurDeFonds` | `Général` | `1` | Multiplie les fonds ajoutés/retirés (`1` = vanilla) |

Une valeur de `1` conserve le comportement d'origine. Les multiplicateurs sont bornés entre `0.1` et `10`.

## 🗂 Structure du projet
```text
BasedMod/
├── Asset/Img/banner.png
├── Install-BepInEx.ps1
└── MonSupermarketMod/
    ├── Plugin.cs
    ├── Patches/
    │   ├── GameDataPatch.cs
    │   └── PlayerPatches.cs
    └── MonSupermarketMod.csproj
```

## 🤝 Contribution
1. Forkez le projet
2. Créez votre branche (`git checkout -b feature/AmazingFeature`)
3. Commit (`git commit -m 'Add some AmazingFeature'`)
4. Push (`git push origin feature/AmazingFeature`)
5. Ouvrez une Pull Request

## 👤 Auteur

**BlackAngelTVdev**
![Follow](https://img.shields.io/github/followers/BlackAngelTVdev?label=Follow%20Me&style=social)

---
## 📄 Licence

Aucune licence explicite n'est encore définie pour ce dépôt :
![GitHub License](https://img.shields.io/github/license/BlackAngelTVdev/BasedMod?style=flat-square&color=blue)

> Mod non officiel, non affilié aux développeurs de Supermarket Together.

### 🧑‍💻 Contributors

Merci à toutes les personnes qui contribuent au projet.

[![Contributors](https://contrib.rocks/image?repo=BlackAngelTVdev/BasedMod)](https://github.com/BlackAngelTVdev/BasedMod/graphs/contributors)
