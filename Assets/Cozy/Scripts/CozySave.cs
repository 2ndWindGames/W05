using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Cozy
{
    [Serializable] public sealed class CozySettings
    {
        public bool english, muted, largeText, reducedDecoration;
        // Kept for older save files; content access no longer depends on these settings.
        public bool testUnlockAll, testUnlockConfigured;
        public int testUnlockRevision;
        public float music = .7f, effects = .7f;
        public string up = "w", down = "s", left = "a", right = "d";
    }
    [Serializable] public sealed class CozyRecord { public string key; public int score; public float survival, bossTime; }
    [Serializable] public sealed class CozyProfile
    {
        public int profileVersion = 1, stars, failures, legacyBest;
        public bool[] cleared = new bool[3], cosmetics = new bool[12], achievements = new bool[18];
        public List<string> settledRuns = new();
        public List<CozyRecord> records = new();
        public CozySettings settings = new();
        public CozyRun activeRun;
        // This edition includes all content in every build, including non-development Android AABs.
        // Earned clears and purchases remain separate so rewards and existing saves stay intact.
        public bool RegionUnlocked(int region) => region >= 0 && region < 3;
        public bool AnimalUnlocked(int animal) => animal >= 0 && animal < 3;
        public bool EndlessUnlocked(int region) => RegionUnlocked(region);
        public bool LettersUnlocked(int region) => EndlessUnlocked(region);
        public bool DecorationAvailable(int id) => id >= 0 && id < cosmetics.Length;
        public bool BuyDecoration(int id)
        {
            if (id < 0 || id >= 12 || DecorationAvailable(id)) return false;
            int cost = (id / 3 + 1) * 100; if (stars < cost) return false;
            stars -= cost; cosmetics[id] = true;
            int owned = 0; foreach (bool item in cosmetics) if (item) owned++;
            if (owned >= 1) achievements[15] = true; if (owned >= 6) achievements[16] = true; if (owned >= 12) achievements[17] = true;
            return true;
        }
        public int Settle(CozyRun run)
        {
            if (!run.Finished || run.rewardClaimed || settledRuns.Contains(run.runId)) return 0;
            int earned = run.CalculateStars();
            if (run.phase == RunPhase.Won)
            {
                if (!cleared[(int)run.region]) earned += 50;
                cleared[(int)run.region] = true; failures = 0;
                achievements[(int)run.region] = true; achievements[3 + (int)run.animal] = true;
            }
            else if (run.phase == RunPhase.Lost) failures++;
            if (run.objectives > 0) achievements[6 + (int)run.region] = true;
            if (run.branch > 0) achievements[9 + (int)run.animal * 2 + run.branch - 1] = true;
            var record = records.Find(x => x.key == run.RecordKey);
            if (record == null) { record = new CozyRecord { key = run.RecordKey }; records.Add(record); }
            record.score = Math.Max(record.score, run.score); record.survival = Mathf.Max(record.survival, Mathf.Min(run.elapsed, 720));
            record.bossTime = Mathf.Max(record.bossTime, run.bossStarted < 0 ? 0 : run.elapsed - run.bossStarted);
            stars += earned; settledRuns.Add(run.runId); run.rewardClaimed = true; run.villageReward = earned;
            activeRun = null; return earned;
        }
    }
    [Serializable] sealed class CozySaveEnvelope { public string payload, sha256; }

    public sealed class CozySave
    {
        public readonly string Path;
        public string Notice { get; private set; }
        public bool ReadOnly { get; private set; }
        public CozySave(string path) { Path = path; }
        static string Digest(string data)
        {
            using var hash = SHA256.Create();
            return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(data)));
        }
        static void Validate(CozyProfile p)
        {
            if (p == null || p.profileVersion != 1 || p.cleared == null || p.cleared.Length != 3 || p.cosmetics == null || p.cosmetics.Length != 12 || p.achievements == null || p.achievements.Length != 18 || p.settings == null || p.records == null || p.settledRuns == null || p.stars < 0)
                throw new InvalidDataException("Unsupported or incomplete profile.");
            var r = p.activeRun;
            if (r == null) return;
            if (r.rulesVersion != CozyRules.Version) throw new NotSupportedException("Saved run uses different rules.");
            if (string.IsNullOrEmpty(r.runId) || r.rng == 0 || r.passives == null || r.passives.Length != 12 || r.relics == null || r.relics.Length != 6 || r.enemies == null || r.attacks == null || r.pickups == null || r.choices == null || r.blockers == null || r.blockers.Length != 2 || r.objectivePoints == null || r.collected == null || r.collected.Length != 5 || r.level < 1 || r.level > 15 || r.hp < 0 || r.hp > r.MaxHP || r.weaponLevel < 1 || r.weaponLevel > 5 || !Enum.IsDefined(typeof(Animal), r.animal) || !Enum.IsDefined(typeof(Region), r.region) || !Enum.IsDefined(typeof(Difficulty), r.difficulty) || !Enum.IsDefined(typeof(RunPhase), r.phase))
                throw new InvalidDataException("Incomplete run snapshot.");
        }
        CozyProfile Read(string path)
        {
            var envelope = JsonUtility.FromJson<CozySaveEnvelope>(File.ReadAllText(path));
            if (envelope == null || string.IsNullOrEmpty(envelope.payload) || envelope.sha256 != Digest(envelope.payload)) throw new InvalidDataException("Save checksum mismatch.");
            var profile = JsonUtility.FromJson<CozyProfile>(envelope.payload);
            // Unity's inline class serialization expands a null run into a default-valued object.
            // Only the exact empty sentinel is a missing run; malformed real runs still fail validation.
            var run = profile?.activeRun;
            if (run != null && string.IsNullOrEmpty(run.runId) && run.rng == 0 &&
                JsonUtility.ToJson(run) == JsonUtility.ToJson(new CozyRun { rulesVersion = run.rulesVersion }))
                profile.activeRun = null;
            Validate(profile); return profile;
        }
        public CozyProfile Load()
        {
            Notice = null; ReadOnly = false;
            if (!File.Exists(Path) && !File.Exists(Path + ".bak")) return new CozyProfile();
            try { return Read(Path); }
            catch (NotSupportedException) { ReadOnly = true; Notice = "version"; return new CozyProfile(); }
            catch (Exception primary) when (primary is IOException || primary is InvalidDataException || primary is ArgumentException || primary is NullReferenceException || primary is UnauthorizedAccessException)
            {
                try { var recovered = Read(Path + ".bak"); Notice = "backup"; return recovered; }
                catch (Exception backup) when (backup is IOException || backup is InvalidDataException || backup is ArgumentException || backup is NullReferenceException || backup is NotSupportedException || backup is UnauthorizedAccessException)
                { ReadOnly = true; Notice = "damaged"; return new CozyProfile(); }
            }
        }
        public void Write(CozyProfile profile)
        {
            if (ReadOnly) throw new IOException("Existing save needs recovery; it will not be overwritten.");
            Validate(profile);
            string folder = System.IO.Path.GetDirectoryName(Path); if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            string payload = JsonUtility.ToJson(profile);
            string encoded = JsonUtility.ToJson(new CozySaveEnvelope { payload = payload, sha256 = Digest(payload) });
            string temp = Path + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(encoded); stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
            }
            if (File.Exists(Path))
            {
                if (Notice == "backup")
                {
                    // Keep the known-good backup instead of replacing it with a corrupt primary.
                    File.Replace(temp, Path, null); Notice = null;
                }
                else File.Replace(temp, Path, Path + ".bak");
            }
            else File.Move(temp, Path);
        }
    }
}
