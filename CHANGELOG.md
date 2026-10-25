# 📜 Changelog

Toutes les modifications notables du projet ItemAdder (mod BepInEx et launcher Windows).
Format inspiré de [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/), versionnage [SemVer](https://semver.org/lang/fr/).

Le mod et le launcher ont chacun leur numéro de version : le tag d'une release porte celui du mod, l'asset `ItemAdderLauncher-vX.Y.Z.exe` porte celui du launcher.

---

## [Non publié]

### Launcher
- **Version 1.5.1** — asset de release `ItemAdderLauncher-v1.5.1.exe`, exécutable single-file autonome (25 octobre 2026).
- **Première installation automatique** : au lancement, le launcher installe BepInEx 5.4.23.5 s'il manque, puis le mod depuis la dernière release GitHub s'il est absent ou plus ancien — sans question, sans bouton à cliquer. Si le jeu tourne, l'installation est reportée et signalée dans le journal (les fichiers sont verrouillés) ; il suffit de fermer le jeu et de relancer le launcher.
- **Launcher installé dès le premier lancement** : plus de boîte de dialogue « installer sur ce PC ? ». L'exécutable se copie dans `%LOCALAPPDATA%\Programs\ItemAdder` et crée ses raccourcis Bureau / menu Démarrer tout seul ; le bouton du menu reste là pour désinstaller.
- **Mises à jour du mod automatiques** : un mod plus ancien que la dernière release est remplacé au démarrage, sans intervention. Un DLL local plus récent que la release n'est jamais remplacé par une version inférieure.
- **Boutons refaits** : coins arrondis réellement transparents (le fond du parent est repeint sous les angles, plus de triangles ni de marches), dégradé et liseré sur les actions principales, transition douce au survol, état enfoncé et état désactivé lisibles. Icône et libellé sont mesurés ensemble et centrés, sans texte tronqué.
- **Icônes nettes** : police d'icônes Fluent (repli MDL2 sous Windows 10) et glyphes choisis pour être reconnaissables — dossier du jeu, bouclier d'état, engrenage des modules, étiquette de release, historique du journal ; le dossier et le document ne s'affichent plus comme des rectangles vides.
- **Rangées de boutons corrigées** : colonnes d'écart explicites, donc plus de boutons collés, plus de coin en créneau entre **Vanilla** et **BepInEx**, et plus de bouton rogné à droite quand la fenêtre est petite.
- **Nouvelle interface** : fond illustré (rayons de magasin flous, lumières chaudes, généré par le projet), **menu latéral** (Accueil, Actualités, Journal), cartes vitrées et gros bouton de lancement. La fenêtre passe à 1120 × 694.
- **Page « Actualités »** : les six dernières releases GitHub sont récupérées et leur texte Markdown est rendu sur place — titres, gras, italique, listes, blocs de code, code en ligne et liens — avec date, auteur et badge de tag. Bouton **Rafraîchir** et lien vers GitHub.
- **Tableau de bord** : carte « Dernière release » cliquable, cases à cocher redessinées (coche ambre), état d'installation et modules regroupés par cartes.
- **Installation sur le PC** : le launcher s'installe dans le profil utilisateur (`%LOCALAPPDATA%\Programs\ItemAdder`), sans droit administrateur. Bouton **« Installer sur le PC »** en haut de la fenêtre, proposition au premier démarrage, puis **« Désinstaller »**.
- **Icône et raccourcis réels** : icône du programme (caddie ambre) intégrée à l'exécutable, raccourci sur le **Bureau** et dans le **menu Démarrer**, entrée complète dans **« Applications installées »** (nom, version, éditeur, taille, icône, `UninstallString`).
- **Mises à jour alignées sur l'installation** : la mise à jour du launcher cible désormais l'exécutable installé, donc les raccourcis restent valides après remplacement ; la version affichée dans Windows est réalignée au démarrage suivant. Les restes des mises à jour précédentes sont nettoyés du dossier temporaire.
- **Installation depuis un build corrigée** : l'installateur copie aussi `ItemAdderLauncher.dll`, `.deps.json` et `.runtimeconfig.json` quand ils accompagnent l'exécutable. Auparavant, installer depuis `bin\...` ne copiait que l'exe de 177 Ko, que Windows listait dans le menu Démarrer sans pouvoir le lancer. Une installation single-file retire au contraire ces fichiers devenus inutiles.
- **Ligne de commande** : `--install` et `--uninstall`, avec `--silent` pour les scripts et pour l'entrée de désinstallation de Windows.

### Mod
- **Escalier remplacé par un puits et une échelle (1.5.3)** : plus d'escalier au milieu de la pièce. Le plafond n'a plus qu'un trou carré de 3 × 3 m au-dessus de la trappe, fermé par quatre parois qui relient le plafond à la dalle du magasin (on ne voit plus à travers le plafond), avec une échelle métallique jusqu'au sol. La descente et la remontée restent des interactions (E).
- **Sous-sol plus profond (1.5.2)** : la salle passe de 4,2 m à **7 m sous le plancher du magasin** (plafond inchangé, juste sous la dalle), soit 6,1 m de hauteur de mur. L'escalier compte 26 marches, les lampes de croissance restent à hauteur de plantes et les lampes de plafond voient leur portée passer à 14 m pour éclairer le sol. Le fantôme du mode placement (F9) suit la nouvelle profondeur.
- **Sous-sol éclairé et murs lisibles (1.5.1)** : les murs restaient noirs par plaques (URP ne rend qu'un nombre limité de lumières additionnelles par objet), si bien que seuls des morceaux de briques éclairés ressortaient, comme des blocs isolés au milieu de la salle. Le bardage brique passe de 1,36 m à 2,3 m de haut et les matériaux texturés reçoivent une auto-illumination de 9 % ; les bétons sont légèrement plus clairs et le contact a sa propre lampe.
- **Vente au contact, plus de rayon à herbes (1.5.0)** : la récolte n'est plus posée sur une étagère du magasin où les clients se servaient tout seuls. Elle reste dans une réserve (24 unités par variété), sauvegardée et synchronisée, et s'écoule au **contact** du sous-sol (table + mallette, `[E] Vendre la récolte`) à prix fixe : 85 $ l'herbe classique, 190 $ la premium, paiement immédiat. Les herbes ne sont plus proposées aux clients, donc plus de clients qui les cherchent en rayon ; les étagères à herbes laissées par les anciennes sauvegardes sont supprimées au chargement. Le stock en réserve compte comme preuve lors d'un raid.
- **Plus de mur au milieu de la salle (1.4.5)** : les deux parois de 3,3 m de haut qui encadraient l'escalier traversaient la pièce sur 8 m et la coupaient en deux (la trappe est au centre de la salle en X). Elles sont remplacées par des garde-corps de 1,25 m qui suivent la pente de la rampe. Deux lampes de plus éclairent l'escalier et le palier, et le log affiche désormais l'emprise exacte de la salle et les limites du magasin (`Limites du magasin : X … Z …`).
- **Descente refaite (1.4.4)** : plus aucun déclencheur physique autour de la trappe. **E** descend et dépose le joueur sur le palier du bas, dans la salle (et non au milieu de la rampe, où il glissait et se coinçait) ; **E** sur le palier, marqué `[E] Remonter`, ramène sur le plancher du magasin. L'emplacement de la trappe doit désormais avoir un point marchable du NavMesh à moins de 1,5 m, sinon il est rejeté : la trappe reste atteignable. Escalier élargi à 3,8 m, palier agrandi.
- **Version 1.4.3** : le log de démarrage contient `ItemAdder v1.4.3 chargé.` puis `[Placement] Module chargé (1.4.3)`, ce qui permet de vérifier que c'est bien ce DLL qui tourne et non l'ancienne release.
- **Pause avant le log du placement** (temporaire, mise au point) : au premier **F10**, le jeu se fige (`Time.timeScale = 0`), le fantôme et les coordonnées restent affichés pour vérifier l'emplacement sans que clients ni police ne bougent ; un second **F10** relance le temps et écrit la position dans le log, **F9** annule et relance le temps. Réglable par `[Placement] PauseAvantLog` (défaut `true`), à retirer avec le reste de l'outil.
- **Mode placement de la trappe (F9 / F10)** : un fantôme translucide du sous-sol (salle, rampe, parcelles, rayon) suit le point visé et reste visible à travers les murs, la trappe réelle étant masquée pendant le réglage. **F10** écrit la position dans le log sous forme de `HatchOverride` prêt à coller dans le code ; **F9** annule.
- **Sous-sol retravaillé** (même emprise, 18 × 16 m, mêmes 6 parcelles et interactions) : textures procédurales de béton, briques, bois, métal et carton ; bandeau de briques, plinthes et piliers ; réglettes néon et lampes de croissance mauves au-dessus des plants ; rack de stockage, établi, séchoir, cuve d'eau et tuyau d'arrosage ; gaine de ventilation et extracteur ; garde-corps d'escalier, tapis, grille d'évacuation et marquage au sol. Le décor est purement visuel, avec collision limitée aux gros éléments.
- **Colis de 50 pièces minimum** : tous les produits commandés dans le carnet arrivent en colis d'au moins 50 unités (vanilla, rayon Tabac et herbes du sous-sol). Seule la contenance change ; les prix unitaires, les paliers et les rayons sont identiques. Un colis coûte donc `prix unitaire × contenance`, comme dans le jeu vanilla.

---

## [1.4.2] — 2026-10-24 · à publier

### Mod
- **Réassort intelligent** (`ReassortIntelligent`, section `Employés`) : quand un employé de réassort n'a aucune tâche, il prend le premier produit resté en réserve et absent de toutes les étagères, réserve une rangée libre de l'étagère compatible la plus proche et va la remplir. Le jeu vanilla ne s'occupait que des produits déjà affectés à une rangée.
- Le même mécanisme couvre les produits du rayon Tabac et les herbes du sous-sol, côté hôte uniquement.

### Launcher
- **Version 1.3.0**
- **Cases à cocher « Modules au lancement »** : rayon tabac, sous-sol clandestin, réassort intelligent. Les valeurs sont écrites dans `BepInEx\config\com.username.supermarkettogethermod.cfg` au clic sur « Lancer le jeu », sans toucher aux commentaires ni aux autres clés.
- Les choix sont mémorisés dans `%APPDATA%\ItemAdderLauncher\settings.json`, avec le chemin du jeu ; si aucune préférence n'existe, le launcher relit le `.cfg`.
- **Mise à jour automatique du launcher** : au démarrage, il compare sa version à celle de l'asset `ItemAdderLauncher-vX.Y.Z.exe` de la dernière release, propose le téléchargement, remplace l'exécutable après sa fermeture et le redémarre. Aucun remplacement sans confirmation.
- Vérification des droits d'écriture du dossier avant tout téléchargement.
- Le bouton « Lancer en vanilla » ne modifie ni le `.cfg` ni les modules.

### Documentation
- README complet : fonctionnement interne du mod, culture et raids du sous-sol, tableau exhaustif de la configuration, chaque bouton du launcher, modes de diagnostic, procédure de publication d'une release et dépannage.

**Commits :** `85edfaf`, `d06eff9`, `1453577`

---

## [1.3.5] — 2026-10-24

### Mod
- **Modèles 3D générés** pour les 44 produits du rayon tabac : coques biseautées (`Slab`), pièces tournées (`Lathe`) et bourgeon (`Blob`), générés sommet par sommet et mis en cache. Fini les cubes et cylindres redimensionnés, toujours sans asset externe.
- **Bouton « Sauvegarder et menu »** dans le menu Échap, entre « Sauvegarder et quitter » et « Retour » ; il réutilise le flux PlayMaker du jeu pour fermer proprement le lobby et sauvegarder.
- **Sous-sol fiabilisé** :
  - la caisse du sous-sol et les ventes dans la salle sont supprimées (clients bloqués) ;
  - la récolte part dans un **rayon à herbes à l'étage**, posé à côté d'un rayon existant ;
  - la demande des clients suit le **stock réel** du rayon, y compris pour les listes de courses déjà en cours ;
  - la **trappe** est replacée dans la surface de vente, côté rue, dans les limites du magasin ;
  - les copies sauvegardées du rayon à herbes sont supprimées au chargement et leur stock est transféré sur le rayon recréé.

**Commits :** `c878a8d`, `0658ca4`, `b75eef5`

---

## [1.3.4] — 2026-10-24

### Mod
- **Sous-sol clandestin** sous le magasin (layout Classic) : salle de 18 × 16, 6 parcelles de culture, trappe avec ouverture/fermeture (**E**) et descente en marchant dessus, bâche (**F**) pour cacher une parcelle.
- Deux produits illicites ajoutés aux tableaux de `ProductListing` : **Herbe classique** (graine 350 $, prix de base 85 $, récolte de 8 unités) et **Herbe premium** (graine 700 $, prix de base 190 $, récolte de 6 unités).
- Pousse basée sur les heures de jeu (`DureePousseHeures`, 8 h par défaut), plafonnée à 50 % sans arrosage.
- **Policier** qui patrouille : inspections entre 8 h et 22 h quand le magasin est ouvert, descente possible si la trappe est ouverte et qu'une plante ou du stock est visible ; amende (`AmendeRaid`, 5 000 $) et confiscation des plants et du stock.
- **Synchronisation multijoueur** : l'hôte est autoritaire (cultures, trappe, stock, amende), les clients reçoivent un état compact et construisent le décor localement.
- Sauvegarde de l'état du sous-sol dans `BepInEx\config\ItemAdder\underground_<sauvegarde>.txt`.
- **F6** : fixe les fonds à 2 000 000 000 $ pour acheter les extensions et casser les murs (hôte uniquement).

**Commit :** `cd6c854`

---

## [1.2.1] — 2026-10-23

### Mod
- **Mise en cache des prefabs, icônes et matériaux** du rayon tabac : un produit n'est construit qu'une fois par processus, puis réutilisé même si `ProductListing` est recréé au chargement d'une autre partie. Le log indique `[Tabac] Catalogue prêt en N ms`.
- **Mesure `[Perf]`** du chargement de la sauvegarde, pour distinguer le coût du mod de celui des données vanilla.
- Palette de détails partagée pour les étiquettes, capsules et liserés des produits.

### Launcher
- **Version 1.2.0**
- Nouveau bouton **« Lancer en vanilla »** : démarre le jeu avec `DOORSTOP_DISABLE=TRUE`, sans désinstaller BepInEx ni le mod.

**Commit :** `dd132e2`

---

## [1.2.0] — 2026-10-23

### Mod
- **Rayon tabac étendu à 44 produits** avec visuels dédiés : cigarettes, tabac à rouler, tubes et filtres, vapes et e-liquides, snus, tabac chauffé, cigares et accessoires.
- Nouveau groupe **Tabac** dans le carnet de commandes, palier **« Tabac I »** (1 point de franchise, après « Fruits de mer I ») et prix de base préremplis.

### Launcher
- **Version 1.1.0** — première version du launcher Windows (WinForms) :
  - détection du jeu via le registre Steam et `libraryfolders.vdf` ;
  - installation de BepInEx 5.4.23.5 x64 s'il est absent ;
  - installation et mise à jour du mod depuis la dernière release GitHub, ou depuis un DLL local ;
  - comparaison de la version installée avec le tag de la release ;
  - ouverture de `BepInEx\plugins`, journal d'activité et modes de diagnostic `--diagnose`, `--selftest`, `--bepinextest`.

**Commit :** `5370931`

---

## [1.1.0] — 2026-10-23

### Mod
- Remplacement des réglages de déplacement du joueur par un **premier rayon tabac** (4 produits), son groupe dans le carnet de commandes et le nœud **« Tabac I »** dans l'arbre de compétences.
- Les tableaux de produits et de paliers sont étendus **à la fin** pour conserver les indices existants des sauvegardes.
- **F5** : ajoute 50 points de franchise (hôte uniquement).
- Positionnement du nœud Tabac I sous l'étoile décorative, à côté de « Fruits de mer I ».
- Renommage du mod en **ItemAdder**.

**Commits :** `d9c5a55`, `9772a77`, `98cbea4`

---

## [1.0.0] — 2026-10-23

### Mod
- Premier plugin BepInEx 5 pour Supermarket Together (net472) :
  - multiplicateur de fonds configurable via un patch Harmony sur `GameData.CmdAlterFunds` ;
  - réglages de vitesse et de hauteur de saut du joueur par patchs configurables.
- Script `Install-BepInEx.ps1` pour installer BepInEx 5.4.23.5 x64 dans le dossier du jeu.
- Copie automatique du DLL dans `BepInEx\plugins` après chaque build.

### Documentation
- Premier README : installation de BepInEx, compilation, déploiement, fichier de configuration généré et structure du projet.

**Commits :** `cba1936`, `06a1dea`

---

## Types de changements

- **Mod** : `ItemAdder.dll`, chargé par BepInEx.
- **Launcher** : `ItemAdderLauncher.exe`, application Windows autonome.
- **Documentation** : README, changelog et guides.

## Notes de compatibilité

- Le mod ajoute des produits et un palier à la fin des tableaux du jeu : **tous les joueurs doivent avoir la même version** du DLL pour rester synchronisés en multijoueur.
- Désactiver `ActiverRayonTabac` ou `ActiverSousSol` sur une sauvegarde qui utilise déjà ces contenus peut la rendre illisible.
- Les numéros de produits et de palier affichés dans les logs dépendent de la version du jeu ; seuls les libellés sont stables.
