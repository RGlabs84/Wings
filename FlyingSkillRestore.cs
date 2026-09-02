using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HarmonyLib;

namespace WingsoftheValkyrie
{
    /// <summary>
    /// The one-time repair for the levels 2.1.0 destroyed.
    ///
    /// Nothing is left on an affected character to restore from -- the reset level was written
    /// over the real one on the first save after each login. The server, however, still holds a
    /// row per pilot in <c>flight_registry.dat</c> carrying the level as of their last flight,
    /// and that row is only rewritten when the pilot flies again. So on its first run the server
    /// freezes those levels into a snapshot, and hands a pilot's own number back to them once,
    /// the first time they load in. After that the character carries its level normally and this
    /// never speaks again.
    ///
    /// Three things make this safe to leave switched on:
    /// it only ever RAISES a level, never lowers one, so it cannot undo a death penalty or a
    /// deliberate admin reset; the server answers only with the number recorded against the
    /// asking character's own id; and each character is stamped once the exchange completes, so
    /// the repair cannot be taken twice.
    /// </summary>
    [HarmonyPatch]
    public static class FlyingSkillRestore
    {
        /// <summary>Deliberately not a `barrkbot_*.json`: the export sweep must never see it.</summary>
        private const string SnapshotFileName = "flight_skill_restore.dat";

        private const string CustomDataKey = "wubarrk.wotv.skillrestore";

        /// <summary>Versioned so 2.1.4's bare "done" marks can be told apart and disregarded.</summary>
        private const string StampPrefix = "v2:";
        /// <summary>How long between attempts to reach the server, and how many to make before
        /// giving up. Twenty seconds in total: long enough to outlast a slow connect, short
        /// enough that a character on a server without the mod is not asking forever.</summary>
        private const float RetrySeconds = 2f;
        private const int MaxAttempts = 10;

        private const string AskRpc = "WotV_SkillRestoreAsk";
        private const string TellRpc = "WotV_SkillRestoreTell";

        // Server side: the frozen "level as it was" per character id.
        private static readonly Dictionary<long, float> Snapshot = new Dictionary<long, float>();

        // Same reasoning as FlightReport: the routed RPC object is rebuilt per network session,
        // so holding the instance we registered against re-registers on joining another world.
        private static ZRoutedRpc _registeredOn;

        // Keyed by character id, not a bare flag: switching characters inside one session
        // must still let the new character ask its own question.
        // The character waiting to be asked about, and the retry state for asking. A request
        // cannot simply be fired the moment the character loads: the routed RPC and the server
        // peer are not reliably in place yet, and a send into a half-built session is silently
        // dropped. So the need is recorded here and Tick keeps trying until it is answered.
        private static long _pendingId;
        private static float _pendingTimer;
        private static int _attempts;

        public static void Register()
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            if (rpc == null || ReferenceEquals(_registeredOn, rpc)) return;

            try
            {
                rpc.Register<long>(AskRpc, OnAsked);
                rpc.Register<string>(TellRpc, OnTold);
                _registeredOn = rpc;

                // NOTHING to do with the snapshot here. This used to clear it, which was the
                // 2.1.4 bug: the plugin drives FlightReport.Tick first, so the registry had
                // already been read and the snapshot filled by the time this ran -- and
                // FlightReport latches _registryLoaded, so the capture could never happen
                // again. The server then answered every pilot with "nothing on record", and
                // each of them spent their one repair on that empty answer. The snapshot is
                // built from a FILE, not from the network session, and is reset by the capture
                // itself when a new session re-reads the registry.
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Could not register the skill restore RPC; lost Valkyrie Flight levels will have to be handed back by hand. Reason: {ex.Message}");
            }
        }

        // ---- server: the snapshot ----------------------------------------------------------------

