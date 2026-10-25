using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using HutongGames.PlayMaker;
using Mirror;
using Rewired;
using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace ItemAdder.Underground
{
    internal sealed class UndergroundManager : MonoBehaviour
    {
        internal static UndergroundManager Instance { get; private set; }

        internal const int PlotCount = 6;

        private BasementLayout _layout;
        private CannabisPlot[] _plots;
        private BasementDoor _door;

        // Récolte en réserve : elle n'est plus vendue sur un rayon du magasin
        // mais au contact, dans le sous-sol (index 0 = classique, 1 = premium).
        private readonly int[] _stock = new int[2];

        // Ancien rayon d'herbes : plus utilisé, conservé pour que les étagères
        // laissées par une ancienne sauvegarde puissent être retrouvées puis
        // supprimées au chargement.
        private Data_Container _shelf;
        private const int StockCapacity = 24;
        private GameObject _police;
        private NavMeshAgent _policeAgent;
        private uint _policeNetId;

        private LayoutReferences _layoutRef;
        private GameData _gameData;
        private bool _built;
        private bool _networkReady;
        private bool _dirty;
        private bool _raidActive;
        private bool _doorOpen = true;
        private float _nextInspection;
        private int _raidPhase;
        private float _raidPhaseStarted;
        private float _lastPoliceRecovery;
        private float _nextSave;
        private float _nextHello;
        private float _nextBuildAttempt;
        private float _nextSpawnRetry;
        private float _nextPrefabWarning;
        private float _nextFallCheck;
        private float _nextLeftoverCheck;
        private float _nextClientResync;
        private float _nextPoliceVisualAttempt;
        private bool _policeVisualAttached;
        private bool _wasConnectedClient;
        private float _lastNotify;
        private int _patrolIndex;
        private Vector3[] _patrolPoints;
        private Vector2 _shopX;
        private Vector2 _shopZ;

        internal bool IsServer => NetworkServer.active;

        /// <summary>
        /// Position définitive de la trappe, relevée avec le mode placement
        /// (F9 puis F10 dans le jeu). Vector3.zero = placement automatique
        /// via FindHatchPosition.
        /// </summary>
        internal static readonly Vector3 HatchOverride = Vector3.zero;

        internal BasementLayout Layout => _layout;

        internal bool HasLayout => _built && _layout != null;

        /// <summary>Limites de construction du magasin en X (x = min, y = max).</summary>
        internal Vector2 ShopX => _shopX;

        /// <summary>Limites de construction du magasin en Z (x = min, y = max).</summary>
        internal Vector2 ShopZ => _shopZ;

        internal bool IsLocalPlayerInBasement
        {
            get
            {
                FirstPersonController player = FirstPersonController.Instance;
                return player != null && IsInBasement(player.transform.position);
            }
        }

        /// <summary>
        /// Masque ou réaffiche la trappe réelle (mode placement).
        /// </summary>
        internal void SetDoorVisible(bool visible)
        {
            if (_door == null)
                return;

            GameObject doorObject = _door.gameObject;
            if (doorObject != null && doorObject.activeSelf != visible)
                doorObject.SetActive(visible);
        }

        internal static bool IsInBasement(Vector3 position)
        {
            UndergroundManager manager = Instance;
            if (manager == null || manager._layout == null)
                return false;

            BasementLayout layout = manager._layout;
            return position.y < -0.6f &&
                   position.y > layout.FloorY - 2.0f &&
                   position.x > layout.MinX - 1.5f &&
                   position.x < layout.MaxX + 1.5f &&
                   position.z > layout.MinZ - 1.5f &&
                   position.z < layout.MaxZ + 1.5f;
        }

        internal static bool HasBasementFloorBelow(Vector3 position)
        {
            UndergroundManager manager = Instance;
            if (manager == null || manager._layout == null)
                return false;

            if (!Physics.Raycast(position + Vector3.up * 0.6f, Vector3.down,
                    out RaycastHit hit, 2.0f, ~0, QueryTriggerInteraction.Ignore))
                return false;

            return hit.collider == manager._layout.FloorCollider ||
                   hit.collider == manager._layout.RampCollider;
        }

        internal bool HasClassicStock => _stock[0] > 0;

        internal bool HasPremiumStock => _stock[1] > 0;

        internal int ClassicStock => _stock[0];

        internal int PremiumStock => _stock[1];

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            UndergroundNetwork.Register();
        }

        private void Update()
        {
            bool connectedClient = NetworkClient.isConnected && !NetworkServer.active;
            if (connectedClient && !_wasConnectedClient)
            {
                // Nouvelle connexion : on redemande un état complet à l'hôte.
                // On réinstalle aussi les handlers, car Mirror a pu vider son
                // dictionnaire au chargement de scène après le chargement du mod.
                UndergroundNetwork.InstallClientHandlers();
                _networkReady = false;
                _policeVisualAttached = false;
                _nextHello = 0f;
                _nextClientResync = 0f;
            }
            _wasConnectedClient = connectedClient;

            if (!connectedClient && !NetworkServer.active)
            {
                _networkReady = false;
                _policeVisualAttached = false;
            }

            if (GameData.Instance == null || LayoutReferences.Instance == null)
                return;

            if (LayoutReferences.Instance.layoutIndex != 0)
            {
                if (_built)
                    DestroyBasement();
                return;
            }

            if (!_built)
            {
                if (Time.time >= _nextBuildAttempt)
                {
                    _nextBuildAttempt = Time.time + 0.5f;
                    TryBuild();
                }
                return;
            }

            if (_layoutRef != LayoutReferences.Instance || _gameData != GameData.Instance)
            {
                DestroyBasement();
                return;
            }

            if (!_networkReady && NetworkServer.active)
            {
                UndergroundNetwork.InstallClientHandlers();
                UndergroundNetwork.InstallServerHandlers();
                SpawnNetworkObjects();
                LoadState();
                _networkReady = true;
                _nextSpawnRetry = Time.time + 4f;
            }
            else if (!NetworkServer.active && NetworkClient.isConnected)
            {
                if (!_networkReady)
                {
                    if (Time.time >= _nextHello)
                    {
                        _nextHello = Time.time + 2f;
                        NetworkClient.Send(new UndergroundHelloMessage());
                    }
                }
                else if (Time.time >= _nextClientResync)
                {
                    // Resynchronisation périodique : si un message a été manqué
                    // (ou si l'hôte a reconstruit son sous-sol), le client se remet à jour.
                    _nextClientResync = Time.time + 30f;
                    NetworkClient.Send(new UndergroundHelloMessage());
                }
            }
            else if (_networkReady && NetworkServer.active && Time.time >= _nextSpawnRetry)
            {
                // Les prefabs du jeu ne sont pas toujours prêts au premier tick :
                // on retente tant que le rayon ou le policier manque.
                _nextSpawnRetry = Time.time + 4f;
                if (_police == null)
                    SpawnNetworkObjects();
            }

            if (!IsServer && _policeNetId != 0 && !_policeVisualAttached &&
                Time.time >= _nextPoliceVisualAttempt)
            {
                _nextPoliceVisualAttempt = Time.time + 1f;
                AttachPoliceVisual(_policeNetId);
            }

            HandleLocalInput();
            TickFallRecovery();
            UpdatePlots();
            TickLeftoverCleanup();
            TickPolice();
            TickSave();
        }

        private void OnDestroy()
        {
            if (IsServer)
                SaveState();

            if (Instance == this)
                Instance = null;
        }

        private void TryBuild()
        {
            if (ProductListing.Instance == null ||
                GameData.Instance.GetComponent<NetworkSpawner>() == null ||
                NPC_Manager.Instance == null)
                return;

            LayoutManager layoutManager = Object.FindFirstObjectByType<LayoutManager>();
            if (layoutManager == null || !layoutManager.hasFinished)
                return;

            Builder_Main builder = Object.FindFirstObjectByType<Builder_Main>();
            if (builder == null || builder.buildXLimits.y <= builder.buildXLimits.x ||
                builder.buildZLimits.y <= builder.buildZLimits.x)
                return;

            if (!UndergroundCatalog.TrySetup(ProductListing.Instance))
                return;

            float minX = builder.buildXLimits.x;
            float maxX = builder.buildXLimits.y;
            float minZ = builder.buildZLimits.x;
            float maxZ = builder.buildZLimits.y;
            _shopX = new Vector2(minX, maxX);
            _shopZ = new Vector2(minZ, maxZ);
            Plugin.Log.LogInfo(
                $"[Souterrain] Limites du magasin : X {minX:0.0} → {maxX:0.0}, " +
                $"Z {minZ:0.0} → {maxZ:0.0}.");

            // La trappe reste dans la surface de vente. Posée dehors, sur le
            // trottoir, elle perçait le décor et le joueur restait coincé dans
            // la géométrie du sous-sol. Une position relevée en mode placement
            // (F9 / F10) prend le dessus.
            bool overridden = HatchOverride != Vector3.zero;
            Vector3 hatch = overridden
                ? HatchOverride
                : FindHatchPosition(minX, maxX, minZ, maxZ);
            Plugin.Log.LogInfo(
                overridden
                    ? $"[Souterrain] Trappe placée en dur à ({hatch.x:0.0}, {hatch.z:0.0})."
                    : $"[Souterrain] Trappe placée à l'entrée, à gauche du magasin " +
                      $"({hatch.x:0.0}, {hatch.z:0.0}).");

            string floorTag = "";
            int floorLayer = 0;
            if (Physics.Raycast(hatch + Vector3.up * 2.5f, Vector3.down,
                    out RaycastHit floorHit, 3.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                floorTag = floorHit.collider.gameObject.tag;
                floorLayer = floorHit.collider.gameObject.layer;
            }

            int agentTypeId = 0;
            Transform agentSample = NPC_Manager.Instance.transform.Find("AgentSample");
            if (agentSample != null)
            {
                NavMeshAgent sample = agentSample.GetComponent<NavMeshAgent>();
                if (sample != null)
                    agentTypeId = sample.agentTypeID;
            }

            _layout = UndergroundBasement.Build(
                hatch, agentTypeId, floorTag, floorLayer,
                UndergroundCatalog.ClassicSeedPrice,
                UndergroundCatalog.PremiumSeedPrice,
                UndergroundCatalog.ClassicProductId,
                UndergroundCatalog.PremiumProductId);

            _plots = _layout.Plots;
            _door = _layout.Door;
            _doorOpen = true;
            _door.SetOpen(true);

            for (int i = 0; i < _plots.Length; i++)
                _plots[i].RefreshVisual();

            _layoutRef = LayoutReferences.Instance;
            _gameData = GameData.Instance;
            _built = true;
            _nextInspection = Time.time + UnityEngine.Random.Range(75f, 150f);
            _nextSave = Time.time + 12f;
            _nextClientResync = Time.time + 30f;

            Notify("Le sous-sol clandestin est aménagé. Trappe à l'entrée, à gauche du magasin.");
            Plugin.Log.LogInfo("[Souterrain] Sous-sol prêt.");

            if (NetworkServer.active)
            {
                SpawnNetworkObjects();
                LoadState();
                _networkReady = true;
            }
        }

        /// <summary>
        /// Choisit un emplacement de trappe **à l'intérieur** du magasin, près
        /// de l'entrée et sur la gauche de la surface de vente. La direction
        /// de la rue vient des points d'apparition des clients (sur le
        /// trottoir). Une grille de candidats est ensuite parcourue depuis ce
        /// point, et on retient le premier sol praticable qui laisse la place
        /// au cadre de 3,3 m de la trappe. En dernier recours, le centre du
        /// magasin.
        /// </summary>
        private static Vector3 FindHatchPosition(float minX, float maxX, float minZ, float maxZ)
        {
            float centerX = (minX + maxX) * 0.5f;
            float centerZ = (minZ + maxZ) * 0.5f;
            Vector3 center = new Vector3(centerX, 0f, centerZ);

            // Trop petit pour placer une trappe de 3,3 m avec ses marges :
            // on retombe sur le centre du magasin (comme les versions
            // précédentes, qui ne posaient pas de problème).
            if (maxX - minX < 7f || maxZ - minZ < 7f)
                return center;

            const float margin = 2.2f;

            // Direction de la rue : les clients apparaissent sur le trottoir,
            // devant la façade.
            Vector3 roadDirection = GetRoadDirection(center);
            Vector3 anchor = center;
            if (roadDirection != Vector3.zero)
            {
                Vector3 right = new Vector3(roadDirection.z, 0f, -roadDirection.x);

                // Contre l'entrée, sur la gauche : la trappe est juste à côté
                // de la porte, donc facile à trouver et à refermer derrière soi.
                anchor = center
                         + roadDirection * ((maxZ - minZ) * 0.30f)
                         - right * ((maxX - minX) * 0.26f);

                anchor = new Vector3(
                    Mathf.Clamp(anchor.x, minX + margin, maxX - margin),
                    0f,
                    Mathf.Clamp(anchor.z, minZ + margin, maxZ - margin));
            }

            const float step = 1.4f;

            List<Vector3> candidates = new List<Vector3>();
            for (float x = minX + margin; x <= maxX - margin; x += step)
            {
                for (float z = minZ + margin; z <= maxZ - margin; z += step)
                    candidates.Add(new Vector3(x, 0f, z));
            }

            if (candidates.Count == 0)
                return center;

            candidates.Sort((left, right) =>
            {
                float leftDistance = (left - anchor).sqrMagnitude;
                float rightDistance = (right - anchor).sqrMagnitude;
                return leftDistance.CompareTo(rightDistance);
            });

            // Premier passage : il faut un sol marchable (NavMesh du magasin)
            // juste à côté, sinon la trappe est inatteignable.
            for (int i = 0; i < candidates.Count; i++)
            {
                if (TryFloorPoint(candidates[i], out Vector3 reachable, 1.8f, true))
                    return reachable;
            }

            // Passages suivants : dégagement large puis minimal.
            float[] clearances = { 2.0f, 1.4f };
            for (int pass = 0; pass < clearances.Length; pass++)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (TryFloorPoint(candidates[i], out Vector3 floor, clearances[pass]))
                        return floor;
                }
            }

            return center;
        }

        /// <summary>
        /// Direction horizontale allant du magasin vers la rue, déduite des
        /// points d'apparition des clients (sur le trottoir devant la façade).
        /// Vector3.zero si l'information manque.
        /// </summary>
        private static Vector3 GetRoadDirection(Vector3 center)
        {
            NPC_Manager manager = NPC_Manager.Instance;
            GameObject spawnPoints = manager != null ? manager.spawnPointsOBJ : null;
            if (spawnPoints == null || spawnPoints.transform.childCount == 0)
                return Vector3.zero;

            Vector3 sum = Vector3.zero;
            int count = 0;
            int childCount = spawnPoints.transform.childCount;

            for (int i = 0; i < childCount; i++)
            {
                Transform child = spawnPoints.transform.GetChild(i);
                if (child == null)
                    continue;

                // Le jeu tire ses points d'apparition dans [0, childCount - 1) :
                // le dernier enfant sert de gabarit, on ne le compte pas.
                if (i == childCount - 1 && count > 0)
                    continue;

                sum += child.position;
                count++;
            }

            if (count == 0)
                return Vector3.zero;

            Vector3 spawn = sum / count;
            Vector3 direction = new Vector3(spawn.x - center.x, 0f, spawn.z - center.z);
            if (direction.sqrMagnitude < 1f)
                return Vector3.zero;

            return direction.normalized;
        }

        private static bool TryFloorPoint(
            Vector3 candidate, out Vector3 floor, float clearance = 1.35f,
            bool requireNavMesh = false)
        {
            floor = Vector3.zero;

            if (!Physics.Raycast(candidate + Vector3.up * 2.5f, Vector3.down,
                    out RaycastHit hit, 3.5f, ~0, QueryTriggerInteraction.Ignore))
                return false;

            if (hit.point.y < -1f || hit.point.y > 1f || hit.normal.y < 0.4f)
                return false;

            Collider[] overlaps = Physics.OverlapBox(
                candidate + Vector3.up * 1.0f,
                new Vector3(clearance, 0.55f, clearance),
                Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider other = overlaps[i];
                if (other == null || other.isTrigger)
                    continue;
                if (other.GetComponentInParent<FirstPersonController>() != null)
                    continue;
                // Les PNJ se déplacent d'un client à l'autre : ils ne doivent
                // pas changer l'emplacement de la trappe, sinon les sous-sols
                // construits localement ne se superposent plus en multi.
                if (other.GetComponentInParent<NPC_Info>() != null)
                    continue;

                return false;
            }

            if (requireNavMesh &&
                !NavMesh.SamplePosition(new Vector3(candidate.x, hit.point.y, candidate.z),
                    out NavMeshHit _, 1.5f, NavMesh.AllAreas))
            {
                // Aucun sol marchable juste à côté : le joueur ne pourrait pas
                // s'approcher de la trappe pour appuyer sur E.
                return false;
            }

            floor = new Vector3(candidate.x, 0f, candidate.z);
            return true;
        }

        private void SpawnNetworkObjects()
        {
            if (!NetworkServer.active)
                return;

            SpawnPolice();
        }

        /// <summary>
        /// ANCIEN rayon d'herbes, à l'étage : plus jamais appelé depuis que la
        /// récolte se vend au contact. Conservé uniquement pour retrouver et
        /// supprimer les étagères laissées par les anciennes sauvegardes.
        /// </summary>
        private void CreateHerbShelf()
        {
            if (_shelf != null || _layout == null)
                return;

            NetworkSpawner spawner = GameData.Instance.GetComponent<NetworkSpawner>();

            // Le jeu sauvegarde les meubles placés : une étagère à herbes
            // laissée par une partie précédente (ou une ancienne version du
            // mod) peut être restaurée dans la réserve. On la retire avant de
            // replacer la nôtre, sinon elles s'accumulent.
            Vector2Int leftoverStock = DestroyLeftoverShelves(spawner);

            Data_Container chosen = null;
            Data_Container preferred = null;
            Data_Container fallback = null;

            for (int i = 0; i < spawner.buildables.Length; i++)
            {
                GameObject candidate = spawner.buildables[i];
                if (candidate == null)
                    continue;

                Data_Container data = candidate.GetComponent<Data_Container>();
                if (data == null)
                    continue;

                // 99 = caisse, 69 = stockage : ce ne sont pas des rayons.
                // (Data_Container lit ces deux valeurs dans OnStartClient.)
                if (data.containerClass == 99 || data.containerClass == 69)
                    continue;
                // Il faut au moins deux rangées (une par variété d'herbe).
                if (data.productInfoArray == null || data.productInfoArray.Length < 4)
                    continue;

                if (fallback == null)
                    fallback = data;

                if (data.standSpot == null)
                    continue;

                preferred = data;
                if (data.parentIndex == 0)
                    break;
            }

            chosen = preferred != null ? preferred : fallback;
            if (chosen == null)
            {
                if (Time.time >= _nextPrefabWarning)
                {
                    _nextPrefabWarning = Time.time + 30f;
                    Plugin.Log.LogWarning(
                        "[Souterrain] Aucune étagère constructible trouvée. Candidats : " +
                        DescribeBuildables(spawner));
                }
                return;
            }

            GameObject prefab = chosen.gameObject;

            Vector3 shelfPosition = FindUpstairsShelfPosition();
            GameObject shelfObject = Object.Instantiate(
                prefab, shelfPosition, Quaternion.Euler(0f, 180f, 0f));
            shelfObject.name = "Underground_HerbShelf";
            Data_Container dataContainer = shelfObject.GetComponent<Data_Container>();
            if (dataContainer == null)
            {
                Object.Destroy(shelfObject);
                return;
            }

            // Certains meubles n'ont pas de point d'attente : sans lui, les PNJ
            // clients ne peuvent pas s'approcher du rayon. On en ajoute un.
            if (dataContainer.standSpot == null)
            {
                GameObject stand = new GameObject("Underground_StandSpot");
                stand.transform.SetParent(shelfObject.transform, false);
                stand.transform.position =
                    shelfObject.transform.position + shelfObject.transform.forward * 1.2f;
                dataContainer.standSpot = stand.transform;
            }

            int[] empty = new int[dataContainer.productInfoArray.Length];
            for (int i = 0; i < empty.Length; i += 2)
            {
                empty[i] = -1;
                if (i + 1 < empty.Length)
                    empty[i + 1] = 0;
            }
            if (dataContainer.isServer)
                dataContainer.NetworkproductInfoArray = empty;
            else
                dataContainer.productInfoArray = empty;

            // On se range dans la liste officielle des rayons : c'est elle que
            // consultent les PNJ clients pour trouver un produit.
            Transform shelfParent = NPC_Manager.Instance.shelvesOBJ != null
                ? NPC_Manager.Instance.shelvesOBJ.transform
                : null;
            if (shelfParent == null && dataContainer.parentIndex >= 0 &&
                dataContainer.parentIndex < spawner.levelPropsOBJ.transform.childCount)
                shelfParent = spawner.levelPropsOBJ.transform.GetChild(dataContainer.parentIndex);
            if (shelfParent == null)
                shelfParent = spawner.levelPropsOBJ.transform.GetChild(0);
            shelfObject.transform.SetParent(shelfParent, true);

            NetworkServer.Spawn(shelfObject);
            dataContainer.ActivateShelvesFromLoad();
            _shelf = dataContainer;

            // Le stock de l'ancienne étagère (restaurée par la sauvegarde) est
            // repris ici : on ne perd pas la récolte en cours.
            if (leftoverStock.x > 0)
                dataContainer.DebugAdd(0, UndergroundCatalog.ClassicProductId,
                    Mathf.Clamp(leftoverStock.x, 1, 24));
            if (leftoverStock.y > 0)
                dataContainer.DebugAdd(2, UndergroundCatalog.PremiumProductId,
                    Mathf.Clamp(leftoverStock.y, 1, 24));

            Plugin.Log.LogInfo(
                $"[Souterrain] Étagère à herbes placée à l'étage en {shelfPosition} " +
                $"({chosen.name}, classe {chosen.containerClass}, parent {chosen.parentIndex}, " +
                $"standspot {(chosen.standSpot != null ? "oui" : "non")}).");
        }

        /// <summary>
        /// Le rayon de vente doit être à l'étage : la salle du sous-sol ne sert
        /// plus qu'à la culture. On le place à côté d'un rayon existant du
        /// magasin (donc dans la surface de vente, jamais dans la réserve) et
        /// sinon autour du centre du magasin.
        /// </summary>
        private Vector3 FindUpstairsShelfPosition()
        {
            Vector3 basePosition = GetSalesFloorCenter();

            if (TryFindSpotBesideExistingShelf(out Vector3 besideShelf))
                return besideShelf;

            Vector3[] offsets =
            {
                Vector3.zero,
                new Vector3(2.5f, 0f, 0f),
                new Vector3(-2.5f, 0f, 0f),
                new Vector3(0f, 0f, 2.5f),
                new Vector3(0f, 0f, -2.5f),
                new Vector3(3.5f, 0f, 3.5f),
                new Vector3(-3.5f, 0f, 3.5f),
                new Vector3(3.5f, 0f, -3.5f),
                new Vector3(-3.5f, 0f, -3.5f)
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 candidate = basePosition + offsets[i];
                if (!TryFloorPoint(candidate, out Vector3 floor))
                    continue;
                if (IsNearStorageRack(floor))
                    continue;
                if (!HasShelfSpace(floor))
                    continue;

                return floor;
            }

            Plugin.Log.LogWarning(
                "[Souterrain] Aucun emplacement libre pour le rayon à herbes à l'étage.");
            return basePosition;
        }

        /// <summary>
        /// Cherche un sol libre juste à côté d'un rayon déjà placé par le
        /// joueur : le rayon à herbes reste ainsi dans la surface de vente et
        /// pas dans la réserve.
        /// </summary>
        private bool TryFindSpotBesideExistingShelf(out Vector3 position)
        {
            position = Vector3.zero;

            NPC_Manager manager = NPC_Manager.Instance;
            GameObject shelves = manager != null ? manager.shelvesOBJ : null;
            if (shelves == null || shelves.transform.childCount == 0)
                return false;

            Vector3 storeCenter = GetSalesFloorCenter();
            Vector3 reference = Vector3.zero;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < shelves.transform.childCount; i++)
            {
                Transform shelf = shelves.transform.GetChild(i);
                if (shelf == null || shelf.name.StartsWith("Underground_"))
                    continue;

                Data_Container container = shelf.GetComponent<Data_Container>();
                if (container == null)
                    continue;
                if (container.containerClass == 99 || container.containerClass == 69)
                    continue;

                float distance = (shelf.position - storeCenter).sqrMagnitude;
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                reference = shelf.position;
            }

            if (reference == Vector3.zero)
                return false;

            // Deux rayons de recherche : tout contre un rayon existant, puis un
            // peu plus loin s'il n'y a pas la place.
            float[] radii = { 2.6f, 3.8f };
            for (int pass = 0; pass < radii.Length; pass++)
            {
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI / 4f;
                    Vector3 candidate = reference + new Vector3(
                        Mathf.Cos(angle) * radii[pass], 0f, Mathf.Sin(angle) * radii[pass]);

                    if (!TryFloorPoint(candidate, out Vector3 floor, 0.9f))
                        continue;
                    if (IsNearStorageRack(floor))
                        continue;
                    if (!HasShelfSpace(floor))
                        continue;

                    position = floor;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Vrai si le point est collé à un meuble de réserve (les étagères de
        /// stockage), pour ne jamais y cacher le rayon à herbes.
        /// </summary>
        private static bool IsNearStorageRack(Vector3 position)
        {
            NPC_Manager manager = NPC_Manager.Instance;
            GameObject storage = manager != null ? manager.storageOBJ : null;
            if (storage == null)
                return false;

            for (int i = 0; i < storage.transform.childCount; i++)
            {
                Transform rack = storage.transform.GetChild(i);
                if (rack == null)
                    continue;

                Vector3 delta = rack.position - position;
                delta.y = 0f;
                if (delta.sqrMagnitude < 20f)
                    return true;
            }

            return false;
        }

        private static Vector3 GetSalesFloorCenter()
        {
            Builder_Main builder = Object.FindFirstObjectByType<Builder_Main>();
            if (builder != null &&
                builder.buildXLimits.y > builder.buildXLimits.x &&
                builder.buildZLimits.y > builder.buildZLimits.x)
            {
                return new Vector3(
                    (builder.buildXLimits.x + builder.buildXLimits.y) * 0.5f, 0f,
                    (builder.buildZLimits.x + builder.buildZLimits.y) * 0.5f);
            }

            UndergroundManager manager = Instance;
            if (manager != null && manager._layout != null)
                return new Vector3(
                    manager._layout.ShelfPosition.x, 0f, manager._layout.ShelfPosition.z);

            return Vector3.zero;
        }

        /// <summary>
        /// Supprime les étagères à herbes laissées par une session précédente
        /// (le jeu sauvegarde les meubles placés, y compris la nôtre) avant
        /// d'en créer une neuve. On repère ces vestiges à deux signes : leur nom
        /// ou le fait qu'elles contiennent nos deux variétés d'herbe.
        /// </summary>
        private static Vector2Int DestroyLeftoverShelves(NetworkSpawner spawner)
        {
            Vector2Int stock = Vector2Int.zero;

            if (spawner == null || spawner.levelPropsOBJ == null)
                return stock;

            int classicId = UndergroundCatalog.ClassicProductId;
            int premiumId = UndergroundCatalog.PremiumProductId;

            Transform root = spawner.levelPropsOBJ.transform;
            List<GameObject> leftovers = new List<GameObject>();

            for (int i = 0; i < root.childCount; i++)
            {
                Transform group = root.GetChild(i);
                if (group == null)
                    continue;

                for (int j = 0; j < group.childCount; j++)
                {
                    Transform child = group.GetChild(j);
                    if (child == null)
                        continue;

                    if (child.name.StartsWith("Underground_HerbShelf") ||
                        ContainsUndergroundProduct(child, classicId, premiumId))
                    {
                        leftovers.Add(child.gameObject);
                        stock = Vector2Int.Max(stock, ReadHerbStock(child, classicId, premiumId));
                    }
                }
            }

            for (int i = 0; i < leftovers.Count; i++)
            {
                GameObject leftover = leftovers[i];
                if (leftover == null)
                    continue;

                Plugin.Log.LogInfo(
                    "[Souterrain] Ancienne étagère à herbes retirée du décor " +
                    $"({leftover.transform.position}).");

                NetworkIdentity identity = leftover.GetComponent<NetworkIdentity>();
                if (NetworkServer.active && identity != null && identity.netId != 0)
                    NetworkServer.Destroy(leftover);
                else
                    Object.Destroy(leftover);
            }

            return stock;
        }

        private static bool ContainsUndergroundProduct(
            Transform candidate, int classicId, int premiumId)
        {
            if (classicId < 0 && premiumId < 0)
                return false;

            Data_Container container = candidate.GetComponent<Data_Container>();
            if (container == null || container.productInfoArray == null)
                return false;

            for (int i = 0; i < container.productInfoArray.Length; i += 2)
            {
                int productId = container.productInfoArray[i];
                if (productId < 0)
                    continue;
                if (productId == classicId || productId == premiumId)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Relève le stock d'herbes d'une étagère restaurée par la sauvegarde,
        /// pour le remettre sur celle qu'on recrée au bon endroit.
        /// </summary>
        private static Vector2Int ReadHerbStock(Transform shelf, int classicId, int premiumId)
        {
            Data_Container container = shelf.GetComponent<Data_Container>();
            if (container == null || container.productInfoArray == null)
                return Vector2Int.zero;

            int classic = 0;
            int premium = 0;

            for (int i = 0; i + 1 < container.productInfoArray.Length; i += 2)
            {
                int productId = container.productInfoArray[i];
                int quantity = container.productInfoArray[i + 1];
                if (quantity <= 0)
                    continue;

                if (productId == classicId)
                    classic = Mathf.Max(classic, quantity);
                else if (productId == premiumId)
                    premium = Mathf.Max(premium, quantity);
            }

            return new Vector2Int(classic, premium);
        }

        private static bool HasShelfSpace(Vector3 position)
        {
            Collider[] overlaps = Physics.OverlapBox(
                position + Vector3.up * 1.3f,
                new Vector3(2.2f, 1.3f, 1.2f),
                Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider other = overlaps[i];
                if (other == null || other.isTrigger)
                    continue;
                if (other.GetComponentInParent<FirstPersonController>() != null)
                    continue;
                if (other.GetComponentInParent<NPC_Info>() != null)
                    continue;

                return false;
            }

            return true;
        }

        /// <summary>
        /// Résumé des meubles constructibles, utile dans le log quand aucun
        /// rayon n'est reconnu : on voit immédiatement les classes et
        /// les parents renvoyés par le jeu.
        /// </summary>
        private static string DescribeBuildables(NetworkSpawner spawner)
        {
            if (spawner == null || spawner.buildables == null)
                return "aucun";

            var builder = new StringBuilder();
            int shown = 0;
            for (int i = 0; i < spawner.buildables.Length && shown < 12; i++)
            {
                GameObject candidate = spawner.buildables[i];
                if (candidate == null)
                    continue;

                Data_Container data = candidate.GetComponent<Data_Container>();
                if (data == null)
                    continue;

                if (shown > 0)
                    builder.Append(", ");
                builder.Append(candidate.name);
                builder.Append("(classe ").Append(data.containerClass);
                builder.Append(", parent ").Append(data.parentIndex);
                builder.Append(", ").Append(data.productInfoArray != null
                    ? data.productInfoArray.Length
                    : 0).Append(" cases");
                builder.Append(data.standSpot != null ? ", standspot)" : ")");
                shown++;
            }

            return shown > 0 ? builder.ToString() : "aucun Data_Container";
        }

        private void SpawnPolice()
        {
            if (_police != null || NPC_Manager.Instance == null || _layout == null)
                return;

            GameObject prefab = NPC_Manager.Instance.npcAgentPrefab;
            if (prefab == null)
                return;

            Vector3 position = FindShopNavMeshPoint(_layout.Hatch + new Vector3(3.2f, 0f, 3.2f));
            if (position == Vector3.zero)
                position = FindShopNavMeshPoint(_layout.Hatch + new Vector3(-3.2f, 0f, 3.2f));
            if (position == Vector3.zero)
                position = FindShopNavMeshPoint(_layout.Hatch);
            if (position == Vector3.zero)
                position = _layout.Hatch + Vector3.up * 0.2f;
            GameObject officer = Object.Instantiate(prefab, position, Quaternion.identity);
            officer.name = "Underground_Police";

            NPC_Info info = officer.GetComponent<NPC_Info>();
            if (info != null)
            {
                info.NPCID = 0;
                info.isCustomer = false;
                info.isEmployee = false;
                info.NPCName = "Agent";
            }

            NetworkServer.Spawn(officer);
            _police = officer;
            _policeAgent = officer.GetComponent<NavMeshAgent>();
            if (_policeAgent != null)
            {
                _policeAgent.enabled = true;
                if (NavMesh.SamplePosition(
                        officer.transform.position, out NavMeshHit spawnHit, 4f, -1))
                    _policeAgent.Warp(spawnHit.position);
                _policeAgent.speed = 2.15f;
                _policeAgent.stoppingDistance = 1.2f;
                _policeAgent.acceleration = 10f;
            }

            NetworkIdentity identity = officer.GetComponent<NetworkIdentity>();
            _policeNetId = identity != null ? identity.netId : 0;
            officer.AddComponent<PoliceVisual>();
            _policeVisualAttached = true;

            _patrolPoints = BuildPatrolPoints();
            _patrolIndex = 0;
            if (_patrolPoints.Length > 0)
                SetPoliceDestination(_patrolPoints[0]);

            NetworkServer.SendToAll(new UndergroundPoliceMessage { NetId = _policeNetId });
            Plugin.Log.LogInfo($"[Souterrain] Policier spawn (netId {_policeNetId}).");
        }

        private Vector3[] BuildPatrolPoints()
        {
            var points = new List<Vector3>();
            NPC_Manager manager = NPC_Manager.Instance;

            if (manager.randomPointsOBJ != null)
            {
                Transform parent = manager.randomPointsOBJ.transform;
                for (int i = 0; i < parent.childCount; i += 2)
                {
                    Vector3 point = FindShopNavMeshPoint(parent.GetChild(i).position);
                    if (point != Vector3.zero)
                        points.Add(point);
                    if (points.Count >= 4)
                        break;
                }
            }

            Vector3 hatchPatrol = FindShopNavMeshPoint(_layout.Hatch + new Vector3(2.5f, 0f, 2.5f));
            if (hatchPatrol != Vector3.zero)
                points.Add(hatchPatrol);

            // Le policier patrouille uniquement à l'étage. C'est le raid qui
            // le fait descendre, sinon il camperait en bas en permanence.
            if (points.Count == 0)
            {
                Vector3 hatch = FindShopNavMeshPoint(_layout.Hatch);
                if (hatch != Vector3.zero)
                    points.Add(hatch);
            }

            return points.ToArray();
        }

        private void RefreshPatrolPoints()
        {
            _patrolPoints = BuildPatrolPoints();
            _patrolIndex = 0;
            if (_patrolPoints.Length > 0)
                SetPoliceDestination(_patrolPoints[0]);
        }

        private void HandleLocalInput()
        {
            FirstPersonController player = FirstPersonController.Instance;
            if (player == null || _layout == null)
                return;

            Player input = ReInput.players.GetPlayer(0);
            if (input == null)
                return;

            bool action = input.GetButtonDown("Main Action");
            bool cover = Input.GetKeyDown(KeyCode.F);
            if (!action && !cover)
                return;

            Vector3 playerPosition = player.transform.position;

            // Vente de la récolte au contact, dans la salle.
            if (action && _layout.ContactPosition != Vector3.zero &&
                Vector3.Distance(playerPosition, _layout.ContactPosition) < 3.0f)
            {
                RequestSell();
                return;
            }

            // Remontée : E sur le palier, au pied des marches.
            if (action && playerPosition.y < -1.2f &&
                Vector3.Distance(playerPosition, _layout.StairExit) < 3.5f)
            {
                AscendPlayer(player);
                return;
            }

            bool nearHatch = Vector3.Distance(playerPosition, _layout.Hatch) < 3.2f;

            if (nearHatch)
            {
                // Trappe ouverte : E descend, F referme. La descente ne dépend
                // donc plus du déclencheur physique (joueur déjà dessus, trappe
                // collée à un mur ou à l'entrée du magasin).
                if (cover && _doorOpen)
                {
                    RequestDoorToggle();
                    return;
                }

                if (action)
                {
                    if (_doorOpen)
                    {
                        DescendPlayer(player);
                    }
                    else
                    {
                        RequestDoorToggle();
                        if (NetworkServer.active)
                        {
                            Notify("Trappe ouverte : appuie à nouveau sur E pour descendre.");
                        }
                    }

                    return;
                }
            }

            CannabisPlot plot = FindClosestPlot(player);
            if (plot == null)
                return;

            if (action)
                RequestPlotAction(plot);
            else if (cover)
                RequestPlotAction(plot, true);
        }

        /// <summary>
        /// Descente : dépose le joueur sur le palier du bas, dans la salle. On
        /// ne le pose plus au milieu de la rampe, où il pouvait glisser, se
        /// coincer dans la pente ou rester bloqué dans le décor.
        /// </summary>
        private void DescendPlayer(FirstPersonController player)
        {
            if (_layout == null || player == null)
                return;

            Vector3 target = _layout.StairExit != Vector3.zero
                ? _layout.StairExit
                : _layout.RampBottom + new Vector3(0f, 0.1f, -1.6f);

            MovePlayer(player, target, 0f);

            Plugin.Log.LogInfo(
                $"[Souterrain] Descente par la trappe vers ({target.x:0.0}, " +
                $"{target.y:0.0}, {target.z:0.0}).");
        }

        /// <summary>
        /// Remontée : E sur le palier du bas ramène le joueur sur le plancher
        /// du magasin, juste au-dessus de la trappe.
        /// </summary>
        private void AscendPlayer(FirstPersonController player)
        {
            if (_layout == null || player == null)
                return;

            Vector3 target = _layout.Hatch + Vector3.up * 0.45f;
            MovePlayer(player, target, 0f);

            Plugin.Log.LogInfo("[Souterrain] Remontée par la trappe.");
        }

        private static void MovePlayer(
            FirstPersonController player, Vector3 target, float yaw)
        {
            FirstPersonTransform transformComponent = player.GetComponent<FirstPersonTransform>();
            if (transformComponent != null)
            {
                transformComponent.coroutineActivator(target, yaw);
            }
            else
            {
                player.transform.position = target;
            }
        }

        private CannabisPlot FindClosestPlot(FirstPersonController player)
        {
            if (_plots == null)
                return null;

            Camera camera = Camera.main;
            Vector3 forward = camera != null ? camera.transform.forward : player.transform.forward;
            CannabisPlot best = null;
            float bestDistance = 3.0f;

            for (int i = 0; i < _plots.Length; i++)
            {
                CannabisPlot plot = _plots[i];
                if (plot == null)
                    continue;

                Vector3 direction = plot.transform.position - player.transform.position;
                float distance = direction.magnitude;
                if (distance > bestDistance)
                    continue;
                if (distance > 0.01f &&
                    Vector3.Dot(forward, direction.normalized) < 0.15f)
                    continue;

                bestDistance = distance;
                best = plot;
            }

            return best;
        }

        private void RequestPlotAction(CannabisPlot plot, bool cover = false)
        {
            if (plot == null)
                return;

            byte action;
            if (cover)
            {
                action = UndergroundNetwork.ActionToggleCover;
            }
            else
            {
                switch (plot.State)
                {
                    case CannabisPlot.Empty:
                        action = UndergroundNetwork.ActionPlant;
                        break;
                    case CannabisPlot.Growing:
                        action = plot.Watered
                            ? UndergroundNetwork.ActionToggleCover
                            : UndergroundNetwork.ActionWater;
                        break;
                    case CannabisPlot.Ready:
                        action = UndergroundNetwork.ActionHarvest;
                        break;
                    default:
                        return;
                }
            }

            if (NetworkServer.active)
                ApplyAction(action, plot.Index);
            else
                UndergroundNetwork.SendAction(action, plot.Index);
        }

        private void RequestSell()
        {
            if (IsServer)
                SellStock();
            else
                UndergroundNetwork.SendAction(UndergroundNetwork.ActionSell, -1);
        }

        /// <summary>
        /// Vend toute la récolte en réserve au contact du marché noir : plus de
        /// rayon dans le magasin, donc plus de clients qui se servent tout seuls
        /// sur une étagère. Le paiement est immédiat, à prix fixe.
        /// </summary>
        internal void SellStock(NetworkConnectionToClient requester = null)
        {
            if (!IsServer)
                return;

            int units = _stock[0] + _stock[1];
            if (units <= 0)
            {
                Notify("Rien à vendre : récolte d'abord sur les parcelles.", requester);
                return;
            }

            float total = _stock[0] * UndergroundCatalog.ClassicBasePrice +
                          _stock[1] * UndergroundCatalog.PremiumBasePrice;

            GameData data = GameData.Instance;
            if (data == null)
                return;

            data.CmdAlterFunds(total);
            ClearStock();

            Notify($"{units} unités vendues au contact : +{total:0} $.", requester);
            Plugin.Log.LogInfo(
                $"[Souterrain] Récolte vendue au contact : {units} unités, {total:0} $.");
            MarkDirty();
            SendState();
            SaveState();
        }

        private void RequestDoorToggle()
        {
            if (NetworkServer.active)
                SetDoorOpen(!_doorOpen);
            else
                UndergroundNetwork.SendAction(UndergroundNetwork.ActionToggleDoor, -1);
        }

        internal void HandleNetworkAction(NetworkConnectionToClient sender, byte action, int plot)
        {
            if (!NetworkServer.active)
                return;

            if (action == UndergroundNetwork.ActionToggleDoor)
            {
                SetDoorOpen(!_doorOpen);
                return;
            }

            if (action == UndergroundNetwork.ActionSell)
            {
                SellStock(sender);
                SendStateTo(sender);
                return;
            }

            ApplyAction(action, plot, sender);

            // Même si l'action a été refusée (état déjà changé, fonds
            // insuffisants...), le client qui agit reçoit l'état autoritaire.
            SendStateTo(sender);
        }

        private void ApplyAction(
            byte action, int plotIndex, NetworkConnectionToClient requester = null)
        {
            if (_plots == null || plotIndex < 0 || plotIndex >= _plots.Length)
                return;

            CannabisPlot plot = _plots[plotIndex];
            if (plot == null)
                return;

            switch (action)
            {
                case UndergroundNetwork.ActionPlant:
                {
                    if (plot.State != CannabisPlot.Empty)
                        return;
                    if (GameData.Instance.gameFunds < plot.SeedCost)
                    {
                        Notify($"Il faut {plot.SeedCost:0} $ pour acheter cette graine.", requester);
                        return;
                    }

                    GameData.Instance.CmdAlterFunds(-plot.SeedCost);
                    plot.State = CannabisPlot.Growing;
                    plot.PlantedAt = NowHours;
                    plot.Watered = false;
                    plot.Covered = false;
                    plot.RefreshVisual();
                    Notify($"Graine plantée ({plot.SeedCost:0} $). Il faut l'arroser.", requester);
                    MarkDirty();
                    break;
                }
                case UndergroundNetwork.ActionWater:
                {
                    if (plot.State != CannabisPlot.Growing || plot.Watered)
                        return;
                    plot.Watered = true;
                    plot.RefreshVisual();
                    Notify("Parcelle arrosée.", requester);
                    MarkDirty();
                    break;
                }
                case UndergroundNetwork.ActionHarvest:
                {
                    if (plot.State != CannabisPlot.Ready)
                        return;

                    int amount = plot.ProductId == UndergroundCatalog.PremiumProductId ? 6 : 8;
                    if (!AddHarvest(plot.ProductId, amount))
                    {
                        Notify($"Stock plein ({StockCapacity} unités). Vends la récolte au contact.", requester);
                        return;
                    }

                    plot.State = CannabisPlot.Empty;
                    plot.PlantedAt = 0f;
                    plot.Watered = false;
                    plot.Covered = false;
                    plot.RefreshVisual();
                    Notify($"Récolte : {amount} unités rangées dans la réserve. Vends-les au contact.", requester);
                    MarkDirty();
                    break;
                }
                case UndergroundNetwork.ActionToggleCover:
                {
                    if (plot.State == CannabisPlot.Empty)
                        return;
                    plot.Covered = !plot.Covered;
                    plot.RefreshVisual();
                    Notify(plot.Covered ? "Parcelle cachée sous la bâche." : "Parcelle découverte.", requester);
                    MarkDirty();
                    break;
                }
            }

            SendState();
        }

        internal void SetDoorOpen(bool open)
        {
            _doorOpen = open;
            if (_door != null)
                _door.SetOpen(open);

            if (IsServer)
            {
                _patrolPoints = BuildPatrolPoints();
                _patrolIndex = 0;
                if (_patrolPoints.Length > 0)
                    SetPoliceDestination(_patrolPoints[0]);
            }

            MarkDirty();
            SendState();
        }

        private void UpdatePlots()
        {
            if (_plots == null)
                return;

            for (int i = 0; i < _plots.Length; i++)
            {
                CannabisPlot plot = _plots[i];
                if (plot == null)
                    continue;

                if (IsServer && plot.State == CannabisPlot.Growing &&
                    plot.Watered && GetGrowth01(plot) >= 1f)
                {
                    plot.State = CannabisPlot.Ready;
                    plot.RefreshVisual();
                    Notify("Une parcelle est prête à être récoltée.");
                    MarkDirty();
                }

                if (plot.State == CannabisPlot.Ready && plot.Watered)
                    plot.RefreshVisual();
            }
        }

        internal float GetGrowth01(CannabisPlot plot)
        {
            if (plot == null || plot.State == CannabisPlot.Empty)
                return 0f;

            float hours = Mathf.Max(1f, Plugin.UndergroundGrowHours.Value);
            float elapsed = Mathf.Max(0f, NowHours - plot.PlantedAt);
            float progress = elapsed / hours;
            if (!plot.Watered)
                progress = Mathf.Min(progress, 0.5f);

            return Mathf.Clamp01(progress);
        }

        private void TickPolice()
        {
            if (!IsServer || _police == null || _policeAgent == null || _layout == null)
                return;

            if (!_policeAgent.isOnNavMesh)
            {
                Vector3 safePoint = FindShopNavMeshPoint(_layout.Hatch + new Vector3(2.0f, 0f, 2.0f));
                if (safePoint == Vector3.zero)
                    safePoint = FindShopNavMeshPoint(_layout.Hatch);
                if (safePoint != Vector3.zero && Time.time - _lastPoliceRecovery > 3f)
                {
                    _lastPoliceRecovery = Time.time;
                    _policeAgent.Warp(safePoint + Vector3.up * 0.05f);
                }
                return;
            }

            if (_raidActive)
            {
                TickRaid();
                return;
            }

            if (_patrolPoints != null && _patrolPoints.Length > 0 &&
                !_policeAgent.pathPending &&
                _policeAgent.remainingDistance <= 1.4f)
            {
                _patrolIndex = (_patrolIndex + 1) % _patrolPoints.Length;
                SetPoliceDestination(_patrolPoints[_patrolIndex]);
            }

            if (Time.time < _nextInspection)
                return;

            _nextInspection = Time.time + UnityEngine.Random.Range(110f, 220f);

            if (!GameData.Instance.isSupermarketOpen ||
                GameData.Instance.timeOfDay < 8f ||
                GameData.Instance.timeOfDay > 22f)
                return;

            if (!_doorOpen || (!AnyUncoveredPlant() && !HasHerbStock()))
                return;

            if (UnityEngine.Random.value < Plugin.UndergroundRaidChance.Value)
                StartRaid();
        }

        private void StartRaid()
        {
            if (_raidActive || _policeAgent == null || _layout == null)
                return;

            _raidActive = true;
            _raidPhase = 0;
            _raidPhaseStarted = Time.time;

            Vector3 hatch = FindShopNavMeshPoint(_layout.Hatch + new Vector3(1.6f, 0f, 1.6f));
            if (hatch == Vector3.zero)
                hatch = FindShopNavMeshPoint(_layout.Hatch);
            if (hatch != Vector3.zero)
                SetPoliceDestination(hatch);

            Notify("La police se dirige vers la trappe !");
            SendAlert("Un policier inspecte le magasin. Fermez la trappe ou cachez les plants !");
            Plugin.Log.LogInfo("[Souterrain] Raid de police déclenché.");
        }

        /// <summary>
        /// Déroulé du raid : approche de la trappe, descente, fouille,
        /// remontée. Des délais bornent chaque étape pour qu'un problème de
        /// navigation ne laisse jamais la police bloquée.
        /// </summary>
        private void TickRaid()
        {
            if (_policeAgent == null || _layout == null)
            {
                EndRaid(false);
                return;
            }

            Vector3 position = _policeAgent.transform.position;
            float elapsed = Time.time - _raidPhaseStarted;

            switch (_raidPhase)
            {
                case 0: // Approche de la trappe, à l'étage.
                {
                    if (!_doorOpen)
                    {
                        Notify("La trappe est fermée : le policier ne peut pas descendre.");
                        SendAlert("La trappe était fermée. La police n'a rien trouvé.");
                        EndRaid(false);
                        return;
                    }

                    float distanceToHatch = Vector3.Distance(position, _layout.Hatch);
                    if (distanceToHatch < 3.0f || elapsed > 22f)
                    {
                        _raidPhase = 1;
                        _raidPhaseStarted = Time.time;
                        SetPoliceDestination(_layout.RoomCenter);

                        // Si le maillage ne relie pas les deux niveaux, on
                        // fait descendre l'agent par l'escalier après 9 s.
                        if (elapsed > 22f && position.y > _layout.FloorY + 1f)
                            DescendPolice();
                    }
                    return;
                }
                case 1: // Descente et fouille du sous-sol.
                {
                    bool inBasement = position.y < _layout.FloorY + 1.6f;
                    if (!inBasement && elapsed > 9f)
                    {
                        DescendPolice();
                        return;
                    }

                    float roomDistance = Vector3.Distance(position, _layout.RoomCenter);
                    if (inBasement && (roomDistance < 4.0f || elapsed > 20f))
                    {
                        ResolveRaid();
                    }
                    return;
                }
                case 2: // Remontée après la fouille.
                {
                    Vector3 hatch = FindShopNavMeshPoint(_layout.Hatch + new Vector3(1.5f, 0f, 1.5f));
                    if (hatch == Vector3.zero)
                        hatch = _layout.Hatch;
                    SetPoliceDestination(hatch);

                    if (position.y < -0.6f && elapsed > 7f)
                    {
                        _policeAgent.Warp(hatch + Vector3.up * 0.15f);
                    }
                    else if (elapsed > 1f && position.y > -0.6f &&
                             Vector3.Distance(position, hatch) < 3.5f)
                    {
                        EndRaid(true);
                    }
                    else if (elapsed > 14f)
                    {
                        EndRaid(true);
                    }
                    return;
                }
            }
        }

        private void DescendPolice()
        {
            if (_policeAgent == null || _layout == null)
                return;

            Vector3 landing = _layout.RampBottom + Vector3.up * 0.2f;
            _policeAgent.Warp(landing);
            SetPoliceDestination(_layout.RoomCenter);
            Plugin.Log.LogInfo("[Souterrain] Le policier descend par l'escalier.");
        }

        private void ResolveRaid()
        {
            // Une fois en bas, fermer la trappe ne cache plus rien. Un rayon
            // garni est une preuve au même titre qu'un plant visible.
            if (AnyUncoveredPlant() || HasHerbStock())
            {
                ApplyFine();
            }
            else
            {
                Notify("La police n'a rien trouvé. Elle remonte.");
                SendAlert("La police a fouillé le sous-sol sans rien trouver.");
            }

            _raidPhase = 2;
            _raidPhaseStarted = Time.time;
        }

        private void EndRaid(bool resolved)
        {
            _raidActive = false;
            _raidPhase = 0;
            _nextInspection = Time.time + UnityEngine.Random.Range(100f, 180f);
            ReturnPoliceToPatrol();
            if (!resolved)
                Plugin.Log.LogInfo("[Souterrain] Raid terminé sans descente.");
        }

        private void ApplyFine()
        {
            float fine = Plugin.UndergroundRaidFine.Value;
            GameData.Instance.CmdAlterFunds(-fine);

            for (int i = 0; i < _plots.Length; i++)
            {
                CannabisPlot plot = _plots[i];
                if (plot == null)
                    continue;
                plot.State = CannabisPlot.Empty;
                plot.PlantedAt = 0f;
                plot.Watered = false;
                plot.Covered = false;
                plot.RefreshVisual();
            }

            ClearStock();
            string message = $"Raid de police : amende de {fine:0} $ et récolte confisquée.";
            Notify(message);
            SendAlert(message);
            Plugin.Log.LogWarning($"[Souterrain] Amende appliquée : {fine:0} $.");
            MarkDirty();
            SaveState();
        }

        private bool HasHerbStock()
        {
            return _stock[0] > 0 || _stock[1] > 0;
        }

        private bool AnyUncoveredPlant()
        {
            if (_plots == null)
                return false;

            for (int i = 0; i < _plots.Length; i++)
            {
                CannabisPlot plot = _plots[i];
                if (plot != null && plot.IsUncoveredAndGrowing)
                    return true;
            }

            return false;
        }

        private void ReturnPoliceToPatrol()
        {
            if (_policeAgent == null || _patrolPoints == null || _patrolPoints.Length == 0)
                return;

            _patrolIndex = 0;
            SetPoliceDestination(_patrolPoints[0]);
        }

        /// <summary>
        /// Filet de sécurité : si le joueur local tombe sous la dalle du sous-sol
        /// (décor supprimé, ancienne trappe, trou dans le sol), on le repose
        /// près de la trappe au lieu de le laisser coincé dans le vide.
        /// </summary>
        private void TickFallRecovery()
        {
            if (Time.time < _nextFallCheck)
                return;

            _nextFallCheck = Time.time + 1f;

            if (_layout == null)
                return;

            FirstPersonController controller = FirstPersonController.Instance;
            if (controller == null)
                return;

            Vector3 position = controller.transform.position;

            // Tombé sous la dalle du sous-sol.
            bool farBelow = position.y <= _layout.FloorY - 3.5f;

            // Coincé *entre* le plancher du magasin et la salle : sous le
            // niveau du sol, mais hors de l'emprise du sous-sol (trappe posée
            // trop loin du magasin, rampe ensevelie, mur traversé).
            bool stuckOutsideRoom =
                position.y < -1.2f && !IsInBasement(position);

            if (!farBelow && !stuckOutsideRoom)
                return;

            Vector3 target = _layout.Hatch + Vector3.up * 0.35f;
            FirstPersonTransform transformComponent =
                controller.GetComponent<FirstPersonTransform>();
            if (transformComponent != null)
                transformComponent.coroutineActivator(target, 0f);
            else
                controller.transform.position = target;

            Plugin.Log.LogWarning(
                $"[Souterrain] Joueur coincé sous le décor en ({position.x:0.0}, {position.y:0.0}, " +
                $"{position.z:0.0}) : remonté près de la trappe.");
        }

        /// <summary>
        /// Le jeu restaure les meubles sauvegardés, y compris notre étagère à
        /// herbes : selon l'ordre de chargement, un doublon peut apparaitre
        /// après notre construction (souvent dans la réserve). On le retire au
        /// fil de l'eau, sans toucher à l'étagère réellement utilisée.
        /// </summary>
        private void TickLeftoverCleanup()
        {
            if (!IsServer || _layout == null)
                return;
            if (Time.time < _nextLeftoverCheck)
                return;

            _nextLeftoverCheck = Time.time + 5f;

            GameData data = GameData.Instance;
            NetworkSpawner spawner = data != null ? data.GetComponent<NetworkSpawner>() : null;
            if (spawner == null)
                return;

            int classicId = UndergroundCatalog.ClassicProductId;
            int premiumId = UndergroundCatalog.PremiumProductId;

            // L'ancien rayon pouvait être rangé sous levelPropsOBJ (groupes de
            // meubles) ou sous shelvesOBJ (liste officielle des rayons) : on
            // balaie les deux, sur deux niveaux.
            if (spawner.levelPropsOBJ != null)
                PurgeLeftoverShelves(spawner.levelPropsOBJ.transform, classicId, premiumId);
            if (NPC_Manager.Instance != null && NPC_Manager.Instance.shelvesOBJ != null)
                PurgeLeftoverShelves(NPC_Manager.Instance.shelvesOBJ.transform, classicId, premiumId);
        }

        private void PurgeLeftoverShelves(Transform root, int classicId, int premiumId)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform group = root.GetChild(i);
                if (group == null)
                    continue;

                if (IsLeftoverHerbShelf(group, classicId, premiumId))
                {
                    RemoveLeftoverShelf(group, classicId, premiumId);
                    continue;
                }

                for (int j = group.childCount - 1; j >= 0; j--)
                {
                    Transform child = group.GetChild(j);
                    if (child != null && IsLeftoverHerbShelf(child, classicId, premiumId))
                        RemoveLeftoverShelf(child, classicId, premiumId);
                }
            }
        }

        private static bool IsLeftoverHerbShelf(
            Transform candidate, int classicId, int premiumId)
        {
            if (candidate.name.StartsWith("Underground_HerbShelf"))
                return true;

            return ContainsUndergroundProduct(candidate, classicId, premiumId);
        }

        private void RemoveLeftoverShelf(Transform shelf, int classicId, int premiumId)
        {
            // La récolte de l'ancien rayon est reprise dans la réserve du
            // contact avant de retirer l'étagère.
            SalvageLeftoverStock(shelf, classicId, premiumId);

            Plugin.Log.LogInfo(
                $"[Souterrain] Ancien rayon à herbes retiré ({shelf.position}).");

            NetworkIdentity identity = shelf.GetComponent<NetworkIdentity>();
            if (identity != null && identity.netId != 0)
                NetworkServer.Destroy(shelf.gameObject);
            else
                Object.Destroy(shelf.gameObject);
        }

        /// <summary>
        /// Transfère le contenu d'une ancienne étagère à herbes dans la
        /// réserve du contact, pour ne pas perdre la récolte en cours.
        /// </summary>
        private void SalvageLeftoverStock(Transform shelf, int classicId, int premiumId)
        {
            if (shelf == null)
                return;

            Data_Container container = shelf.GetComponent<Data_Container>();
            if (container == null || container.productInfoArray == null)
                return;

            for (int i = 0; i + 1 < container.productInfoArray.Length; i += 2)
            {
                int id = container.productInfoArray[i];
                int quantity = container.productInfoArray[i + 1];
                if (quantity <= 0)
                    continue;

                if (id == classicId)
                    AddHarvest(classicId, quantity);
                else if (id == premiumId)
                    AddHarvest(premiumId, quantity);
            }
        }

        private bool AddHarvest(int productId, int amount)
        {
            int row = productId == UndergroundCatalog.PremiumProductId ? 1 : 0;
            int next = Mathf.Clamp(_stock[row] + amount, 0, StockCapacity);
            if (next == _stock[row])
                return false;

            _stock[row] = next;
            return true;
        }

        private void ClearStock()
        {
            _stock[0] = 0;
            _stock[1] = 0;
        }

        private void TickSave()
        {
            if (Time.time < _nextSave)
                return;

            _nextSave = Time.time + 12f;
            if (_dirty && IsServer)
                SaveState();
        }

        private void MarkDirty()
        {
            _dirty = true;
        }

        private float NowHours
        {
            get
            {
                GameData data = GameData.Instance;
                if (data == null)
                    return 0f;
                return data.gameDay * 24f + data.timeOfDay;
            }
        }

        private Vector3 FindNavMeshPoint(Vector3 position)
        {
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 4f, -1))
                return hit.position;
            return Vector3.zero;
        }

        /// <summary>
        /// Point du navmesh du magasin (et non du sous-sol, situé 4 m plus bas).
        /// Sans ce filtre, SamplePosition peut renvoyer le sol du sous-sol et
        /// faire apparaître le policier hors du maillage de l'étage.
        /// </summary>
        private static Vector3 FindShopNavMeshPoint(Vector3 position)
        {
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 6f, -1) &&
                hit.position.y > -0.8f)
                return hit.position;
            return Vector3.zero;
        }

        private void SetPoliceDestination(Vector3 destination)
        {
            if (_policeAgent == null || !_policeAgent.isOnNavMesh)
                return;

            _policeAgent.destination = destination;
        }

        internal void Notify(string text, NetworkConnectionToClient target = null)
        {
            if (string.IsNullOrEmpty(text))
                return;

            // En multi, le joueur qui agit doit aussi voir le résultat, même
            // quand l'action est exécutée sur l'hôte.
            if (target != null)
                target.Send(new UndergroundNotifyMessage { Text = text });

            if (Time.time - _lastNotify < 0.35f)
                return;

            _lastNotify = Time.time;
            GameCanvas canvas = GameCanvas.Instance;
            if (canvas == null || canvas.importantNotificationPrefab == null ||
                canvas.importantNotificationParentTransform == null)
            {
                Plugin.Log.LogInfo("[Souterrain] " + text);
                return;
            }

            GameObject notification = Object.Instantiate(
                canvas.importantNotificationPrefab,
                canvas.importantNotificationParentTransform);
            TextMeshProUGUI label = notification.GetComponent<TextMeshProUGUI>();
            if (label != null)
                label.text = text;
            notification.SetActive(true);
        }

        private void SendAlert(string text)
        {
            if (!NetworkServer.active)
                return;

            NetworkServer.SendToAll(new UndergroundAlertMessage { Text = text });
        }

        internal void ShowAlert(string text)
        {
            Notify(text);
        }

        internal void AttachPoliceVisual(uint netId)
        {
            if (netId == 0 || _policeVisualAttached)
                return;

            if (NetworkClient.spawned.TryGetValue(netId, out NetworkIdentity identity) &&
                identity != null)
            {
                if (identity.GetComponent<PoliceVisual>() == null)
                    identity.gameObject.AddComponent<PoliceVisual>();
                _policeVisualAttached = true;
            }
        }

        private UndergroundStateMessage BuildStateMessage()
        {
            int count = _plots != null ? _plots.Length : 0;
            var message = new UndergroundStateMessage
            {
                DoorOpen = _doorOpen,
                States = new byte[count],
                PlantedAt = new float[count],
                Watered = new bool[count],
                Covered = new bool[count],
                PoliceNetId = _policeNetId,
                ClassicStock = _stock[0],
                PremiumStock = _stock[1]
            };

            for (int i = 0; i < count; i++)
            {
                CannabisPlot plot = _plots[i];
                message.States[i] = plot != null ? plot.State : CannabisPlot.Empty;
                message.PlantedAt[i] = plot != null ? plot.PlantedAt : 0f;
                message.Watered[i] = plot != null && plot.Watered;
                message.Covered[i] = plot != null && plot.Covered;
            }

            return message;
        }

        private void SendState()
        {
            if (!NetworkServer.active || !_networkReady)
                return;

            NetworkServer.SendToAll(BuildStateMessage());
        }

        internal void SendStateTo(NetworkConnectionToClient connection)
        {
            // Ne rien envoyer avant que l'hôte ait fini de construire son
            // sous-sol : le client redemandera son état.
            if (!NetworkServer.active || connection == null ||
                !_networkReady || _plots == null)
                return;

            connection.Send(BuildStateMessage());
        }

        internal void ApplyState(UndergroundStateMessage message)
        {
            if (_plots == null)
                return;

            bool wasReady = _networkReady;
            _doorOpen = message.DoorOpen;
            if (_door != null)
                _door.SetOpen(_doorOpen);

            int count = Mathf.Min(_plots.Length, message.States != null ? message.States.Length : 0);
            for (int i = 0; i < count; i++)
            {
                CannabisPlot plot = _plots[i];
                if (plot == null)
                    continue;

                plot.ApplyState(
                    message.States[i],
                    message.PlantedAt != null && i < message.PlantedAt.Length ? message.PlantedAt[i] : 0f,
                    message.Watered != null && i < message.Watered.Length && message.Watered[i],
                    message.Covered != null && i < message.Covered.Length && message.Covered[i]);
            }

            if (_policeNetId != message.PoliceNetId)
            {
                _policeNetId = message.PoliceNetId;
                _policeVisualAttached = false;
            }
            AttachPoliceVisual(_policeNetId);

            _stock[0] = Mathf.Clamp(message.ClassicStock, 0, StockCapacity);
            _stock[1] = Mathf.Clamp(message.PremiumStock, 0, StockCapacity);

            if (!NetworkServer.active && !wasReady)
            {
                _networkReady = true;
                _nextClientResync = Time.time + 30f;
                Plugin.Log.LogInfo("[Souterrain] État du sous-sol reçu de l'hôte.");
            }
        }

        private string GetSavePath()
        {
            string saveName = "default";
            try
            {
                string current = FsmVariables.GlobalVariables.GetFsmString("CurrentFilename").Value;
                if (!string.IsNullOrEmpty(current))
                    saveName = Path.GetFileNameWithoutExtension(current);
            }
            catch
            {
                // Le nom de sauvegarde n'est pas encore disponible au menu.
            }

            if (string.IsNullOrEmpty(saveName))
                saveName = "default";

            foreach (char invalid in Path.GetInvalidFileNameChars())
                saveName = saveName.Replace(invalid, '_');

            string directory = Path.Combine(Paths.ConfigPath, "ItemAdder");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "underground_" + saveName + ".txt");
        }

        private void SaveState()
        {
            if (!IsServer || _plots == null)
                return;

            try
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("version=2");
                builder.AppendLine("door=" + (_doorOpen ? "1" : "0"));
                builder.AppendLine("stock=" + _stock[0] + "|" + _stock[1]);
                for (int i = 0; i < _plots.Length; i++)
                {
                    CannabisPlot plot = _plots[i];
                    builder.Append("plot=");
                    builder.Append(plot != null ? plot.State.ToString(CultureInfo.InvariantCulture) : "0");
                    builder.Append('|');
                    builder.Append(plot != null ? plot.PlantedAt.ToString("0.###", CultureInfo.InvariantCulture) : "0");
                    builder.Append('|');
                    builder.Append(plot != null && plot.Watered ? "1" : "0");
                    builder.Append('|');
                    builder.Append(plot != null && plot.Covered ? "1" : "0");
                    builder.AppendLine();
                }

                File.WriteAllText(GetSavePath(), builder.ToString());
                _dirty = false;
            }
            catch (Exception exception)
            {
                Plugin.Log.LogWarning("[Souterrain] Sauvegarde impossible : " + exception.Message);
            }
        }

        private void LoadState()
        {
            try
            {
                string path = GetSavePath();
                if (!File.Exists(path))
                    return;

                string[] lines = File.ReadAllLines(path);
                bool doorOpen = true;
                int plotIndex = 0;

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.StartsWith("door="))
                    {
                        doorOpen = line.EndsWith("1");
                    }
                    else if (line.StartsWith("stock="))
                    {
                        string[] stockValues = line.Substring(6).Split('|');
                        if (stockValues.Length >= 2 &&
                            int.TryParse(stockValues[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int classic) &&
                            int.TryParse(stockValues[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int premium))
                        {
                            _stock[0] = Mathf.Clamp(classic, 0, StockCapacity);
                            _stock[1] = Mathf.Clamp(premium, 0, StockCapacity);
                        }
                    }
                    else if (line.StartsWith("plot=") && plotIndex < _plots.Length)
                    {
                        string[] values = line.Substring(5).Split('|');
                        if (values.Length >= 4 &&
                            byte.TryParse(values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte state) &&
                            float.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float plantedAt))
                        {
                            _plots[plotIndex].ApplyState(
                                state,
                                plantedAt,
                                values[2] == "1",
                                values[3] == "1");
                        }
                        plotIndex++;
                    }
                }

                _doorOpen = doorOpen;
                if (_door != null)
                    _door.SetOpen(doorOpen);
                Plugin.Log.LogInfo(
                    $"[Souterrain] État chargé depuis {Path.GetFileName(path)} " +
                    $"({_stock[0]} classique, {_stock[1]} premium en réserve).");
            }
            catch (Exception exception)
            {
                Plugin.Log.LogWarning("[Souterrain] Chargement impossible : " + exception.Message);
            }
        }

        private void DestroyBasement()
        {
            if (IsServer)
                SaveState();

            if (_police != null)
            {
                if (NetworkServer.active)
                    NetworkServer.Destroy(_police);
                else
                    Object.Destroy(_police);
                _police = null;
                _policeAgent = null;
                _policeNetId = 0;
            }

            if (_shelf != null)
            {
                if (NetworkServer.active)
                    NetworkServer.Destroy(_shelf.gameObject);
                else
                    Object.Destroy(_shelf.gameObject);
            }

            if (_layout != null && _layout.Root != null)
                Object.Destroy(_layout.Root);

            _layout = null;
            _plots = null;
            _door = null;
            _shelf = null;
            _patrolPoints = null;
            _built = false;
            _networkReady = false;
            _raidActive = false;
            _raidPhase = 0;
            _raidPhaseStarted = 0f;
            _lastPoliceRecovery = 0f;
            _doorOpen = true;
            _policeVisualAttached = false;
        }
    }
}
