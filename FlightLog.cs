using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace WingsoftheValkyrie
{
    /// <summary>
    /// Tracks the local character's time on the wing and keeps it on the character.
    ///
    /// Persistence rides on <c>Player.m_customData</c>, the vanilla per-character string
    /// dictionary that Valheim serialises inside the character save. That means the log
    /// travels with the character across worlds and servers, survives a reinstall of the mod,
    /// and needs no save file of our own.
    ///
    /// Only the local player is tracked, because everything it measures -- glide time,
    /// altitude, speed -- is client-side physics that only the owning client actually runs.
    /// Getting those numbers to a server (and from there to BarrkBOT) is
    /// <see cref="FlightReport"/>'s job, not this one's.
    /// </summary>
    public static class FlightLog
    {
        // Never rename: the key IS the save location. A rename orphans every existing saga.
        private const string CustomDataKey = "wubarrk.wotv.flightlog";

        // A tenth of a metre per second is below what anyone can feel, and it keeps a player
        // idling in a stiff breeze from accruing "distance flown".
        private const float MinTrackedSpeed = 0.1f;

        // Above any speed the mod can produce. Teleports, portals and physics hiccups arrive as
        // enormous one-frame steps and would otherwise be logged as record-breaking sprints.
        private const float ImplausibleSpeed = 200f;

        private const float AltitudeSampleInterval = 0.25f;
        private const float InFlightFlushInterval = 10f;

        private static FlightSaga _saga = new FlightSaga();

        // Which character the saga belongs to, so a logout-and-swap cannot bleed one character's
        // numbers into another's save. The Player reference alongside it is purely a fast path:
        // EnsureLoaded runs every frame, and a reference compare beats asking the game for a
        // name sixty times a second.
        private static string _loadedFor;
        private static Player _boundPlayer;

        // ---- current-flight state (never persisted) --------------------------------------------

        private static bool _airborne;
        private static float _currentFlightSeconds;
        private static float _currentFlightMeters;
        private static Vector3 _lastPosition;
        private static bool _hasLastPosition;
        private static float _altitudeSampleTimer;
        private static float _flushTimer;

        public static bool HasFlown => _saga.HasFlown;
        public static FlightSaga Saga => _saga;
        public static string OwnerName => _loadedFor;

        // ---- load / save -----------------------------------------------------------------------

        /// <summary>Binds the saga to this character, re-reading it off <c>m_customData</c>.
        /// Call from the <c>Player.Load</c> postfix, which is the exact moment the character's
        /// custom data is known to be populated.</summary>
        public static void LoadFrom(Player player)
        {
            Bind(player, force: true);
        }

        /// <summary>Binds the saga only if nothing is bound for this character yet. Safe to
        /// call every frame.</summary>
        public static void EnsureLoaded(Player player)
        {
            Bind(player, force: false);
        }

        // Load ordering is not something a Harmony patch gets to assume: Player.Update can run
        // before the profile has been read back into m_customData. So the authoritative read is
        // forced from the Player.Load postfix and always wins, while every other caller merely
        // ensures *something* is bound. Whichever order the game picks, the forced read is the
        // one that survives -- an unforced bind can never overwrite it with an empty saga.
        private static void Bind(Player player, bool force)
        {
            if (player == null) return;
            if (!force && ReferenceEquals(_boundPlayer, player)) return;

            string owner = player.GetPlayerName();
            if (!force && _loadedFor == owner) { _boundPlayer = player; return; }

            _saga = new FlightSaga();
            _loadedFor = owner;
            _boundPlayer = player;
            ResetFlightState();
            _airborne = false;

            try
            {
                Dictionary<string, string> data = player.m_customData;
                if (data != null && data.TryGetValue(CustomDataKey, out string stored) && !string.IsNullOrEmpty(stored))
                {
                    _saga = FlightSaga.Deserialize(stored);
                }
            }
            catch (Exception ex)
            {
                // A corrupt saga is worth losing; a corrupt saga that stops you flying is not.
                _saga = new FlightSaga();
                Jotunn.Logger.LogWarning($"[Wings of the Valkyrie] Could not read the flight logbook for {owner}, starting a fresh one. Reason: {ex.Message}");
            }
        }

        /// <summary>Writes the saga back onto the character and offers it to the server.
        /// <paramref name="force"/> bypasses the report throttle -- pass it when the game itself
        /// is saving, so what the server publishes matches what went to disk.</summary>
        public static void Flush(Player player, bool force)
        {
            if (player == null || !ModConfig.EnableFlightLog.Value) return;

            // Never write a saga onto a character it was not read from. Without this, saving
            // during the window before the profile is bound would stamp an empty logbook over
            // a real one.
            if (_loadedFor == null || _loadedFor != player.GetPlayerName()) return;

            _saga.SkillLevel = FlyingSkill.Level(player);

            try
            {
                Dictionary<string, string> data = player.m_customData;
                if (data == null)
                {
                    data = new Dictionary<string, string>();
                    player.m_customData = data;
                }

                data[CustomDataKey] = _saga.Serialize();
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning($"[Wings of the Valkyrie] Could not store the flight logbook on your character. Reason: {ex.Message}");
            }

            FlightReport.ReportToServer(player, _saga, force);
        }

        // ---- recording -------------------------------------------------------------------------

        public static void NoteFlap()
        {
            if (!ModConfig.EnableFlightLog.Value) return;
            _saga.Flaps++;
        }

        public static void NoteStaminaDenied()
        {
            if (!ModConfig.EnableFlightLog.Value) return;
            _saga.StaminaDenials++;
        }

        public static void NoteSkillDenied()
        {
            if (!ModConfig.EnableFlightLog.Value) return;
            _saga.SkillDenials++;
        }

        /// <summary>Called every frame for the local player. Handles takeoff and landing edges,
        /// accumulates time, distance and records, and never throws into the game loop.</summary>
        public static void Tick(Player player, bool gliding, string wingsName, float deltaTime)
        {
            if (player == null || !ModConfig.EnableFlightLog.Value) return;

            EnsureLoaded(player);

            if (gliding && !_airborne) BeginFlight(player);
            else if (!gliding && _airborne) EndFlight(player);

            if (!gliding)
            {
                _hasLastPosition = false;
                return;
            }

            Accumulate(player, wingsName, deltaTime);
        }

        private static void BeginFlight(Player player)
        {
            _airborne = true;
            _currentFlightSeconds = 0f;
            _currentFlightMeters = 0f;
            _altitudeSampleTimer = 0f;
            _flushTimer = 0f;
            _lastPosition = player.transform.position;
            _hasLastPosition = true;

            _saga.Flights++;

            long now = FlightSaga.UnixNow();
            if (_saga.FirstFlightUnix == 0) _saga.FirstFlightUnix = now;
            _saga.LastFlightUnix = now;
        }

        private static void EndFlight(Player player)
        {
            _airborne = false;

            if (_currentFlightSeconds > _saga.BestFlightSeconds) _saga.BestFlightSeconds = _currentFlightSeconds;
            if (_currentFlightMeters > _saga.BestFlightMeters) _saga.BestFlightMeters = _currentFlightMeters;
            _saga.LastFlightUnix = FlightSaga.UnixNow();

            if (player.IsSwimming() || player.InWater()) _saga.LandingsWater++;
            else if (player.GetStandingOnShip() != null) _saga.LandingsShip++;
            else _saga.LandingsGround++;

            ResetFlightState();
            Flush(player, force: false);
        }

        private static void Accumulate(Player player, string wingsName, float deltaTime)
        {
            if (deltaTime <= 0f) return;

            _currentFlightSeconds += deltaTime;
            _saga.TimeFlownSeconds += deltaTime;
            _saga.AddTierTime(wingsName, deltaTime);

            // Landing is the natural place to write the saga down, but dying in mid-air never
            // reaches one. Checkpoint during the flight so a fatal dive costs seconds, not the
            // whole crossing.
            _flushTimer += deltaTime;
            if (_flushTimer >= InFlightFlushInterval)
            {
                _flushTimer = 0f;
                Flush(player, force: false);
            }

            if (EnvMan.IsNight()) _saga.NightSeconds += deltaTime;

            Vector3 position = player.transform.position;

            if (_hasLastPosition)
            {
                Vector3 step = position - _lastPosition;
                step.y = 0f;

                float distance = step.magnitude;
                float speed = distance / deltaTime;

                if (speed >= MinTrackedSpeed && speed < ImplausibleSpeed)
                {
                    _saga.DistanceMeters += distance;
                    _currentFlightMeters += distance;
                    if (speed > _saga.BestSpeed) _saga.BestSpeed = speed;
                }
            }

            _lastPosition = position;
            _hasLastPosition = true;

            float diveSpeed = -player.GetVelocity().y;
            if (diveSpeed > _saga.BestDiveSpeed && diveSpeed < ImplausibleSpeed) _saga.BestDiveSpeed = diveSpeed;

            _altitudeSampleTimer += deltaTime;
            if (_altitudeSampleTimer >= AltitudeSampleInterval)
            {
                _altitudeSampleTimer = 0f;
                SampleAltitudeAndBiome(player, position);
            }
        }

        private static void SampleAltitudeAndBiome(Player player, Vector3 position)
        {
            if (ZoneSystem.instance != null)
            {
                float altitude = position.y - ZoneSystem.instance.GetGroundHeight(position);
                if (altitude > _saga.BestAltitude) _saga.BestAltitude = altitude;
            }

            Heightmap.Biome biome = player.GetCurrentBiome();
            if (biome != Heightmap.Biome.None) _saga.BiomeMask |= (int)biome;
        }

        private static void ResetFlightState()
        {
            _currentFlightSeconds = 0f;
            _currentFlightMeters = 0f;
            _hasLastPosition = false;
            _altitudeSampleTimer = 0f;
            _flushTimer = 0f;
        }

        // ---- readout -----------------------------------------------------------------------------

        private static string Num(double value, int decimals)
        {
            return value.ToString("N" + decimals, CultureInfo.InvariantCulture);
        }

        /// <summary>The in-game readout, one line per entry.</summary>
        public static List<string> Report(Player player, bool includeOddities)
        {
            var lines = new List<string>();

            if (!ModConfig.EnableFlightLog.Value)
            {
                lines.Add("The flight logbook is switched off (see EnableFlightLog in the config).");
                return lines;
            }

            if (!HasFlown)
            {
                lines.Add("Your logbook is empty. The sky is still waiting.");
                return lines;
            }

            lines.Add("=== The Flight Saga of " + (_loadedFor ?? "a nameless Viking") + " ===");
            lines.Add("Valkyrie Flight level : " + Num(FlyingSkill.Level(player), 1));
            lines.Add("Time flown            : " + FlightSaga.Duration(_saga.TimeFlownSeconds));
            lines.Add("Distance flown        : " + Num(_saga.DistanceMeters, 0) + " m");
            lines.Add("Flights               : " + _saga.Flights.ToString(CultureInfo.InvariantCulture));
            lines.Add("Wingbeats             : " + _saga.Flaps.ToString(CultureInfo.InvariantCulture));
            lines.Add("-- Records --");
            lines.Add("Highest above ground  : " + Num(_saga.BestAltitude, 1) + " m");
            lines.Add("Longest single flight : " + FlightSaga.Duration(_saga.BestFlightSeconds) + " / " + Num(_saga.BestFlightMeters, 0) + " m");
            lines.Add("Fastest on the wing   : " + Num(_saga.BestSpeed, 1) + " m/s");
            lines.Add("Steepest stoop        : " + Num(_saga.BestDiveSpeed, 1) + " m/s straight down");

            if (!includeOddities) return lines;

            lines.Add("-- Oddities --");
            lines.Add("Flown by night        : " + FlightSaga.Duration(_saga.NightSeconds));
            lines.Add("Out of stamina midair : " + _saga.StaminaDenials.ToString(CultureInfo.InvariantCulture) + " times");
            lines.Add("Wings refused you     : " + _saga.SkillDenials.ToString(CultureInfo.InvariantCulture) + " times (not skilled enough to flap)");
            lines.Add("Landed on solid ground: " + _saga.LandingsGround.ToString(CultureInfo.InvariantCulture));
            lines.Add("Landed in the drink   : " + _saga.LandingsWater.ToString(CultureInfo.InvariantCulture));
            lines.Add("Landed on a ship      : " + _saga.LandingsShip.ToString(CultureInfo.InvariantCulture));

            List<string> biomes = _saga.BiomesFlownOver();
            lines.Add("Biomes crossed        : " + biomes.Count + "/" + FlightSaga.BiomeCount
                      + (biomes.Count > 0 ? " (" + string.Join(", ", biomes.ToArray()) + ")" : ""));

            lines.Add("-- Time per tier --");
            lines.Add("Crude  : " + FlightSaga.Duration(_saga.CrudeSeconds));
            lines.Add("Troll  : " + FlightSaga.Duration(_saga.TrollSeconds));
            lines.Add("Lox    : " + FlightSaga.Duration(_saga.LoxSeconds));
            lines.Add("Dragon : " + FlightSaga.Duration(_saga.DragonSeconds));

            return lines;
        }
    }
}