        /// <summary>
        /// Called by <see cref="FlightReport"/> the instant the registry is read off disk. Loads
        /// the snapshot if one already exists -- an existing file is never rewritten, because it
        /// was taken when the numbers were still good and the registry behind it may not be any
        /// more -- and otherwise writes one from the rows just loaded.
        /// </summary>
        internal static void CaptureSnapshot(int rowCount, Action<Action<long, string, float>> visitRows)
        {
            // Called once per registry read; a new network session re-reads and so rebuilds.
            Snapshot.Clear();

            try
            {
                string path = Path.Combine(FlightReport.ExportDirectory(), SnapshotFileName);

                if (File.Exists(path))
                {
                    foreach (string line in File.ReadAllLines(path))
                    {
                        if (string.IsNullOrEmpty(line)) continue;

                        // "<id>|<name>|<level>" -- the level is after the LAST separator, because
                        // a character name may contain anything at all, separators included.
                        int last = line.LastIndexOf('|');
                        int first = line.IndexOf('|');
                        if (first <= 0 || last <= first) continue;

                        if (!long.TryParse(line.Substring(0, first), NumberStyles.Integer, CultureInfo.InvariantCulture, out long id)) continue;
                        if (!float.TryParse(line.Substring(last + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out float level)) continue;

                        Snapshot[id] = level;
                    }

                    Log.LogInfo($"Valkyrie Flight restore snapshot loaded: {Snapshot.Count} pilot(s) on file.");
                    return;
                }

                if (rowCount <= 0 || visitRows == null) return;

                var lines = new List<string>();
                visitRows((id, name, level) =>
                {
                    if (id == 0L || level <= 0f) return;
                    Snapshot[id] = level;
                    lines.Add(id.ToString(CultureInfo.InvariantCulture) + "|" + (name ?? "Unknown") + "|" +
                              level.ToString("R", CultureInfo.InvariantCulture));
                });

                if (lines.Count == 0) return;

                File.WriteAllLines(path, lines.ToArray());
                Log.LogInfo(
                    $"Wrote the Valkyrie Flight restore snapshot for {lines.Count} pilot(s) to {path}. " +
                    "Each of them will have their level handed back once, the first time they load in. " +
                    "This file is written once and never rewritten -- keep a copy.");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Could not prepare the Valkyrie Flight restore snapshot: {ex.Message}");
            }
        }

        private static void OnAsked(long sender, long playerId)
        {
            if (ZNet.instance != null && !ZNet.instance.IsServer()) return;

            // Answered even when nothing is on record. The reply is what stamps the character
            // as repaired, so staying silent would leave every pilot who never lost anything
            // asking the same question on every login for the life of the character.
            float level = Lookup(playerId);

            try
            {
                ZRoutedRpc rpc = ZRoutedRpc.instance;
                if (rpc == null) return;

                Log.LogInfo(level > 0f
                    ? $"Skill restore: pilot {playerId} asked; answering with Valkyrie Flight {level}."
                    : $"Skill restore: pilot {playerId} asked; nothing on record for them.");

                rpc.InvokeRoutedRPC(sender, TellRpc, level.ToString("R", CultureInfo.InvariantCulture));
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Could not answer a skill restore request: {ex.Message}");
            }
        }

        private static float Lookup(long playerId)
        {
            // Forces the registry read, which is what builds or loads the snapshot.
            FlightReport.EnsureSnapshot();
            return Snapshot.TryGetValue(playerId, out float level) ? level : 0f;
        }

        // ---- client: ask once, restore once ------------------------------------------------------

        /// <summary>
        /// Notes that this character still has its one repair to claim. Player.Load is only ever
        /// called for the character the profile belongs to -- remote players arrive over the
        /// network and never come through here -- so unlike OnSpawned it needs no local-player
        /// check. That check was the bug: Player.m_localPlayer is not reliably assigned yet when
        /// OnSpawned runs, so the request was being abandoned before it was ever sent.
        /// </summary>
        [HarmonyPatch(typeof(Player), "Load")]
        [HarmonyPostfix]
        private static void LoadPostfix(Player __instance)
        {
            if (__instance == null || !FlyingSkill.IsAvailable) return;
            if (IsStamped(__instance)) return;

            long id = __instance.GetPlayerID();
            if (id == 0L) return;

            _pendingId = id;
            _pendingTimer = 0f;
            _attempts = 0;
        }

        /// <summary>
        /// Sends the request once the session can actually carry it, and keeps trying for a short
        /// while if it cannot yet. Called every frame by the plugin.
        /// </summary>
        public static void Tick(float deltaTime)
        {
            Register();

            if (_pendingId == 0L) return;

            if (!ModConfig.RestoreLostSkillLevels.Value) { _pendingId = 0L; return; }

            _pendingTimer += deltaTime;
            if (_pendingTimer < RetrySeconds) return;
            _pendingTimer = 0f;

            try
            {
                // Solo play and a listen server are both "the server is right here".
                if (ZNet.instance == null || ZNet.instance.IsServer())
                {
                    Apply(Lookup(_pendingId));
                    return;
                }

                ZRoutedRpc rpc = ZRoutedRpc.instance;
                ZNetPeer server = ZNet.instance.GetServerPeer();

                if (rpc != null && server != null)
                {
                    rpc.InvokeRoutedRPC(server.m_uid, AskRpc, _pendingId);
                }

                if (++_attempts < MaxAttempts) return;

                Log.LogWarning(
                    "The server never answered about this character's Valkyrie Flight level. If a level " +
                    "went missing it has not been restored; an admin can hand it back with " +
                    "'resetskill ValkyrieFlight' then 'raiseskill ValkyrieFlight <level>'.");
                _pendingId = 0L;
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Could not ask the server about your Valkyrie Flight level: {ex.Message}");
                _pendingId = 0L;
            }
        }

        private static void OnTold(long sender, string payload)
        {
            if (string.IsNullOrEmpty(payload)) return;
            if (!float.TryParse(payload, NumberStyles.Float, CultureInfo.InvariantCulture, out float level)) return;

            Apply(level);   // a level of 0 restores nothing but still spends the repair
        }

        private static void Apply(float recorded)
        {
            Player player = Player.m_localPlayer;

            // The answer can outrun the local player reference on a slow load. Leaving the
            // request pending means Tick simply asks again rather than losing the repair.
            if (player == null || !FlyingSkill.IsAvailable) return;

            _pendingId = 0L;
            if (IsStamped(player)) return;

            // An answer of zero means the server had nothing on record -- which is also exactly
            // what a broken lookup returns. Spending the one repair on it is how 2.1.4 quietly
            // used up three pilots' restores. Leave the character unstamped: at worst it asks
            // again next login, which costs one small message.
            if (recorded <= 0f) return;

            float current = FlyingSkill.Level(player);

            // Only ever upward. A pilot who is already at or above the recorded level has either
            // never lost anything or has already earned it back, and a death penalty taken since
            // must not be quietly refunded.
            if (recorded > current && FlyingSkill.SetLevel(player, recorded, 0f))
            {
                Log.LogInfo($"Restored Valkyrie Flight from {current} to {recorded} for {player.GetPlayerName()}.");
                player.Message(MessageHud.MessageType.Center,
                    $"The Valkyries remember your wings: Valkyrie Flight restored to {(int)recorded}");
            }

            Stamp(player, recorded);
        }

        /// <summary>
        /// Whether this character's repair is genuinely spent. Only the versioned mark counts:
        /// 2.1.4 wrote a bare "done" off an answer its own bug had emptied, so every one of those
        /// marks is treated as never having happened and the repair is still owed.
        /// </summary>
        private static bool IsStamped(Player player)
        {
            Dictionary<string, string> data = player.m_customData;
            return data != null
                && data.TryGetValue(CustomDataKey, out string mark)
                && mark != null
                && mark.StartsWith(StampPrefix, StringComparison.Ordinal);
        }

        /// <summary>Marks the repair as spent for this character, whatever the outcome. Written
        /// even when nothing was restored, so a pilot who never lost anything is not asked on
        /// every login for the rest of the character's life.</summary>
        private static void Stamp(Player player, float recorded)
        {
            try
            {
                Dictionary<string, string> data = player.m_customData;
                if (data == null)
                {
                    data = new Dictionary<string, string>();
                    player.m_customData = data;
                }

                data[CustomDataKey] = StampPrefix + recorded.ToString("R", CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Could not mark the Valkyrie Flight restore as done: {ex.Message}");
            }
        }
    }
}
