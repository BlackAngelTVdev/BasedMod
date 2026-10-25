using System;
using Mirror;
using UnityEngine;

namespace ItemAdder.Underground
{
    public struct UndergroundStateMessage : NetworkMessage
    {
        public bool DoorOpen;
        public byte[] States;
        public float[] PlantedAt;
        public bool[] Watered;
        public bool[] Covered;
        public uint PoliceNetId;
        public int ClassicStock;
        public int PremiumStock;
    }

    public struct UndergroundActionMessage : NetworkMessage
    {
        public byte Action;
        public int Plot;
    }

    public struct UndergroundHelloMessage : NetworkMessage
    {
    }

    public struct UndergroundAlertMessage : NetworkMessage
    {
        public string Text;
    }

    public struct UndergroundPoliceMessage : NetworkMessage
    {
        public uint NetId;
    }

    public struct UndergroundNotifyMessage : NetworkMessage
    {
        public string Text;
    }

    internal static class UndergroundNetwork
    {
        internal const byte ActionPlant = 0;
        internal const byte ActionWater = 1;
        internal const byte ActionHarvest = 2;
        internal const byte ActionToggleCover = 3;
        internal const byte ActionToggleDoor = 4;
        internal const byte ActionSell = 5;

        private static bool _serializersReady;

        internal static void Register()
        {
            if (!_serializersReady)
            {
                _serializersReady = true;

                // Mirror génère normalement ces sérialiseurs via son weaver, qui ne
                // s'exécute pas sur un DLL de mod. Sans ces délégués, l'envoi d'un
                // message personnalisé échoue au moment de l'écriture.
                Writer<UndergroundStateMessage>.write = WriteState;
                Reader<UndergroundStateMessage>.read = ReadState;
                Writer<UndergroundActionMessage>.write = WriteAction;
                Reader<UndergroundActionMessage>.read = ReadAction;
                Writer<UndergroundAlertMessage>.write = WriteAlert;
                Reader<UndergroundAlertMessage>.read = ReadAlert;
                Writer<UndergroundPoliceMessage>.write = WritePolice;
                Reader<UndergroundPoliceMessage>.read = ReadPolice;
                Writer<UndergroundNotifyMessage>.write = WriteNotify;
                Reader<UndergroundNotifyMessage>.read = ReadNotify;
                Writer<UndergroundHelloMessage>.write = WriteHello;
                Reader<UndergroundHelloMessage>.read = ReadHello;
            }

            InstallClientHandlers();
            InstallServerHandlers();
        }

        /// <summary>
        /// Mirror vide ses dictionnaires de handlers dans NetworkClient.Shutdown()
        /// et NetworkServer.Shutdown(), qui tournent au chargement de scène. Un
        /// enregistrement fait seulement dans Awake est donc perdu avant la
        /// première partie : on réinstalle à chaque (ré)initialisation du réseau.
        /// </summary>
        internal static void InstallClientHandlers()
        {
            NetworkClient.ReplaceHandler<UndergroundStateMessage>(OnState);
            NetworkClient.ReplaceHandler<UndergroundAlertMessage>(OnAlert);
            NetworkClient.ReplaceHandler<UndergroundPoliceMessage>(OnPolice);
            NetworkClient.ReplaceHandler<UndergroundNotifyMessage>(OnNotify);
        }

        internal static void InstallServerHandlers()
        {
            NetworkServer.ReplaceHandler<UndergroundActionMessage>(OnAction, false);
            NetworkServer.ReplaceHandler<UndergroundHelloMessage>(OnHello, false);
        }

        private static void OnState(UndergroundStateMessage message)
        {
            UndergroundManager.Instance?.ApplyState(message);
        }

        private static void OnAlert(UndergroundAlertMessage message)
        {
            UndergroundManager.Instance?.ShowAlert(message.Text);
        }

        private static void OnPolice(UndergroundPoliceMessage message)
        {
            UndergroundManager.Instance?.AttachPoliceVisual(message.NetId);
        }

        private static void OnAction(NetworkConnectionToClient connection, UndergroundActionMessage message)
        {
            UndergroundManager.Instance?.HandleNetworkAction(connection, message.Action, message.Plot);
        }

        private static void OnNotify(UndergroundNotifyMessage message)
        {
            UndergroundManager.Instance?.ShowAlert(message.Text);
        }

        private static void OnHello(NetworkConnectionToClient connection, UndergroundHelloMessage message)
        {
            UndergroundManager.Instance?.SendStateTo(connection);
        }

        internal static void SendAction(byte action, int plot)
        {
            if (!NetworkClient.isConnected)
                return;

            NetworkClient.Send(new UndergroundActionMessage
            {
                Action = action,
                Plot = plot
            });
        }

        private static void WriteState(NetworkWriter writer, UndergroundStateMessage value)
        {
            writer.WriteBool(value.DoorOpen);
            writer.WriteUInt(value.PoliceNetId);
            writer.WriteArray(value.States ?? Array.Empty<byte>());
            writer.WriteArray(value.PlantedAt ?? Array.Empty<float>());
            writer.WriteArray(value.Watered ?? Array.Empty<bool>());
            writer.WriteArray(value.Covered ?? Array.Empty<bool>());
            writer.WriteInt(value.ClassicStock);
            writer.WriteInt(value.PremiumStock);
        }

        private static UndergroundStateMessage ReadState(NetworkReader reader)
        {
            return new UndergroundStateMessage
            {
                DoorOpen = reader.ReadBool(),
                PoliceNetId = reader.ReadUInt(),
                States = reader.ReadArray<byte>(),
                PlantedAt = reader.ReadArray<float>(),
                Watered = reader.ReadArray<bool>(),
                Covered = reader.ReadArray<bool>(),
                ClassicStock = reader.ReadInt(),
                PremiumStock = reader.ReadInt()
            };
        }

        private static void WriteAction(NetworkWriter writer, UndergroundActionMessage value)
        {
            writer.WriteByte(value.Action);
            writer.WriteInt(value.Plot);
        }

        private static UndergroundActionMessage ReadAction(NetworkReader reader)
        {
            return new UndergroundActionMessage
            {
                Action = reader.ReadByte(),
                Plot = reader.ReadInt()
            };
        }

        private static void WriteAlert(NetworkWriter writer, UndergroundAlertMessage value)
        {
            writer.WriteString(value.Text ?? string.Empty);
        }

        private static UndergroundAlertMessage ReadAlert(NetworkReader reader)
        {
            return new UndergroundAlertMessage { Text = reader.ReadString() };
        }

        private static void WritePolice(NetworkWriter writer, UndergroundPoliceMessage value)
        {
            writer.WriteUInt(value.NetId);
        }

        private static UndergroundPoliceMessage ReadPolice(NetworkReader reader)
        {
            return new UndergroundPoliceMessage { NetId = reader.ReadUInt() };
        }

        private static void WriteNotify(NetworkWriter writer, UndergroundNotifyMessage value)
        {
            writer.WriteString(value.Text ?? string.Empty);
        }

        private static UndergroundNotifyMessage ReadNotify(NetworkReader reader)
        {
            return new UndergroundNotifyMessage { Text = reader.ReadString() };
        }

        private static void WriteHello(NetworkWriter writer, UndergroundHelloMessage value)
        {
        }

        private static UndergroundHelloMessage ReadHello(NetworkReader reader)
        {
            return default(UndergroundHelloMessage);
        }
    }
}
