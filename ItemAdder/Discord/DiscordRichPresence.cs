using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ItemAdder.Discord
{
    /// <summary>
    /// Publie un statut « jeu en cours » dans le client Discord.
    /// Aucune dépendance externe : on parle directement le protocole IPC de
    /// Discord sur son tube nommé <c>discord-ipc-0..9</c>, en tâche de fond,
    /// pour ne jamais bloquer le thread du jeu.
    /// </summary>
    /// <remarks>
    /// Le nom affiché par Discord (« Joue à … ») est le nom de l'application
    /// Discord dont l'identifiant est fourni ici : il n'est pas modifiable par
    /// le protocole. Les deux lignes suivantes viennent de <see cref="_details"/>
    /// et <see cref="_state"/>.
    /// </remarks>
    internal sealed class DiscordRichPresence : IDisposable
    {
        private const int OpHandshake = 0;
        private const int OpFrame = 1;
        private const int OpClose = 2;
        private const int OpPing = 3;
        private const int OpPong = 4;

        /// <summary>Discord tronque details/state au-delà de 128 caractères.</summary>
        private const int MaxTextLength = 128;

        /// <summary>Bouton cliquable de la carte Discord (32 caractères max).</summary>
        private const string ButtonLabel = "Télécharger le mod";

        private const string ButtonUrl = "https://github.com/BlackAngelTVdev/BasedMod/releases";

        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan FailureDelay = TimeSpan.FromSeconds(5);

        private readonly string _clientId;
        private readonly string _pipeNamePrefix;
        private readonly string _details;
        private readonly string _state;
        private readonly long _startedAtMs;
        private readonly Action<string> _log;
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();

        private int _disposed;

        public DiscordRichPresence(
            string clientId,
            string details,
            string state,
            Action<string> log,
            string pipeNamePrefix = "discord-ipc-")
        {
            _clientId = clientId;
            _pipeNamePrefix = pipeNamePrefix;
            _details = Trim(details);
            _state = Trim(state);
            _log = log;
            _startedAtMs = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                .TotalMilliseconds;
        }

        public void Start()
        {
            Task.Run(() => RunAsync(_cancellation.Token));
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            try
            {
                _cancellation.Cancel();
                _cancellation.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Déjà libéré : rien à faire.
            }
        }

        private async Task RunAsync(CancellationToken token)
        {
            bool waitingReported = false;
            bool keepTrying = true;

            while (keepTrying && !token.IsCancellationRequested)
            {
                TimeSpan delay = RetryDelay;

                try
                {
                    NamedPipeClientStream pipe = await TryConnectAsync(token).ConfigureAwait(false);
                    if (pipe is null)
                    {
                        if (!waitingReported)
                        {
                            _log("[Discord] Client Discord fermé : le statut sera publié dès son démarrage.");
                            waitingReported = true;
                        }
                    }
                    else
                    {
                        using (pipe)
                        {
                            keepTrying = await PublishAsync(pipe, token).ConfigureAwait(false);
                        }

                        waitingReported = false;
                        // Discord a fermé la connexion (redémarrage ou fermeture) :
                        // on retente sans attendre le délai complet.
                        delay = FailureDelay;
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _log("[Discord] Statut interrompu : " + ex.Message);
                    delay = FailureDelay;
                }

                try
                {
                    await Task.Delay(delay, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Cherche un client Discord ouvert. Discord expose un tube par instance
        /// (discord-ipc-0 pour la première, etc.).
        /// </summary>
        private async Task<NamedPipeClientStream> TryConnectAsync(CancellationToken token)
        {
            for (int index = 0; index < 10; index++)
            {
                token.ThrowIfCancellationRequested();

                var pipe = new NamedPipeClientStream(
                    ".",
                    _pipeNamePrefix + index.ToString(),
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);

                try
                {
                    await pipe.ConnectAsync(250).ConfigureAwait(false);
                    return pipe;
                }
                catch (Exception)
                {
                    pipe.Dispose();
                }
            }

            return null;
        }

        /// <summary>
        /// Envoie la poignée de main puis le statut, et reste à l'écoute tant que
        /// Discord garde la connexion ouverte (PING, fermeture, erreurs).
        /// </summary>
        /// <summary>
        /// Retourne <c>false</c> quand réessayer ne servirait à rien (identifiant
        /// refusé par Discord), <c>true</c> quand la connexion doit être retentée.
        /// </summary>
        private async Task<bool> PublishAsync(NamedPipeClientStream pipe, CancellationToken token)
        {
            await WriteFrameAsync(
                    pipe,
                    OpHandshake,
                    "{\"v\":1,\"client_id\":\"" + _clientId + "\"}",
                    token)
                .ConfigureAwait(false);

            bool published = false;

            while (!token.IsCancellationRequested && pipe.IsConnected)
            {
                (int opcode, string payload) = await ReadFrameAsync(pipe, token).ConfigureAwait(false);

                if (opcode < 0)
                {
                    // Connexion fermée par Discord.
                    return true;
                }

                if (opcode == OpPing)
                {
                    await WriteFrameAsync(pipe, OpPong, payload, token).ConfigureAwait(false);
                    continue;
                }

                if (opcode == OpClose)
                {
                    // 4000 = « Invalid Client ID » : inutile de réessayer à chaque
                    // passage, le réglage lui-même est faux.
                    if (payload.IndexOf("4000", StringComparison.Ordinal) >= 0)
                    {
                        _log(
                            "[Discord] Identifiant d'application refusé par Discord (Invalid Client ID) : " +
                            "vérifie [Discord] IdentifiantApplication dans le launcher ou le .cfg.");
                        return false;
                    }

                    return true;
                }

                if (opcode != OpFrame)
                {
                    continue;
                }

                if (payload.IndexOf("\"evt\":\"ERROR\"", StringComparison.Ordinal) >= 0)
                {
                    _log("[Discord] Discord a refusé le statut : " + payload);
                    return false;
                }

                if (!published && payload.IndexOf("\"evt\":\"READY\"", StringComparison.Ordinal) >= 0)
                {
                    await WriteFrameAsync(pipe, OpFrame, BuildActivityJson(), token).ConfigureAwait(false);
                    published = true;
                    _log($"[Discord] Statut publié : « {_details} » / « {_state} ».");
                }
            }

            return true;
        }

        private string BuildActivityJson()
        {
            int processId = Process.GetCurrentProcess().Id;

            var json = new StringBuilder(320);
            json.Append("{\"cmd\":\"SET_ACTIVITY\",\"nonce\":\"")
                .Append(Guid.NewGuid().ToString())
                .Append("\",\"args\":{\"pid\":")
                .Append(processId)
                .Append(",\"activity\":{\"type\":0");

            // « type » est déjà écrit : chaque champ suivant commence par une virgule.
            if (_details.Length >= 2)
            {
                json.Append(",\"details\":\"").Append(Escape(_details)).Append('"');
            }

            if (_state.Length >= 2)
            {
                json.Append(",\"state\":\"").Append(Escape(_state)).Append('"');
            }

            json.Append(",\"timestamps\":{\"start\":").Append(_startedAtMs).Append('}');
            json.Append(",\"buttons\":[{\"label\":\"")
                .Append(Escape(ButtonLabel))
                .Append("\",\"url\":\"")
                .Append(Escape(ButtonUrl))
                .Append("\"}]");

            // Ferme activity, args puis l'objet racine.
            json.Append(",\"instance\":false}}}");

            return json.ToString();
        }

        private static async Task WriteFrameAsync(
            Stream pipe,
            int opcode,
            string payload,
            CancellationToken token)
        {
            byte[] body = Encoding.UTF8.GetBytes(payload);
            byte[] frame = new byte[body.Length + 8];

            Buffer.BlockCopy(BitConverter.GetBytes(opcode), 0, frame, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(body.Length), 0, frame, 4, 4);
            Buffer.BlockCopy(body, 0, frame, 8, body.Length);

            await pipe.WriteAsync(frame, 0, frame.Length, token).ConfigureAwait(false);
            await pipe.FlushAsync(token).ConfigureAwait(false);
        }

        /// <summary>Lit une trame ; opcode négatif = connexion fermée.</summary>
        private static async Task<(int Opcode, string Payload)> ReadFrameAsync(
            Stream pipe,
            CancellationToken token)
        {
            byte[] header = new byte[8];
            if (!await ReadExactAsync(pipe, header, token).ConfigureAwait(false))
            {
                return (-1, string.Empty);
            }

            int opcode = BitConverter.ToInt32(header, 0);
            int length = BitConverter.ToInt32(header, 4);

            if (length <= 0 || length > 1 << 20)
            {
                return (opcode, string.Empty);
            }

            byte[] body = new byte[length];
            if (!await ReadExactAsync(pipe, body, token).ConfigureAwait(false))
            {
                return (-1, string.Empty);
            }

            return (opcode, Encoding.UTF8.GetString(body));
        }

        private static async Task<bool> ReadExactAsync(Stream pipe, byte[] buffer, CancellationToken token)
        {
            int offset = 0;

            while (offset < buffer.Length)
            {
                int read = await pipe
                    .ReadAsync(buffer, offset, buffer.Length - offset, token)
                    .ConfigureAwait(false);

                if (read <= 0)
                {
                    return false;
                }

                offset += read;
            }

            return true;
        }

        private static string Trim(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            string trimmed = value.Trim();
            if (trimmed.Length > MaxTextLength)
            {
                trimmed = trimmed.Substring(0, MaxTextLength);
            }

            return trimmed;
        }

        private static string Escape(string value)
        {
            var escaped = new StringBuilder(value.Length + 8);

            foreach (char character in value)
            {
                switch (character)
                {
                    case '"':
                        escaped.Append("\\\"");
                        break;
                    case '\\':
                        escaped.Append("\\\\");
                        break;
                    case '\n':
                        escaped.Append("\\n");
                        break;
                    case '\r':
                        escaped.Append("\\r");
                        break;
                    case '\t':
                        escaped.Append("\\t");
                        break;
                    default:
                        if (character < 0x20)
                        {
                            escaped.Append("\\u").Append(((int)character).ToString("x4"));
                        }
                        else
                        {
                            escaped.Append(character);
                        }

                        break;
                }
            }

            return escaped.ToString();
        }
    }
}
