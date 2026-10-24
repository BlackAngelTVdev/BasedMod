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
        private Data_Container _shelf;
        private Data_Container _checkout;
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
        private float _nextProductRefresh;
        private float _nextBuildAttempt;
        private float _nextSpawnRetry;
        private float _nextPrefabWarning;
        private float _nextClientResync;
        private float _nextPoliceVisualAttempt;
        private bool _policeVisualAttached;
        private bool _wasConnectedClient;
        private float _lastNotify;
        private int _patrolIndex;
        private Vector3[] _patrolPoints;

        internal bool IsServer => NetworkServer.active;

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

        internal bool CanSellHerbs
        {
            get
            {
                return IsServer && _shelf != null && _doorOpen &&
                       (GetShelfQuantity(0) > 0 || GetShelfQuantity(1) > 0);
            }
        }

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
                // on retente tant que le rayon, la caisse ou le policier manquent.
                _nextSpawnRetry = Time.time + 4f;
                if (_shelf == null || _checkout == null || _police == null)
                    SpawnNetworkObjects();
            }

            if (!IsServer && _policeNetId != 0 && !_policeVisualAttached &&
                Time.time >= _nextPoliceVisualAttempt)
            {
                _nextPoliceVisualAttempt = Time.time + 1f;
                AttachPoliceVisual(_policeNetId);
            }

            HandleLocalInput();
            UpdatePlots();
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

            Vector3 hatch = FindHatchPosition(minX, maxX, minZ, maxZ);
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

            Notify("Le sous-sol clandestin est aménagé. Trappe dans le magasin.");
            Plugin.Log.LogInfo("[Souterrain] Sous-sol prêt.");

            if (NetworkServer.active)
            {
                SpawnNetworkObjects();
                LoadState();
                _networkReady = true;
            }
        }

        private static Vector3 FindHatchPosition(float minX, float maxX, float minZ, float maxZ)
        {
            float centerX = (minX + maxX) * 0.5f;
            float centerZ = (minZ + maxZ) * 0.5f;

            Vector3[] candidates =
            {
                new Vector3(centerX, 0f, centerZ),
                new Vector3(centerX - 3f, 0f, centerZ - 3f),
                new Vector3(centerX + 3f, 0f, centerZ - 3f),
                new Vector3(centerX - 3f, 0f, centerZ + 3f),
                new Vector3(centerX + 3f, 0f, centerZ + 3f),
                new Vector3(minX + 3f, 0f, minZ + 3f),
                new Vector3(maxX - 3f, 0f, minZ + 3f),
                new Vector3(minX + 3f, 0f, maxZ - 3f),
                new Vector3(maxX - 3f, 0f, maxZ - 3f)
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                Vector3 candidate = candidates[i];
                if (candidate.x < minX + 2f || candidate.x > maxX - 2f ||
                    candidate.z < minZ + 2f || candidate.z > maxZ - 2f)
                    continue;

                if (!Physics.Raycast(candidate + Vector3.up * 2.5f, Vector3.down,
                        out RaycastHit hit, 3.5f, ~0, QueryTriggerInteraction.Ignore))
                    continue;

                if (hit.point.y < -1f || hit.point.y > 1f)
                    continue;

                Collider[] overlaps = Physics.OverlapBox(
                    candidate + Vector3.up * 1.0f,
                    new Vector3(1.35f, 0.55f, 1.35f),
                    Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);

                bool blocked = false;
                for (int j = 0; j < overlaps.Length; j++)
                {
                    if (overlaps[j] == null)
                        continue;
                    if (overlaps[j].GetComponentInParent<FirstPersonController>() != null)
                        continue;
                    // Les PNJ se déplacent d'un client à l'autre : ils ne doivent
                    // pas changer l'emplacement de la trappe, sinon les sous-sols
                    // construits localement ne se superposent plus en multi.
                    if (overlaps[j].GetComponentInParent<NPC_Info>() != null)
                        continue;
                    blocked = true;
                    break;
                }

                if (!blocked)
                    return new Vector3(candidate.x, 0f, candidate.z);
            }

            return new Vector3(centerX, 0f, centerZ);
        }

        private void SpawnNetworkObjects()
        {
            if (!NetworkServer.active)
                return;

            CreateHerbShelf();
            CreateCheckout();
            SpawnPolice();
            RefreshProducts();
        }

        private void CreateHerbShelf()
        {
            if (_shelf != null || _layout == null)
                return;

            NetworkSpawner spawner = GameData.Instance.GetComponent<NetworkSpawner>();
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

            GameObject shelfObject = Object.Instantiate(
                prefab, _layout.ShelfPosition, Quaternion.Euler(0f, 180f, 0f));
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

            Plugin.Log.LogInfo(
                $"[Souterrain] Étagère à herbes placée en {_layout.ShelfPosition} " +
                $"({chosen.name}, classe {chosen.containerClass}, parent {chosen.parentIndex}, " +
                $"standspot {(chosen.standSpot != null ? "oui" : "non")}).");
        }

        private void CreateCheckout()
        {
            if (_checkout != null || _layout == null)
                return;

            NetworkSpawner spawner = GameData.Instance.GetComponent<NetworkSpawner>();
            GameObject prefab = null;

            for (int i = 0; i < spawner.buildables.Length; i++)
            {
                GameObject candidate = spawner.buildables[i];
                if (candidate == null)
                    continue;

                Data_Container data = candidate.GetComponent<Data_Container>();
                if (data == null || data.containerClass != 99)
                    continue;

                // 12 = caisse automatique : elle utilise selfCheckoutOBJ, pas
                // checkoutOBJ, et ne doit donc pas être placée ici.
                if (data.containerID == 12)
                    continue;

                prefab = candidate;
                if (data.parentIndex == 2)
                    break;
            }

            if (prefab == null)
            {
                if (Time.time >= _nextPrefabWarning)
                {
                    _nextPrefabWarning = Time.time + 30f;
                    Plugin.Log.LogWarning(
                        "[Souterrain] Aucune caisse constructible trouvée. Candidats : " +
                        DescribeBuildables(spawner));
                }
                return;
            }

            GameObject checkoutObject = Object.Instantiate(
                prefab, _layout.CheckoutPosition, Quaternion.Euler(0f, 180f, 0f));
            checkoutObject.name = "Underground_Checkout";
            Data_Container dataContainer = checkoutObject.GetComponent<Data_Container>();
            if (dataContainer == null)
            {
                Object.Destroy(checkoutObject);
                return;
            }

            // Même principe pour la caisse : elle doit figurer dans la liste
            // des caisses connues pour que les clients viennent y payer.
            Transform checkoutParent = NPC_Manager.Instance.checkoutOBJ != null
                ? NPC_Manager.Instance.checkoutOBJ.transform
                : null;
            if (checkoutParent == null && dataContainer.parentIndex >= 0 &&
                dataContainer.parentIndex < spawner.levelPropsOBJ.transform.childCount)
                checkoutParent = spawner.levelPropsOBJ.transform.GetChild(dataContainer.parentIndex);
            if (checkoutParent == null)
                checkoutParent = spawner.levelPropsOBJ.transform.GetChild(2);
            checkoutObject.transform.SetParent(checkoutParent, true);

            NetworkServer.Spawn(checkoutObject);
            _checkout = dataContainer;
            Plugin.Log.LogInfo(
                $"[Souterrain] Caisse placée en {_layout.CheckoutPosition} ({prefab.name}).");
        }

        /// <summary>
        /// Résumé des meubles constructibles, utile dans le log quand aucun
        /// rayon/caisse n'est reconnu : on voit immédiatement les classes et
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
            if (Vector3.Distance(playerPosition, _layout.Hatch) < 2.8f && action)
            {
                RequestDoorToggle();
                return;
            }

            CannabisPlot plot = FindClosestPlot(player);
            if (plot == null)
                return;

            if (action)
                RequestPlotAction(plot);
            else if (cover)
                RequestPlotAction(plot, true);
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
                        Notify("Le rayon à herbes est plein. Vends la récolte avant de récolter.", requester);
                        return;
                    }

                    plot.State = CannabisPlot.Empty;
                    plot.PlantedAt = 0f;
                    plot.Watered = false;
                    plot.Covered = false;
                    plot.RefreshVisual();
                    Notify($"Récolte : {amount} unités ajoutées au rayon du sous-sol.", requester);
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

            if (IsServer && _checkout != null)
            {
                bool shouldClose = !open;
                if (_checkout.isCheckoutClosed != shouldClose)
                    _checkout.CmdCloseCheckout();
            }

            if (IsServer)
            {
                _patrolPoints = BuildPatrolPoints();
                _patrolIndex = 0;
                if (_patrolPoints.Length > 0)
                    SetPoliceDestination(_patrolPoints[0]);
            }

            RefreshProducts();
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

            ClearShelf();
            string message = $"Raid de police : amende de {fine:0} $ et récolte confisquée.";
            Notify(message);
            SendAlert(message);
            Plugin.Log.LogWarning($"[Souterrain] Amende appliquée : {fine:0} $.");
            MarkDirty();
            SaveState();
        }

        private bool HasHerbStock()
        {
            return GetShelfQuantity(0) > 0 || GetShelfQuantity(1) > 0;
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

        private void RefreshProducts()
        {
            if (Time.time < _nextProductRefresh)
                return;

            _nextProductRefresh = Time.time + 0.75f;
            UndergroundCatalog.RefreshAvailability();
        }

        private int GetShelfQuantity(int row)
        {
            if (_shelf == null || _shelf.productInfoArray == null)
                return 0;

            int index = row * 2 + 1;
            return index < _shelf.productInfoArray.Length
                ? Mathf.Max(0, _shelf.productInfoArray[index])
                : 0;
        }

        private bool AddHarvest(int productId, int amount)
        {
            if (_shelf == null)
                return false;

            int row = productId == UndergroundCatalog.PremiumProductId ? 1 : 0;
            int current = GetShelfQuantity(row);
            int next = Mathf.Clamp(current + amount, 0, 24);
            if (next == current)
                return false;

            _shelf.DebugAdd(row * 2, productId, next);
            RefreshProducts();
            return true;
        }

        private void ClearShelf()
        {
            if (_shelf == null)
                return;

            _shelf.CmdContainerClear(0);
            _shelf.CmdContainerClear(2);
            RefreshProducts();
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
                PoliceNetId = _policeNetId
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
            RefreshProducts();

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
                builder.AppendLine("version=1");
                builder.AppendLine("door=" + (_doorOpen ? "1" : "0"));
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
                if (IsServer && _checkout != null)
                {
                    bool shouldClose = !doorOpen;
                    if (_checkout.isCheckoutClosed != shouldClose)
                        _checkout.CmdCloseCheckout();
                }

                RefreshProducts();
                Plugin.Log.LogInfo($"[Souterrain] État chargé depuis {Path.GetFileName(path)}.");
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

            if (_checkout != null)
            {
                if (NetworkServer.active)
                    NetworkServer.Destroy(_checkout.gameObject);
                else
                    Object.Destroy(_checkout.gameObject);
            }

            if (_layout != null && _layout.Root != null)
                Object.Destroy(_layout.Root);

            _layout = null;
            _plots = null;
            _door = null;
            _shelf = null;
            _checkout = null;
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
