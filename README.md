# 🚀 ItemAdder
![Stars](https://img.shields.io/github/stars/BlackAngelTVdev/BasedMod?style=for-the-badge&color=yellow)
![Commits](https://img.shields.io/github/commit-activity/m/BlackAngelTVdev/BasedMod?style=for-the-badge&color=blue)
![Issues](https://img.shields.io/github/issues/BlackAngelTVdev/BasedMod?style=for-the-badge&color=orange)
![Forks](https://img.shields.io/github/forks/BlackAngelTVdev/BasedMod?style=for-the-badge&color=808080)
![Last Commit](https://img.shields.io/github/last-commit/BlackAngelTVdev/BasedMod?style=for-the-badge&color=blue)

> **ItemAdder — un mod BepInEx 5 pour Supermarket Together : un rayon tabac complet, un sous-sol clandestin à cultiver, des employés plus logiques, et un launcher Windows qui installe et met à jour le tout.**

---

## 🧐 Aperçu
![ItemAdder](Asset/Img/banner.png)

Le projet contient deux programmes indépendants, qui peuvent être utilisés séparément :

| Composant | Rôle | Version |
| :--- | :--- | :---: |
| `ItemAdder.dll` | Le mod, chargé par BepInEx dans le jeu. Il ajoute les produits, le rayon tabac, le sous-sol, la police, le réassort intelligent et le bouton du menu Échap. | 1.5.3 |
| `ItemAdderLauncher.exe` | L'application Windows avec menu latéral qui s'installe sur le PC (icône sur le Bureau), détecte le jeu, installe BepInEx et le mod, choisit les modules actifs, lit les actualités GitHub, lance le jeu et gère les mises à jour. | 1.5.1 |

Le jeu n'est ni modifié ni recompilé : le mod s'accroche au chargement (BepInEx + Harmony) et ajoute ses données aux tableaux du jeu en mémoire. Aucun asset externe n'est nécessaire, les modèles 3D des nouveaux produits sont générés par code.

> Mod non officiel, non affilié aux développeurs de Supermarket Together.

## 🧭 Sommaire
- [Fonctionnalités](#-fonctionnalités)
- [Comment ça marche](#-comment-ça-marche)
- [Installation & lancement](#-installation--lancement)
- [Utilisation](#-utilisation)
- [Configuration du mod](#-configuration-du-mod)
- [Mises à jour](#-mises-à-jour)
- [Dépannage](#-dépannage)
- [Structure du projet](#-structure-du-projet)

## ✨ Fonctionnalités

### Le mod
- ✅ **Rayon Tabac** : 44 produits vendables dans un nouveau groupe « Tabac » du carnet de commandes — cigarettes, tabac à rouler, tubes et filtres, vapes jetables et e-liquides, snus, tabac chauffé, cigares et articles pour fumeurs.
- ✅ **Modèles 3D générés** : chaque produit a son maillage (coques biseautées, pièces tournées : paquet, blague, bouteille, vape, boîte métallique, cigare, briquet, cendrier, grinder, bourgeon), construit sommet par sommet au chargement — aucune primitive Unity, aucun fichier à installer.
- ✅ **Palier d'arbre de compétences** : « Tabac I » apparaît juste après « Fruits de mer I », coûte 1 point de franchise et exige ce palier.
- ✅ **Prix libres** : les produits tabac passent par la machine à prix comme les autres ; leur prix de base est prérempli.
- ✅ **Sous-sol clandestin** : une salle sous le magasin avec 6 parcelles à cultiver, une trappe discrète à l'entrée du magasin, sur la gauche, un contact du marché noir qui rachète la récolte sur place, un policier qui patrouille et des amendes — jouable en solo comme en multijoueur.
- ✅ **Employés plus logiques** : un produit resté en réserve sans être posé sur une étagère est rangé par un employé de réassort sur l'étagère compatible la plus proche.
- ✅ **Bouton « Sauvegarder et menu »** dans le menu Échap, entre « Sauvegarder et quitter » et « Retour ».
- ✅ **Multiplicateur de fonds** configurable, sans toucher au reste de l'économie.
- ✅ **Raccourcis de confort** : F5 ajoute 50 points de franchise, F6 fixe les fonds à 2 milliards (hôte uniquement), F9 / F10 placent la trappe du sous-sol au centimètre.
- ✅ **Configuration sans recompilation** : chaque module peut être désactivé dans le fichier `.cfg` de BepInEx.
- ✅ **Chargement optimisé** : prefabs, icônes et matériaux du rayon tabac sont générés une seule fois puis réutilisés entre les parties ; les temps sont tracés dans le log (`[Perf]`).

### Le launcher
- ✅ **Détection automatique** du dossier Steam (registre, `libraryfolders.vdf`, chemins classiques), avec sélection manuelle possible.
- ✅ **Installation de BepInEx 5.4.23.5 x64** s'il est absent.
- ✅ **Plug & play** : au premier lancement, BepInEx et le mod s'installent tout seuls depuis la dernière release GitHub, et le launcher se copie dans le profil utilisateur avec ses raccourcis. Un mod plus ancien que la release est mis à jour automatiquement au démarrage suivant.
- ✅ **Installation / mise à jour du mod** depuis la dernière release GitHub, ou depuis un DLL local.
- ✅ **Cases à cocher « Modules au lancement »** : rayon tabac, sous-sol clandestin, réassort intelligent — écrites dans le `.cfg` du mod avant chaque lancement.
- ✅ **Lancement en vanilla** pour une session sans BepInEx, sans rien désinstaller.
- ✅ **Interface avec menu latéral et fond de magasin flouté** : tableau de bord (état, actions, modules, dernière release, gros bouton de lancement), page **Actualités** qui récupère les releases GitHub et met leur Markdown en forme, et page **Journal**.
- ✅ **Boutons dessinés à la main** : coins arrondis transparents, dégradé et liseré sur les actions principales, survol et appui animés, icônes Fluent alignées sur le texte. Les rangées gardent un écart fixe, sans bouton collé ni rogné.
- ✅ **Installation sur le PC** : copie dans le profil utilisateur, icône sur le Bureau, raccourci dans le menu Démarrer et entrée dans « Applications installées » — désinstallation depuis Windows ou depuis le launcher.
- ✅ **Mise à jour du launcher lui-même**, proposée au démarrage avec confirmation ; elle cible la copie installée, donc les raccourcis restent valides.
- ✅ **Journal d'activité** dans la fenêtre, et modes de diagnostic en ligne de commande.

## 🔍 Comment ça marche

### Chargement et patches
Le mod est un plugin BepInEx 5 classique (`Plugin.cs`) : au démarrage du jeu, BepInEx charge `ItemAdder.dll`, Harmony applique les patches et le plugin crée deux objets persistants (`DontDestroyOnLoad`) : le gestionnaire du sous-sol et le bouton du menu Échap.

Les patches Harmony servent uniquement à brancher le mod sur des points du jeu vanilla :

| Patch | Cible | Rôle |
| :--- | :--- | :--- |
| `TobaccoPatches` | `ProductListing`, arbre de compétences, interface du magasin | Ajoute les 44 produits tabac, leur palier et rafraîchit l'interface |
| `UndergroundPatches` / `UndergroundNetworkPatches` / `UndergroundSavePatches` | `ProductListing`, `GameData`, sauvegarde | Ajoute les 2 produits illicites, l'état réseau et la persistance |
| `EmployeeRestockPatch` | `NPC_Manager.ReturnWeightedRestockerTask` | Confie un produit orphelin à un employé de réassort |
| `GameDataPatch` | `GameData.Awake`, `CmdAlterFunds` | Journalise l'état de la partie et applique le multiplicateur de fonds |

### Rayon tabac : données et modèles
Les produits sont ajoutés **à la fin** des tableaux de `ProductListing` (`productsData`, prix joueur, inflation, paliers). Ils sont marqués par leur marque (`Kiosque`) : si une sauvegarde ou une synchronisation les contient déjà, le mod retrouve leurs indices au lieu de les dupliquer.

Conséquences importantes :

- les identifiants exacts dépendent du nombre de produits déjà présents dans ta version du jeu — le log en donne un exemple (`produits 339-382`), ne t'y fie pas dans un script ;
- un joueur **sans le mod** ne connaît pas ces produits : il ne peut pas voir le rayon ni le commander ;
- désactiver le rayon tabac sur une sauvegarde qui en contient déjà peut la rendre illisible (voir [Configuration](#-configuration-du-mod)).

Les maillages sont construits dans `Products/ProductMeshes.cs` à partir de trois briques — `Slab` (coque biseautée), `Lathe` (pièce tournée) et `Blob` (bourgeon) — puis mis en cache par clé : un paquet, une bouteille ou un cigare n'est généré qu'une fois par session.

### Sous-sol : construction, autorité réseau et sauvegarde
Le sous-sol n'est pas une scène : il est assemblé **à partir des prefabs du magasin** (murs, sol, porte, caisse non incluse) sous le bâtiment, dans une salle de 18 × 16 avec 6 parcelles, dont le sol est **7 m sous le plancher** du magasin (plafond juste sous la dalle, soit 6,1 m de hauteur). Le mod repère un emplacement libre et une trappe dans la surface de vente, près de l'entrée et sur la gauche du magasin, à l'intérieur des limites du magasin.

L'hôte est autoritaire : cultures, trappe, réserve et amende sont décidés côté serveur, puis un état compact est envoyé aux clients (parcelles, bâche, ouverture de la trappe, stock en réserve). Chaque client construit le même décor localement pour garder la scène cohérente.

L'état du sous-sol est enregistré séparément de la sauvegarde du jeu, dans :

```text
BepInEx\config\ItemAdder\underground_<nom de la sauvegarde>.txt
```

Ce fichier contient la version du format, l'état de la trappe et, pour chaque parcelle, son état, son heure de plantation, l'arrosage et la bâche. Il est relu au chargement de la partie.

### Employés : réassort
Le jeu vanilla ne demande aux employés de ranger que les produits **déjà affectés à une rangée d'étagère**. Un produit acheté puis stocké en réserve sans avoir été posé n'intéresse donc personne et les employés restent inactifs.

Le patch intervient seulement quand l'employé n'a **aucune tâche vanilla** : il cherche le premier produit en réserve qui n'apparaît sur aucune étagère, réserve une rangée libre de l'étagère du bon type la plus proche et l'envoie la remplir. Le même mécanisme sert au rayon tabac.

### Interface : menu Échap
Le bouton « Sauvegarder et menu » est un clone du bouton « Oui » de la confirmation « Retour au menu principal » : son automate PlayMaker exécute déjà tout le flux du jeu (sauvegarde, fermeture du lobby, arrêt de l'hôte, retour au menu). Le mod le renomme et le replace entre « Sauvegarder et quitter » et « Retour ».

## 🚀 Installation & Lancement

### Prérequis
- **Windows** et la version **Steam** de Supermarket Together.
- **BepInEx 5 x64** dans le dossier du jeu (le launcher l'installe automatiquement s'il est absent).
- Pour compiler : **.NET SDK 8** (ou plus récent) avec le **targeting pack .NET Framework 4.7.2**.

### Launcher (recommandé)
Le launcher est une **application Windows native** (C# WinForms), pas un script PowerShell. L'exécutable publié se trouve dans :

```text
dist\ItemAdderLauncher.exe
```

Double-clique dessus. Au premier lancement, le launcher se contente de faire le nécessaire : il trouve le jeu par Steam, installe **BepInEx** s'il manque, télécharge la **dernière release du mod**, se copie dans le profil utilisateur et crée les raccourcis. Rien à cliquer, rien à configurer.

La fenêtre s'ouvre sur un fond de magasin flouté, avec un **menu latéral** à gauche et trois pages :

| Page | Contenu |
| :--- | :--- |
| **Accueil** | Dossier du jeu + `Parcourir...`, état de l'installation (version du mod, fichier, dernière release, BepInEx, badge « Prêt / Mise à jour / Erreur »), boutons d'installation, modules au lancement, carte « dernière release » et gros bouton **Lancer le jeu** |
| **Actualités** | Les dernières releases GitHub, texte Markdown mis en forme (titres, gras, listes, code, liens) avec date et auteur, bouton **Rafraîchir**. La molette fait défiler la page, même au-dessus du texte |
| **Journal** | Toutes les opérations, les versions, les erreurs réseau et les chemins utilisés, avec **Effacer** |

Les actions de la page Accueil :

| Élément | Rôle |
| :--- | :--- |
| **Installer / Mettre à jour** | Télécharge l'asset du mod depuis la dernière release (jeu fermé obligatoire) |
| **Vérifier les mises à jour** | Recompare le mod installé et la dernière release |
| **Installer un DLL** | Installe un build compilé, pour tester ou réparer |
| **Lancer le jeu** | Applique les modules cochés puis démarre le jeu avec BepInEx |
| **Vanilla** | Démarre le jeu avec `DOORSTOP_DISABLE=TRUE` : BepInEx et les plugins ne se chargent pas pour cette session |
| **BepInEx** | Ouvre `BepInEx\plugins` dans l'Explorateur |
| **Modules au lancement** | Cases à cocher « Rayon Tabac », « Sous-sol clandestin », « Réassort intelligent » |
| **Installer sur le PC** (en bas du menu) | Installe le launcher : icône sur le Bureau, menu Démarrer, entrée « Applications installées ». Le bouton devient **Désinstaller le launcher** une fois l'installation faite |
| **Releases GitHub** (en bas du menu) | Ouvre la page des releases dans le navigateur |

Les modules cochés sont écrits dans le `.cfg` **au moment du clic sur « Lancer le jeu »**, pas avant : tu peux donc tester une configuration sans toucher au fichier. Les choix sont aussi mémorisés dans :

```text
%APPDATA%\ItemAdderLauncher\settings.json
```

Ce fichier contient le chemin du jeu et les trois booléens des modules. Si aucune préférence n'existe encore, le launcher reprend les valeurs déjà présentes dans le `.cfg`.

L'écriture dans le `.cfg` est chirurgicale : seules les clés concernées changent, les commentaires BepInEx et les autres réglages (amende, durée de pousse, multiplicateur) sont conservés. Le lancement vanilla ne modifie ni le `.cfg` ni les modules.

#### Installation sur le PC (icône, menu Démarrer, désinstallation)
L'exécutable peut rester un simple fichier portable, mais le launcher s'installe aussi comme une vraie application : au premier démarrage, il se copie tout seul et crée ses raccourcis (le bouton **« Installer sur le PC »** / **« Désinstaller le launcher »** reste disponible en bas du menu).

- Copie de l'exécutable dans `%LOCALAPPDATA%\Programs\ItemAdder\ItemAdderLauncher.exe` — **aucun droit administrateur** n'est nécessaire. Si tu installes depuis un build multi-fichiers (`bin\...`), le `.dll`, le `.deps.json` et le `.runtimeconfig.json` sont copiés avec lui : sans eux, la copie installée ne démarrerait pas (et exigerait le .NET Desktop Runtime). L'exécutable publié en single-file n'en a pas besoin.
- Raccourci **sur le Bureau** et dans le **menu Démarrer** (`Programs\ItemAdder`), avec l'icône du programme (un caddie ambre, générée pour cette application).
- Entrée dans **« Applications installées »** de Windows : nom, version, éditeur, taille et icône. La désinstallation se lance depuis Windows ou depuis le bouton **« Désinstaller »** du launcher.
- La désinstallation retire les raccourcis, l'entrée Windows et le dossier d'installation. Elle ne touche **ni au mod dans le jeu, ni à `%APPDATA%\ItemAdderLauncher`** : chemin du jeu et modules cochés sont conservés pour une réinstallation.

Installation et désinstallation sont aussi disponibles en ligne de commande, pour un script ou un déploiement silencieux :

```text
ItemAdderLauncher.exe --install --silent
ItemAdderLauncher.exe --uninstall --silent
```

Pour générer l'exécutable depuis les sources :

```bash
dotnet publish ItemAdderLauncher/ItemAdderLauncher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist
```

L'exécutable publié est autonome : le **.NET Desktop Runtime n'est pas requis** sur le PC cible.

#### Modes de diagnostic
Le launcher accepte des arguments de ligne de commande, utiles quand quelque chose ne se détecte pas :

| Commande | Effet |
| :--- | :--- |
| `ItemAdderLauncher.exe --diagnose` | Écrit `launcher-diagnose.txt` : chemin du jeu, validité de l'exe, version du mod installé, BepInEx, dernière release |
| `ItemAdderLauncher.exe --selftest [dll]` | Installe un DLL dans un faux dossier de jeu temporaire et vérifie la copie, la suppression de l'ancien DLL et la comparaison de versions → `launcher-selftest.txt` |
| `ItemAdderLauncher.exe --bepinextest` | Télécharge et extrait BepInEx dans un dossier temporaire pour valider le réseau et l'archive → `launcher-bepinextest.txt` |
| `ItemAdderLauncher.exe --install` | Installe le launcher sur le PC (raccourcis + entrée Windows) et affiche un récapitulatif ; `--silent` supprime les fenêtres |
| `ItemAdderLauncher.exe --uninstall` | Désinstalle le launcher (raccourcis, entrée Windows, dossier d'installation) ; `--silent` supprime les fenêtres |

Les rapports sont écrits dans le dossier courant du launcher.

### Installation manuelle

#### 1. Installer BepInEx
Le script fourni télécharge BepInEx **5.4.23.5 x64** et l'extrait dans le dossier du jeu. Il ne fait rien si BepInEx est déjà présent.

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-BepInEx.ps1 -GamePath "E:\SteamLibrary\steamapps\common\Supermarket Together"
```

Sans `-GamePath`, le script utilise `E:\SteamLibrary\steamapps\common\Supermarket Together`. Adapte le chemin à ton installation Steam.

Installation manuelle possible : télécharge `BepInEx_win_x64_5.4.23.5.zip` depuis les releases BepInEx et extrais son contenu à la racine du jeu (à côté de `Supermarket Together.exe`).

#### 2. Cloner le projet
```bash
git clone https://github.com/BlackAngelTVdev/BasedMod.git
cd BasedMod
```

#### 3. Configurer le chemin du jeu
Ouvre `ItemAdder/ItemAdder.csproj` et adapte la propriété `GameDir` :

```xml
<GameDir>E:\SteamLibrary\steamapps\common\Supermarket Together\</GameDir>
```

Les DLL de référence (`BepInEx.dll`, `0Harmony.dll`, `Assembly-CSharp.dll`, `Unity.TextMeshPro.dll`, `Rewired_Core.dll`, etc.) sont lues depuis ce dossier.

#### 4. Compiler et installer le plugin
```bash
cd ItemAdder
dotnet build -c Release
```

La cible post-build copie automatiquement `ItemAdder.dll` dans `[GameDir]\BepInEx\plugins\ItemAdder.dll` et supprime l'ancien `MonSupermarketMod.dll` pour éviter de charger deux versions.

Si tu préfères installer un DLL déjà compilé, copie simplement `ItemAdder.dll` dans `BepInEx\plugins\`.

#### 5. Lancer le jeu
Démarre Supermarket Together, puis vérifie `BepInEx\LogOutput.log`. Au démarrage, puis au chargement d'une partie Classic, tu dois y voir :

```text
ItemAdder v1.5.3 chargé.
[Tabac] Rayon ajouté : 44 produits, palier 55 ('Tabac I'), groupe 19, produits 339-382. Prérequis : palier 23 ('Fruits de mer I').
[Interface] Bouton « Sauvegarder et menu » ajouté au menu Échap.
[Souterrain] Produits illicites ajoutés : Herbe classique (id 383) et Herbe premium (id 384).
[Souterrain] Sous-sol construit sous le magasin : trappe (5.1, 27.0), salle 18x16, 6 parcelles.
[Souterrain] Sous-sol prêt.
[Souterrain] Policier spawn (netId 33).
```

Les identifiants de produits et de palier peuvent varier selon la version du jeu : ce sont les libellés qui comptent.

## 📖 Utilisation

### Configuration du mod
Au premier lancement, BepInEx crée :

```text
BepInEx\config\com.username.supermarkettogethermod.cfg
```

Le plus simple est de cocher ou décocher les trois modules dans le launcher. Pour les valeurs fines, modifie le fichier puis relance le jeu (la configuration est lue au démarrage) :

```ini
[Général]
MultiplicateurDeFonds = 1

[Tabac]
ActiverRayonTabac = true

[Employés]
ReassortIntelligent = true

[Souterrain]
ActiverSousSol = true
AmendeRaid = 5000
DureePousseHeures = 8
ChanceRaidParInspection = 0.35
```

| Clé | Section | Défaut | Effet |
| :--- | :--- | :---: | :--- |
| `MultiplicateurDeFonds` | `Général` | `1` | Multiplie les fonds ajoutés/retirés via `GameData.CmdAlterFunds` (`1` = vanilla) |
| `ActiverRayonTabac` | `Tabac` | `true` | Ajoute le rayon tabac, ses 44 produits et le palier « Tabac I » |
| `ReassortIntelligent` | `Employés` | `true` | Les employés rangent les produits en réserve absents de toutes les étagères |
| `ActiverSousSol` | `Souterrain` | `true` | Construit le sous-sol, ses parcelles, son rayon et son policier |
| `AmendeRaid` | `Souterrain` | `5000` | Montant retiré des fonds lors d'un raid réussi |
| `DureePousseHeures` | `Souterrain` | `8` | Heures de jeu nécessaires pour qu'une parcelle arrosée arrive à maturité |
| `ChanceRaidParInspection` | `Souterrain` | `0.35` | Probabilité qu'une inspection se transforme en descente |
| `PauseAvantLog` | `Placement` | `true` | **Temporaire (mise au point)** : au F10 du mode placement, fige le jeu pour vérifier l'emplacement ; un second F10 écrit les coordonnées dans le log, F9 annule et relance le temps |

> ⚠️ Le mod ajoute des produits et un palier à la fin des tableaux du jeu. **Tous les joueurs doivent avoir le mod** (et la même version) pour que le multijoueur reste synchronisé. Désactiver `ActiverRayonTabac` ou `ActiverSousSol` après avoir utilisé ces contenus dans une sauvegarde peut la rendre illisible : garde les options actives pour les parties concernées.

### Colis de 50 pièces
Tous les produits commandés arrivent en **colis d'au moins 50 unités**, quel que soit le rayon (produits vanilla, Tabac et herbes du sous-sol).

- Seule la **contenance** change : les prix unitaires, les paliers et les emplacements en rayon restent ceux du jeu.
- Le prix d'un colis vaut `prix unitaire × contenance`, exactement comme en vanilla : une commande coûte donc plus cher, mais chaque pièce garde son prix.
- Un colis existant d'une ancienne sauvegarde reste valide ; il peut simplement être complété jusqu'à 50 pièces.
- Les rayons, réserves et employés fonctionnent normalement : le jeu transfère les pièces par lots de la taille des rangées.

Aucune option de configuration : la règle s'applique à toutes les parties.

### Rayon tabac
1. Débloque d'abord **Fruits de mer I** dans l'arbre de compétences.
2. Achète le nœud **Tabac I** (1 point de franchise, clic sur le nœud), placé à côté du nœud Fruits de mer I.
3. Le groupe **Tabac** apparaît dans le carnet de commandes. Commande les produits, place-les en rayon et vends-les.
4. Les prix se règlent sur la machine à prix comme pour les autres produits.

Le catalogue couvre :

- **Cigarettes et tabac à rouler** : paquets classiques, menthol, blondes, brunes, light et roulées, blagues de tabac, tubes, filtres, filtreuse et feuilles à rouler.
- **Vapes et e-liquides** : vapes jetables, recharges, résistances et e-liquides (tabac, menthe, fruits rouges).
- **Nicotine alternative** : sachets de nicotine (snus/pouches) et tabac chauffé avec ses bâtonnets.
- **Cigares** : cigares, cigare premium, cigarillos vanille, cave à cigares et humidificateur.
- **Accessoires** : briquets, zippo, briquet tempête, cendriers, grinder, filtres, étui à cigarettes et porte-cigarette.

### Sous-sol clandestin

#### Accès
Le sous-sol est construit automatiquement sous le magasin dès que le layout **Classic** est chargé. Une trappe apparaît dans la surface de vente, **près de l'entrée et sur la gauche** du magasin (le mod repère la rue grâce aux points d'apparition des clients), toujours à l'intérieur des limites du magasin, sur un sol libre **et à côté d'une zone marchable** : si aucun point du NavMesh du magasin n'existe à moins de 1,5 m, l'emplacement est rejeté pour que la trappe reste toujours atteignable à pied.

La descente ne dépend d'aucun déclencheur physique (c'était la cause des trappes impossibles à emprunter) :

- **E** sur la trappe fermée : l'ouvrir (un message rappelle qu'il faut appuyer une seconde fois pour descendre).
- **E** sur la trappe ouverte : descendre — le joueur est déposé **au pied de l'échelle**, sous la trappe, face à la salle. Plus d'escalier au milieu de la pièce : le puits se résume à un trou carré dans le plafond, ses quatre parois et une échelle verticale, ce qui évite aussi de voir à travers le plafond.
- **E** au pied de l'échelle, sous l'inscription `[E] Remonter` : remonter sur le plancher du magasin.
- **F** sur la trappe ouverte : la refermer.
- La trappe est aussi un lien de navigation : la police peut l'emprunter quand elle est ouverte.

Si la trappe gêne un passage ou se retrouve contre un meuble, déplace le meuble : le mod cherche un autre emplacement libre au chargement suivant. Pour choisir l'emplacement à la main, voir **F9 / F10** plus bas.

#### Décor et ambiance
La salle de 18 × 16 m est meublée comme un vrai local clandestin, sans aucune ressource externe : les textures (béton, briques, bois, métal, carton) sont générées au chargement.

- **Murs** en béton coulé avec bandeau de briques en partie basse, plinthes métalliques et piliers d'angle.
- **Éclairage** : cinq réglettes néon blanches au plafond et trois **lampes de croissance mauves** suspendues au-dessus des parcelles.
- **Rangement** : rack métallique à deux niveaux au fond, cartons, bâche ; établi avec balance, pots et bocaux contre le mur est.
- **Culture** : cuve d'eau, tuyau d'arrosage, arrosoir et sacs de terre ; marquage jaune au sol autour de la zone de culture.
- **Séchage** : séchoir en bois à trois barres avec têtes suspendues, le long du mur d'entrée.
- **Local technique** : gaine de ventilation avec grilles, extracteur mural, tableau électrique et câbles.
- **Détails** : tapis caoutchouc au pied de l'échelle, grille d'évacuation, flaque, bidons, seau et balai.

Tout est décoratif : les interactions, les six parcelles, la trappe, le contact et la police ne changent pas. Les gros éléments (rack, établi, cuve, séchoir) ont une collision ; les petits détails n'en ont pas pour ne jamais bloquer le joueur ou les PNJ.

#### Culture
Six parcelles, chacune avec son pot et son étiquette d'état.

| | Graine | Prix de base du produit | Récolte |
| :--- | :---: | :---: | :---: |
| Herbe classique | 350 $ | 85 $ | 8 unités |
| Herbe premium | 700 $ | 190 $ | 6 unités |

- **E** sur une parcelle vide : planter (la graine est débitée des fonds).
- **E** sur une parcelle plantée non arrosée : arroser. Sans arrosage, la pousse plafonne à **50 %**.
- La pousse suit les heures de jeu : `DureePousseHeures` (8 h par défaut) après la plantation si la parcelle est arrosée.
- **E** sur une parcelle prête : récolter. La récolte part **dans la réserve du sous-sol** (24 unités maximum par variété) ; si la réserve est pleine, le message te demande de vendre avant de récolter.
- **F** sur une parcelle plantée : bâcher / débâcher. Une parcelle bâchée cache sa plante (utile pendant une inspection).

#### Vente au contact
Plus de rayon improvisé dans le magasin : les herbes ne sont **jamais** mises en rayon, donc plus de clients qui se servent tout seuls sur une étagère comme s'il s'agissait de lessive. La vente se fait au fond du sous-sol, sur une table discrète avec une mallette, marquée `Contact — [E] Vendre la récolte` :

- **E** devant le contact : écoule **toute la réserve** d'un coup et crédite les fonds immédiatement ;
- prix de vente fixes : **85 $** l'herbe classique, **190 $** l'herbe premium ;
- la réserve est sauvegardée avec la partie (fichier du sous-sol), et l'hôte est seul à décider de la vente en multijoueur.

Les anciennes étagères à herbes laissées par une sauvegarde précédente sont supprimées au chargement.

#### Police et amendes
Un agent patrouille dans le magasin. Il effectue des inspections :

- la première a lieu **75 à 150 secondes** après le chargement, puis toutes les **110 à 220 secondes** ;
- une inspection ne peut déboucher sur une descente que si le magasin est **ouvert** et qu'il est entre **8 h et 22 h** ;
- il faut en plus que la **trappe soit ouverte** et qu'il reste soit une **plante non bâchée**, soit de la **récolte en réserve** ;
- la probabilité de descente est alors `ChanceRaidParInspection` (35 % par défaut).

Déroulé d'un raid :

1. message d'alerte, le policier se dirige vers la trappe ;
2. **s'il la trouve fermée, il abandonne** — c'est le moment de refermer derrière toi ;
3. il descend, fouille la salle et, s'il reste une plante visible ou de la récolte en réserve, applique l'amende (`AmendeRaid`, 5 000 $ par défaut), **vide toutes les parcelles et la réserve**, puis remonte.

Une fois en bas, fermer la trappe ne cache plus rien : bâche les plants et vends la réserve au contact avant sa descente.

### Menu Échap
Un bouton **« Sauvegarder et menu »** est ajouté dans le menu Échap, entre « Sauvegarder et quitter » et « Retour ». Il sauvegarde la partie puis revient au menu principal, sans quitter le jeu.

### Raccourcis F5 / F6
En solo ou en tant qu'hôte :

- **F5** : ajoute **50 points de franchise**.
- **F6** : fixe les fonds à **2 000 000 000 $** (argent illimité pour acheter les extensions et casser les murs du magasin).

En multijoueur client, les raccourcis sont ignorés : les points et les fonds doivent être modifiés par l'hôte.

### Placer la trappe (F9 / F10)
Pour choisir l'emplacement exact de la trappe au lieu de le deviner :

1. lance une partie et attends `[Souterrain] Sous-sol prêt.` ;
2. **F9** active le mode placement : un fantôme translucide du sous-sol (cadre de trappe, salle 18 × 16 m, puits d'échelle, 6 parcelles, contact) suit le point que tu vises. Il est dessiné par une caméra de superposition, donc **visible à travers les murs** ; la trappe réelle est masquée pendant le réglage ;
3. vise le sol à l'endroit voulu : l'encadré affiche X / Y / Z et prévient si la salle dépasse des limites du magasin ;
4. **F10** met le jeu en pause pour vérifier l'emplacement : le fantôme et l'encadré des coordonnées restent affichés, le temps est figé (les clients et la police ne bougent plus) ;
5. un **second F10** relance le jeu et écrit la position dans `BepInEx\LogOutput.log` sous la forme
   `[Placement] HatchOverride = new Vector3(5.42f, 0.03f, 27.51f);`, puis quitte le mode ;
6. **F9** à nouveau annule, relance le jeu et réaffiche la trappe réelle.

Le gel avant écriture est un réglage temporaire de mise au point, activé par défaut et désactivable dans le `.cfg` (`[Placement]` → `PauseAvantLog = false`) : F10 écrit alors les coordonnées en une seule fois comme avant.

Pour rendre l'emplacement définitif, remplace la valeur de `HatchOverride` dans `ItemAdder/Underground/UndergroundManager.cs` par le `Vector3` relevé, puis recompile. `Vector3.zero` conserve le placement automatique.

### Multijoueur
1. Installe **BepInEx 5.4.23.5 x64** et le **même** `ItemAdder.dll` (version 1.5.3) dans `BepInEx\plugins` sur le PC de **chaque** joueur.
2. L'hôte lance une partie **Classic** et ouvre son lobby comme d'habitude. Le sous-sol apparaît quand le layout Classic est chargé, avant ou après l'arrivée des amis.
3. Dans le log de l'hôte, vérifie `[Souterrain] Sous-sol prêt.` et `[Souterrain] Policier spawn`.
4. Chez un client, le log doit afficher `[Souterrain] État du sous-sol reçu de l'hôte.` : trappe, parcelles, bâches et stock sont pilotés par l'hôte.

Chaque joueur peut planter, arroser, bâcher et récolter ; l'action est envoyée à l'hôte, qui applique le résultat et renvoie l'état. Le joueur qui agit reçoit aussi ses propres messages. La police, l'amende et l'épuisement du stock sont décidés côté hôte.

> Un joueur **sans le mod** ne verra ni la trappe ni le sous-sol. Ne mélange pas les versions : le jeu peut se désynchroniser si les tableaux de produits ou le nombre de parcelles diffèrent. L'hôte **et les clients** doivent garder `ActiverSousSol = true`.

## 🔄 Mises à jour

Le launcher interroge l'API GitHub à chaque démarrage :

```text
GET https://api.github.com/repos/BlackAngelTVdev/BasedMod/releases/latest
```

Seule la **dernière release publiée** est prise en compte : un brouillon ou une pre-release n'est pas renvoyé par cet endpoint. Une requête par démarrage ; en cas d'abus, GitHub répond « limite de requêtes atteinte » (60 requêtes/heure par IP sans authentification) et le launcher affiche cet état sans bloquer le reste.

### Mise à jour du mod
1. Le launcher récupère le tag (ex. `v1.4.3`) et les assets de la release.
2. Il choisit l'asset : `ItemAdder.dll` en priorité, sinon `MonSupermarketMod.dll`, sinon la première archive `.zip`.
3. Il télécharge dans `%TEMP%`, extrait l'archive si besoin et cherche `ItemAdder.dll`.
4. Il copie le DLL dans `BepInEx\plugins\ItemAdder.dll` via un fichier `.new` renommé (jamais de DLL à moitié écrit), puis supprime l'ancien `MonSupermarketMod.dll`.
5. Il compare la version du DLL installé (lue dans ses métadonnées, donc `<Version>` dans `ItemAdder.csproj`) au numéro du tag. Badge **Prêt** ou **Mise à jour**.

L'installation du mod exige que le jeu soit **fermé** ; le launcher refuse sinon. Sans release disponible, **« Installer un DLL local... »** reste utilisable.

### Mise à jour du launcher
Le launcher se met à jour depuis la même release, via un asset dédié :

1. il cherche un asset dont le nom commence par `ItemAdderLauncher` et finit par `.exe` ;
2. il lit sa version **dans le nom du fichier** (`ItemAdderLauncher-v1.4.0.exe` → 1.4.0), ce qui évite de télécharger l'exécutable pour rien ;
3. si cette version est plus récente que la sienne, une fenêtre propose de télécharger et redémarrer — jamais de remplacement silencieux ;
4. s'il accepte : téléchargement dans `%TEMP%`, relecture de la version **dans le fichier téléchargé** (garde-fou), vérification que le dossier du launcher est accessible en écriture, puis un script `cmd` attend la fermeture du launcher, remplace l'exécutable (60 tentatives à 1 s d'intervalle), redémarre et nettoie le temporaire.

Si le launcher est **installé sur le PC**, deux détails changent :

- la mise à jour cible `%LOCALAPPDATA%\Programs\ItemAdder\ItemAdderLauncher.exe`, même si tu as lancé une copie téléchargée ailleurs. Les raccourcis du Bureau et du menu Démarrer pointent sur ce chemin : ils continuent de fonctionner sans être recréés ;
- au démarrage suivant, la version affichée dans « Applications installées » est réalignée sur l'exécutable réellement présent.

Sans installation, c'est le fichier exécuté qui est remplacé, comme avant. Les restes des mises à jour précédentes (exécutable téléchargé, scripts) sont supprimés du temporaire au démarrage.

Un asset nommé simplement `ItemAdderLauncher.exe` est ignoré pour la vérification et signalé dans le journal. Si le launcher est installé dans un dossier protégé (`Program Files` sans droits), la mise à jour est refusée avec un message explicite avant tout téléchargement.

### Publier une release
1. Incrémente les versions : `<Version>` dans `ItemAdder/ItemAdder.csproj` (mod) et `ItemAdderLauncher/ItemAdderLauncher.csproj` (launcher).
2. Compile les deux (`dotnet publish` pour le launcher, voir plus haut).
3. Crée une **release GitHub** marquée « latest », taguée `vX.Y.Z` avec le numéro du **mod**, et joins deux assets :
   - `ItemAdder.dll` (ou une archive ZIP contenant `ItemAdder.dll` / `MonSupermarketMod.dll`) ;
   - `ItemAdderLauncher-vX.Y.Z.exe` — la version du **launcher** doit rester dans le nom du fichier.

Le tag porte la version du mod, le nom du fichier exécutable porte la version du launcher : les deux numérotations sont indépendantes et c'est normal.

## 🧯 Dépannage

| Symptôme | Cause probable | Solution |
| :--- | :--- | :--- |
| Le launcher ne trouve pas le jeu | Installation hors des chemins Steam habituels | `Parcourir...` et choisis le dossier qui contient `Supermarket Together.exe` |
| « Aucune release GitHub n'est publiée » | Pas de release marquée « latest » | Crée une release avec l'asset `ItemAdder.dll`, ou utilise « Installer un DLL local... » |
| « Ferme Supermarket Together avant d'installer » | Le jeu tourne encore | Ferme le jeu (le DLL est verrouillé par le processus) |
| Badge « Prêt » alors que tu viens de publier | `<Version>` du DLL non incrémentée | Incrémente la version, recompile, republie l'asset |
| Pas de sous-sol | `ActiverSousSol = false` ou layout non-Classic | Coche le module dans le launcher, charge une partie Classic |
| La trappe gêne le passage | Meuble posé dessus après coup | Déplace le meuble ; le mod cherche un autre emplacement au chargement suivant |
| Le contact n'apparaît pas | Sous-sol d'une ancienne version encore chargé | Recharge la partie : le sous-sol est reconstruit au chargement |
| Amende alors que la trappe était fermée | La police était déjà descendue | Referme la trappe **avant** son arrivée, bâche les plants, vide le rayon |
| Le launcher se ferme pendant la mise à jour | Dossier non accessible en écriture | Déplace l'exécutable dans un dossier utilisateur |
| Le raccourci du Bureau pointe vers un ancien fichier | Le launcher a été installé depuis une autre copie | Lance la copie voulue et clique « Installer sur le PC » : les raccourcis sont recréés vers le dossier d'installation |
| Où est passé le launcher installé ? | Installation par utilisateur, sans `Program Files` | `%LOCALAPPDATA%\Programs\ItemAdder\ItemAdderLauncher.exe`, aussi accessible par le raccourci du menu Démarrer |
| `Steamworks is not initialized` dans le log | Le jeu est lancé sans Steam (ou Steam hors ligne) | Lance le jeu depuis Steam ; le mod n'est pas concerné |
| `BoxCollider does not support negative scale` | Avertissement interne du jeu sur ses portes | Sans effet sur le mod |

## 🗂 Structure du projet
```text
BasedMod/
├── Asset/Img/banner.png
├── Install-BepInEx.ps1
├── ItemAdderLauncher/
│   ├── ItemAdderLauncher.csproj
│   ├── app.ico
│   ├── app.manifest
│   ├── Assets/
│   │   ├── background.jpg
│   │   └── logo.png
│   ├── Program.cs
│   ├── MainForm.cs
│   ├── Diagnostics.cs
│   ├── Core/
│   │   ├── GameLocator.cs
│   │   ├── GitHubService.cs
│   │   ├── ModService.cs
│   │   ├── ModConfig.cs
│   │   ├── LauncherInstaller.cs
│   │   ├── LauncherUpdater.cs
│   │   ├── Models.cs
│   │   ├── VersionHelper.cs
│   │   └── LauncherSettings.cs
│   └── UI/
│       ├── Theme.cs
│       ├── BackdropPanel.cs
│       ├── MarkdownRenderer.cs
│       ├── RoundedCheckBox.cs
│       ├── RoundedPanel.cs
│       ├── RoundedButton.cs
│       ├── RoundedTextBox.cs
│       └── WheelThroughRichTextBox.cs
└── ItemAdder/
    ├── Plugin.cs
    ├── Patches/
    │   ├── GameDataPatch.cs
    │   ├── TobaccoPatches.cs
    │   └── EmployeeRestockPatch.cs
    ├── Products/
    │   ├── BoxSizes.cs
    │   └── ProductMeshes.cs
    ├── Tobacco/
    │   ├── TobaccoManager.cs
    │   └── TobaccoSkillNode.cs
    ├── UI/
    │   └── EscapeMenuSaveButton.cs
    ├── Underground/
    │   ├── UndergroundManager.cs
    │   ├── UndergroundBasement.cs
    │   ├── UndergroundCatalog.cs
    │   ├── UndergroundNetwork.cs
    │   ├── UndergroundNetworkPatches.cs
    │   ├── UndergroundPatches.cs
    │   ├── UndergroundPlacementMode.cs
    │   ├── UndergroundSavePatches.cs
    │   ├── BasementDoor.cs
    │   ├── CannabisPlot.cs
    │   └── PoliceVisual.cs
    └── ItemAdder.csproj
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
