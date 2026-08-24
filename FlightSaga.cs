using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WingsoftheValkyrie
{
    /// <summary>
    /// One character's flight record, and the wire/save format for it.
    ///
    /// This type is deliberately plain data with no Unity or Valheim surface, because it is
    /// used in three places that share nothing else: the client tracks into it, the character
    /// save stores it, and the server keeps one per player to build the BarrkBOT export from.
    /// </summary>
    public sealed class FlightSaga
    {
        /// <summary>Bumped when the stored field set changes. Readers ignore fields they do not
        /// know and writers never remove one, so a saga survives moving between mod versions in
        /// either direction. This is the version of the *save blob* only.</summary>
        public const int SchemaVersion = 1;

        /// <summary>The version of the published BarrkBOT export, which moves on its own schedule
        /// and for its own reasons: it tracks the shape agreed with the reader, not the shape of
        /// anything stored here. It shared a constant with SchemaVersion until the two needed to
        /// disagree, which they now do -- the save blob is still v1, the export is v4.</summary>
        public const int ExportSchemaVersion = 4;

        public long Flights;
        public double TimeFlownSeconds;
        public double DistanceMeters;
        public long Flaps;

        public long LandingsGround;
        public long LandingsWater;
        public long LandingsShip;

        public float BestAltitude;
        public float BestFlightSeconds;
        public float BestFlightMeters;
        public float BestSpeed;
        public float BestDiveSpeed;

        public long StaminaDenials;
        public long SkillDenials;
        public double NightSeconds;
        public int BiomeMask;

        public double CrudeSeconds;
        public double TrollSeconds;
        public double LoxSeconds;
        public double DragonSeconds;

        public long FirstFlightUnix;
        public long LastFlightUnix;

        /// <summary>Skill level at the time of the last report. Not tracked by the saga itself --
        /// it lives in the player's skills -- but carried along so the server can publish it.</summary>
        public float SkillLevel;

        public bool HasFlown => Flights > 0;

        public void AddTierTime(string wingsName, double seconds)
        {
            switch (wingsName)
            {
                case WingsItem.TrollName: TrollSeconds += seconds; break;
                case WingsItem.LoxName: LoxSeconds += seconds; break;
                case WingsItem.DragonName: DragonSeconds += seconds; break;
                default: CrudeSeconds += seconds; break;
            }
        }

        // ---- storage / wire format ------------------------------------------------------------
        //
        // "key=value;key=value", invariant culture. Chosen over JSON so the saga costs one
        // m_customData entry and pulls in no serialiser: neither the character save nor an RPC
        // payload is the place to be clever. Unknown keys are skipped on read, so a saga written
        // by a newer build still loads in an older one.

        public string Serialize()
        {
            var sb = new StringBuilder(360);
            Put(sb, "v", SchemaVersion);
            Put(sb, "flights", Flights);
            Put(sb, "time", TimeFlownSeconds);
            Put(sb, "dist", DistanceMeters);
            Put(sb, "flaps", Flaps);
            Put(sb, "landGround", LandingsGround);
            Put(sb, "landWater", LandingsWater);
            Put(sb, "landShip", LandingsShip);
            Put(sb, "bestAlt", BestAltitude);
            Put(sb, "bestTime", BestFlightSeconds);
            Put(sb, "bestDist", BestFlightMeters);
            Put(sb, "bestSpeed", BestSpeed);
            Put(sb, "bestDive", BestDiveSpeed);
            Put(sb, "noStam", StaminaDenials);
            Put(sb, "noSkill", SkillDenials);
            Put(sb, "night", NightSeconds);
            Put(sb, "biomes", BiomeMask);
            Put(sb, "tCrude", CrudeSeconds);
            Put(sb, "tTroll", TrollSeconds);
            Put(sb, "tLox", LoxSeconds);
            Put(sb, "tDragon", DragonSeconds);
            Put(sb, "first", FirstFlightUnix);
            Put(sb, "last", LastFlightUnix);
            Put(sb, "skill", SkillLevel);
            return sb.ToString();
        }

        public static FlightSaga Deserialize(string stored)
        {
            var saga = new FlightSaga();
            if (string.IsNullOrEmpty(stored)) return saga;

            foreach (string pair in stored.Split(';'))
            {
                int eq = pair.IndexOf('=');
                if (eq <= 0) continue;

                string key = pair.Substring(0, eq);
                string raw = pair.Substring(eq + 1);

                switch (key)
                {
                    case "flights": saga.Flights = Long(raw); break;
                    case "time": saga.TimeFlownSeconds = Double(raw); break;
                    case "dist": saga.DistanceMeters = Double(raw); break;
                    case "flaps": saga.Flaps = Long(raw); break;
                    case "landGround": saga.LandingsGround = Long(raw); break;
                    case "landWater": saga.LandingsWater = Long(raw); break;
                    case "landShip": saga.LandingsShip = Long(raw); break;
                    case "bestAlt": saga.BestAltitude = (float)Double(raw); break;
                    case "bestTime": saga.BestFlightSeconds = (float)Double(raw); break;
                    case "bestDist": saga.BestFlightMeters = (float)Double(raw); break;
                    case "bestSpeed": saga.BestSpeed = (float)Double(raw); break;
                    case "bestDive": saga.BestDiveSpeed = (float)Double(raw); break;
                    case "noStam": saga.StaminaDenials = Long(raw); break;
                    case "noSkill": saga.SkillDenials = Long(raw); break;
                    case "night": saga.NightSeconds = Double(raw); break;
                    case "biomes": saga.BiomeMask = (int)Long(raw); break;
                    case "tCrude": saga.CrudeSeconds = Double(raw); break;
                    case "tTroll": saga.TrollSeconds = Double(raw); break;
                    case "tLox": saga.LoxSeconds = Double(raw); break;
                    case "tDragon": saga.DragonSeconds = Double(raw); break;
                    case "first": saga.FirstFlightUnix = Long(raw); break;
                    case "last": saga.LastFlightUnix = Long(raw); break;
                    case "skill": saga.SkillLevel = (float)Double(raw); break;
                }
            }
            return saga;
        }

        private static void Put(StringBuilder sb, string key, double value)
        {
            if (sb.Length > 0) sb.Append(';');
            sb.Append(key).Append('=').Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void Put(StringBuilder sb, string key, long value)
        {
            if (sb.Length > 0) sb.Append(';');
            sb.Append(key).Append('=').Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static long Long(string raw)
        {
            return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) ? value : 0L;
        }

        private static double Double(string raw)
        {
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : 0d;
        }

        // ---- shared formatting ------------------------------------------------------------------

        private static readonly KeyValuePair<Heightmap.Biome, string>[] BiomeNames =
        {
            new KeyValuePair<Heightmap.Biome, string>(Heightmap.Biome.Meadows, "Meadows"),
            new KeyValuePair<Heightmap.Biome, string>(Heightmap.Biome.BlackForest, "Black Forest"),
            new KeyValuePair<Heightmap.Biome, string>(Heightmap.Biome.Swamp, "Swamp"),
            new KeyValuePair<Heightmap.Biome, string>(Heightmap.Biome.Mountain, "Mountain"),
            new KeyValuePair<Heightmap.Biome, string>(Heightmap.Biome.Plains, "Plains"),
            new KeyValuePair<Heightmap.Biome, string>(Heightmap.Biome.Mistlands, "Mistlands"),
            new KeyValuePair<Heightmap.Biome, string>(Heightmap.Biome.AshLands, "Ashlands"),
            new KeyValuePair<Heightmap.Biome, string>(Heightmap.Biome.DeepNorth, "Deep North"),
            new KeyValuePair<Heightmap.Biome, string>(Heightmap.Biome.Ocean, "Ocean"),
        };

        /// <summary>How many biomes exist to be crossed, so a readout can say "3 of 9".</summary>
        public static int BiomeCount => BiomeNames.Length;

        public List<string> BiomesFlownOver()
        {
            var found = new List<string>();
            foreach (KeyValuePair<Heightmap.Biome, string> entry in BiomeNames)
            {
                if ((BiomeMask & (int)entry.Key) != 0) found.Add(entry.Value);
            }
            return found;
        }

        public static string Duration(double seconds)
        {
            if (seconds < 1d) return "no time at all";

            var span = TimeSpan.FromSeconds(seconds);
            if (span.TotalHours >= 1d)
                return string.Format(CultureInfo.InvariantCulture, "{0}h {1}m {2}s", (int)span.TotalHours, span.Minutes, span.Seconds);
            if (span.TotalMinutes >= 1d)
                return string.Format(CultureInfo.InvariantCulture, "{0}m {1}s", (int)span.TotalMinutes, span.Seconds);
            return string.Format(CultureInfo.InvariantCulture, "{0}s", span.Seconds);
        }

        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static long UnixNow()
        {
            return (long)(DateTime.UtcNow - Epoch).TotalSeconds;
        }

        /// <summary>ISO-8601 UTC with a Z, which is what the export consumers key staleness off.</summary>
        public static string Iso(long unixSeconds)
        {
            return Epoch.AddSeconds(unixSeconds).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        }

        public static string IsoNow()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        }
    }
}
