using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cozy
{
    [Serializable] public sealed class RunEnemy
    {
        public int id;
        public EnemyRole role;
        public Vector2 p, aim;
        public float hp, maxHP, speed, warmup = .5f, hitCooldown, slow, timer = 2, actionTime;
        public int action, cycle;
        public float Radius => role == EnemyRole.Boss ? 45 : role == EnemyRole.Elite ? 30 : role == EnemyRole.Tough ? 23 : role == EnemyRole.Child ? 12 : 18;
    }
    [Serializable] public sealed class RunAttack
    {
        public Vector2 p, v;
        public float life, age, radius, damage;
        public int kind, pierce = 1; // 0 letter, 1 wave, 2 enemy bullet, 3 eruption
        public string source;
        public List<int> hit = new();
    }
    [Serializable] public sealed class RunPickup
    {
        public Vector2 p;
        public int kind; // 0 XP, 1 healing
        public float value, life = 3600;
    }

    // Serializable deterministic simulation. Rendering, device input and disk I/O live elsewhere.
    [Serializable] public sealed class CozyRun
    {
        public string rulesVersion = CozyRules.Version, runId;
        public uint rng;
        public int seed, nextId = 1;
        public Animal animal;
        public Region region;
        public Difficulty difficulty;
        public bool endless;
        public RunPhase phase = RunPhase.Playing, resumePhase = RunPhase.Playing;
        public Vector2 position, velocity;
        public float elapsed, invincible, orbitAngle, attackTimer, spawnTimer = 1.5f, xp, bossStarted = -1;
        public int hp = 10, level = 1, weaponLevel = 1, branch, pendingLevels, rerolls = 2, score, kills, objectives, windKills;
        public int[] passives = new int[12];
        public bool[] relics = new bool[6];
        public bool shield, bossSpawned, rewardClaimed;
        public int villageReward;
        public string lastDamage = "", resultReason = "";
        public List<RunEnemy> enemies = new();
        public List<RunAttack> attacks = new();
        public List<RunPickup> pickups = new();
        public List<int> choices = new(); // -1 weapon; -2 heal; 0..11 passives (or relic index)
        public bool choosingBranch, relicPending;
        public int objectiveIndex, nextObjective, nextHeal, nextElite = 240, objectiveCollected;
        public float objectiveRemaining, objectiveHeld;
        public bool carrying;
        public bool[] collected = new bool[5];
        public Vector2[] objectivePoints = new Vector2[0];
        public Vector2[] blockers;
        public float eliteRelief;
        static readonly int[] NormalHealTimes = { 180, 420, 660 };
        static readonly int[] CozyHealTimes = { 180, 420, 540, 660 };

        public CozyRun() { }
        public CozyRun(Animal a, Region r, Difficulty d, bool infinite, int randomSeed)
        {
            animal = a; region = r; difficulty = d; endless = infinite; seed = randomSeed;
            rng = (uint)randomSeed; if (rng == 0) rng = 1;
            runId = Guid.NewGuid().ToString("N"); blockers = CozyRules.Blockers(r);
        }
        public bool Finished => phase == RunPhase.Won || phase == RunPhase.Lost || phase == RunPhase.Retired;
        public int MaxHP => 10 + passives[4] * 2 + (relics[5] ? 2 : 0);
        public float MoveSpeed => 220 * (1 + .06f * passives[1] + (relics[5] ? .05f : 0));
        public WeaponStats Weapon => CozyRules.Weapon(animal, weaponLevel, branch);
        public float AbsorbRadius => 48 + 25 * passives[6];
        public float RangeScale => 1 + .08f * passives[3];
        public float AttackSpeed => 1 + .08f * passives[2];
        public float RandomValue() { rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5; return (rng & 0x00ffffff) / 16777216f; }
        public int RandomInt(int max) => Mathf.Min(max - 1, (int)(RandomValue() * max));
        public Vector2 FlakePosition(int i) => position + new Vector2(Mathf.Cos(orbitAngle + i * Mathf.PI * 2 / Weapon.count), Mathf.Sin(orbitAngle + i * Mathf.PI * 2 / Weapon.count)) * Weapon.range;
        public void Pause() { if (Finished || phase == RunPhase.Paused) return; resumePhase = phase; phase = RunPhase.Paused; velocity = Vector2.zero; }
        public void Resume() { if (phase == RunPhase.Paused) phase = resumePhase; }
        public void Retire() { if (!Finished) { phase = RunPhase.Retired; resultReason = "retired"; velocity = Vector2.zero; } }

        public Vector2 ResolvePosition(Vector2 p, float radius = 13)
        {
            p = CozyRules.Clamp(p);
            foreach (Vector2 b in blockers)
            {
                Vector2 away = p - b; float min = CozyRules.BlockerRadius(region) + radius;
                if (away.sqrMagnitude < min * min) p = b + (away.sqrMagnitude < .001f ? Vector2.right : away.normalized) * min;
            }
            return CozyRules.Clamp(p);
        }
        public void Tick(float dt, Vector2 input)
        {
            if (phase != RunPhase.Playing || dt <= 0) return;
            dt = Mathf.Min(dt, .05f);
            Vector2 oldPosition = position; float oldAngle = orbitAngle;
            elapsed += dt; invincible = Mathf.Max(0, invincible - dt); eliteRelief = Mathf.Max(0, eliteRelief - dt);
            velocity = Vector2.MoveTowards(velocity, Vector2.ClampMagnitude(input, 1) * MoveSpeed, MoveSpeed / .08f * dt);
            position = ResolvePosition(position + velocity * dt);
            orbitAngle = Mathf.Repeat(orbitAngle + dt * Mathf.PI * 2 / Weapon.interval * AttackSpeed, Mathf.PI * 2);
            Director(dt);
            // Damage is resolved before reward screens. Dead players cannot acquire XP or choices.
            MoveEnemies(dt);
            if (Finished) return;
            PlayerAttack(dt, oldPosition, oldAngle);
            UpdateAttacks(dt);
            if (Finished) return;
            UpdateObjective(dt);
            CollectPickups(dt);
            if (bossSpawned && elapsed - bossStarted >= CozyRules.BossDuration && !Finished)
            { phase = RunPhase.Lost; resultReason = "boss_timeout"; }
            ResolveChoices();
        }
        void Director(float dt)
        {
            if (!endless && elapsed >= CozyRules.Duration && !bossSpawned)
            {
                bossSpawned = true; bossStarted = elapsed; objectiveRemaining = 0; carrying = false;
                enemies.Clear(); attacks.RemoveAll(a => a.kind >= 2);
                Spawn(EnemyRole.Boss, new Vector2(0, 215));
                return;
            }
            if (bossSpawned) return;
            if (nextObjective < 3 && elapsed >= 120 + nextObjective * 240) StartObjective();
            int[] healTimes = difficulty == Difficulty.Cozy ? CozyHealTimes : NormalHealTimes;
            if (nextHeal < healTimes.Length && elapsed >= healTimes[nextHeal])
            {
                nextHeal++;
                for (int i = 0; i < 8; i++)
                {
                    Vector2 p = ResolvePosition(new Vector2((RandomValue() * 2 - 1) * 520, (RandomValue() * 2 - 1) * 265));
                    if (Vector2.Distance(p, position) < 180) continue;
                    pickups.Add(new RunPickup { p = p, kind = 1, value = 2, life = 60 }); break;
                }
            }
            if (elapsed >= nextElite && (endless || nextElite < 720))
            {
                enemies.RemoveAll(e => e.role == EnemyRole.Elite); Spawn(EnemyRole.Elite);
                nextElite += 240; eliteRelief = 20;
            }
            spawnTimer -= dt;
            if (spawnTimer <= 0)
            {
                spawnTimer = 1 / (CozyRules.SpawnRate(elapsed) * (eliteRelief > 0 ? .7f : 1));
                float x = RandomValue(); EnemyRole role = EnemyRole.Basic;
                if (elapsed >= 60)
                {
                    float tough = elapsed >= 300 && region == Region.Springs ? .25f : .15f;
                    float ranged = region == Region.PostOffice && elapsed >= 300 ? .15f : .10f;
                    if (x < tough) role = EnemyRole.Tough;
                    else if (x < tough + .1f) role = EnemyRole.Charger;
                    else if (elapsed >= 180 && x < tough + .1f + ranged) role = EnemyRole.Ranged;
                    else if (elapsed >= 300 && x < tough + .2f + ranged) role = EnemyRole.Splitter;
                    else if (elapsed >= 300 && x < tough + .25f + ranged) role = EnemyRole.Support;
                }
                Spawn(role);
            }
        }
        public RunEnemy Spawn(EnemyRole role, Vector2? requested = null)
        {
            bool special = role >= EnemyRole.Elite;
            int normalCount = 0, roleCount = 0;
            foreach (var enemy in enemies) if (enemy.hp > 0) { if (enemy.role < EnemyRole.Elite) normalCount++; if (enemy.role == role) roleCount++; }
            if (!special && normalCount >= CozyRules.EnemyCap) return null;
            if (special && enemies.Exists(e => e.role == role && e.hp > 0)) return null;
            int cap = role == EnemyRole.Charger ? 8 : role == EnemyRole.Ranged ? 6 : role == EnemyRole.Support ? 3 : int.MaxValue;
            if (roleCount >= cap) role = EnemyRole.Basic;
            Vector2 p = requested ?? Vector2.zero;
            if (!requested.HasValue)
            {
                bool found = false;
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    int edge = RandomInt(4); float margin = 24 + RandomValue() * 40;
                    p = edge < 2 ? new Vector2((edge == 0 ? -1 : 1) * (640 + margin), (RandomValue() * 2 - 1) * 310) : new Vector2((RandomValue() * 2 - 1) * 590, (edge == 2 ? -1 : 1) * (360 + margin));
                    if (Vector2.Distance(p, position) < 180) continue;
                    if (role == EnemyRole.Charger && region == Region.Springs && objectiveRemaining > 0 && Vector2.Distance(p, objectivePoints[0]) < 180) continue;
                    found = true; break;
                }
                if (!found) return null;
            }
            float baseHP = role == EnemyRole.Elite ? elapsed < 480 ? 350 : 650 : role == EnemyRole.Boss ? region == Region.Springs ? 2000 : 1800 : CozyRules.EnemyHP[(int)role];
            float hpScale = special || role == EnemyRole.Child ? 1 : CozyRules.HPScale(elapsed, endless);
            var e = new RunEnemy { id = nextId++, role = role, p = p, hp = Mathf.Ceil(baseHP * hpScale * CozyRules.HPScale(difficulty)), speed = CozyRules.EnemySpeed[(int)role] * CozyRules.SpeedScale(difficulty) * (special ? 1 : CozyRules.SpeedScale(elapsed, endless)) };
            e.maxHP = e.hp; enemies.Add(e); return e;
        }
        void MoveEnemies(float dt)
        {
            foreach (var e in enemies)
            {
                if (e.hp <= 0) continue;
                e.hitCooldown -= dt; e.slow -= dt; e.warmup -= dt;
                if (e.warmup > 0) continue;
                Vector2 before = e.p, toward = (position - e.p).normalized;
                if (e.role == EnemyRole.Boss) BossAI(e, dt);
                else if (e.role == EnemyRole.Charger || e.role == EnemyRole.Elite)
                {
                    e.timer -= dt;
                    if (e.action == 0 && e.timer <= 0) { e.action = 1; e.actionTime = e.role == EnemyRole.Elite ? .9f : .8f; e.aim = toward; }
                    if (e.action > 0)
                    {
                        e.actionTime -= dt;
                        if (e.action == 1 && e.actionTime <= 0) { e.action = 2; e.actionTime = .6f; }
                        else if (e.action == 2) { e.p += e.aim * 260 * CozyRules.SpeedScale(difficulty) * dt; if (e.actionTime <= 0) { e.action = 0; e.timer = e.role == EnemyRole.Elite ? 6 : 4; } }
                    }
                    else e.p += toward * e.speed * dt;
                }
                else if (e.role == EnemyRole.Ranged)
                {
                    e.timer -= dt;
                    if (e.action == 0 && e.timer <= 0) { e.action = 1; e.actionTime = 1; e.aim = toward; }
                    if (e.action == 1)
                    {
                        e.actionTime -= dt;
                        if (e.actionTime <= 0) { EnemyBullet(e.p, e.aim * 140, 4, "ranged"); e.action = 0; e.timer = 2.2f; }
                    }
                    else { float distance = Vector2.Distance(position, e.p); e.p += toward * (distance > 275 ? 1 : distance < 245 ? -1 : 0) * e.speed * dt; }
                }
                else
                {
                    bool supported = enemies.Exists(other => other != e && other.hp > 0 && other.warmup <= 0 && other.role == EnemyRole.Support && (other.p - e.p).sqrMagnitude < 25600);
                    e.p += toward * e.speed * (supported ? 1.15f : 1) * dt;
                }
                if (e.slow > 0 && e.role != EnemyRole.Boss) e.p = Vector2.Lerp(before, e.p, .8f);
                // Steer around the two circular blockers rather than getting stuck on them.
                foreach (var b in blockers)
                {
                    Vector2 away = e.p - b; float min = CozyRules.BlockerRadius(region) + e.Radius;
                    if (away.sqrMagnitude < min * min)
                    {
                        Vector2 normal = away.sqrMagnitude < .001f ? Vector2.right : away.normalized;
                        Vector2 tangent = new Vector2(-normal.y, normal.x);
                        if (Vector2.Dot(tangent, toward) < 0) tangent = -tangent;
                        e.p = b + normal * min + tangent * e.speed * dt;
                    }
                }
                if (CozyRules.SegmentDistance(before - position, e.p - position, Vector2.zero) <= 13 + e.Radius)
                {
                    bool canHit = invincible <= 0;
                    DamagePlayer(e.role.ToString(), e.p);
                    if (canHit && e.role != EnemyRole.Boss) e.p -= toward * 40;
                }
                if (Finished) return;
            }
        }
        void BossAI(RunEnemy e, float dt)
        {
            e.timer -= dt;
            if (region == Region.Snowfield)
            {
                if (e.action == 0 && e.timer <= 0) { e.action = 1; e.actionTime = 1; e.aim = (position - e.p).normalized; }
                if (e.action == 1) { e.actionTime -= dt; if (e.actionTime <= 0) { e.action = 2; e.actionTime = .7f; } }
                else if (e.action == 2)
                {
                    e.p = CozyRules.Clamp(e.p + e.aim * 260 * dt); e.actionTime -= dt;
                    if (e.actionTime <= 0)
                    {
                        e.cycle++; e.action = 0; e.timer = .2f;
                        if (e.cycle >= 3) { Fan(e.p, (position - e.p).normalized, 5, 130, 4, "snow_fan", 0); e.cycle = 0; e.timer = e.hp < e.maxHP * .5f ? 1.6f : 2; }
                    }
                }
            }
            else if (region == Region.Springs)
            {
                if (e.timer <= 0)
                {
                    if (CountAttacks(true) < CozyRules.ProjectileCap)
                        attacks.Add(new RunAttack { kind = 3, p = ResolvePosition(position), life = 1.6f, radius = 60, source = "spring_eruption" });
                    e.cycle++; e.timer = e.cycle < 3 ? .8f : e.hp < e.maxHP * .5f ? 1.6f : 2; if (e.cycle >= 3) e.cycle = 0;
                }
                e.p += (new Vector2(0, 140) - e.p).normalized * 20 * dt;
            }
            else
            {
                if (e.action == 0 && e.timer <= 0) { e.aim = (position - e.p).normalized; e.action = 1; e.timer = 1; e.cycle = 0; }
                else if (e.action == 1 && e.timer <= 0)
                {
                    Fan(e.p, e.aim, 7, 120, 5, "letter_fan", e.cycle * 30);
                    e.cycle++; e.timer = 1;
                    if (e.cycle >= 3) { e.action = 2; e.timer = e.hp < e.maxHP * .5f ? 1.6f : 2; }
                }
                else if (e.action == 2)
                {
                    e.p = CozyRules.Clamp(e.p + new Vector2(e.p.x > 0 ? -60 : 60, 0) * dt);
                    if (e.timer <= 0) e.action = 0;
                }
            }
        }
        void Fan(Vector2 p, Vector2 aim, int count, float speed, float life, string source, float rotate)
        {
            float angle = Mathf.Atan2(aim.y, aim.x);
            for (int i = 0; i < count; i++)
            {
                // All bullets are outside the central +/-25 degree escape wedge.
                float offset = (i % 2 == 0 ? -1 : 1) * (35 + (i / 2) * 26) + rotate;
                float a = angle + offset * Mathf.Deg2Rad; EnemyBullet(p, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed, life, source);
            }
        }
        int CountAttacks(bool hostile) { int count = 0; foreach (var a in attacks) if ((a.kind >= 2) == hostile) count++; return count; }
        void EnemyBullet(Vector2 p, Vector2 v, float life, string source)
        {
            if (CountAttacks(true) < CozyRules.ProjectileCap) attacks.Add(new RunAttack { kind = 2, p = p, v = v, life = life, radius = 9, source = source });
        }
        void PlayerAttack(float dt, Vector2 oldPosition, float oldAngle)
        {
            WeaponStats w = Weapon;
            if (animal == Animal.Penguin)
            {
                // Subdivide the rotating path so crossing a small enemy between ticks still hits.
                foreach (var e in enemies)
                {
                    if (e.hp <= 0 || e.warmup > 0 || e.hitCooldown > 0) continue;
                    for (int i = 0; i < w.count; i++)
                    {
                        float a = oldAngle + i * Mathf.PI * 2 / w.count;
                        Vector2 start = oldPosition + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * w.range;
                        if (CozyRules.SegmentDistance(start, FlakePosition(i), e.p) > 16 * RangeScale + e.Radius) continue;
                        Hit(e, w.damage, 18); e.hitCooldown = .4f;
                        if (branch == 2 && e.role != EnemyRole.Boss) e.slow = 1;
                        break;
                    }
                }
                RemoveDead(); return;
            }
            attackTimer -= dt;
            if (attackTimer > 0) return;
            attackTimer = w.interval / AttackSpeed;
            if (animal == Animal.Capybara)
            {
                if (CountAttacks(false) < CozyRules.AttackCap) attacks.Add(new RunAttack { kind = 1, p = position, life = .55f, radius = w.range * RangeScale, damage = w.damage });
            }
            else
            {
                var targets = enemies.FindAll(e => e.hp > 0 && e.warmup <= 0 && Vector2.Distance(e.p, position) <= w.range * RangeScale);
                targets.Sort((a, b) => { int d = (a.p - position).sqrMagnitude.CompareTo((b.p - position).sqrMagnitude); return d == 0 ? a.id.CompareTo(b.id) : d; });
                for (int i = 0; i < Mathf.Min(w.count, targets.Count) && CountAttacks(false) < CozyRules.AttackCap; i++)
                    attacks.Add(new RunAttack { kind = 0, p = position, v = (targets[i].p - position).normalized * 420, life = 1.2f, damage = w.damage, radius = 8, pierce = branch == 1 ? 3 : 1 });
            }
        }
        void UpdateAttacks(float dt)
        {
            for (int i = attacks.Count - 1; i >= 0; i--)
            {
                var a = attacks[i]; Vector2 before = a.p; a.age += dt; a.life -= dt; a.p += a.v * dt;
                if (a.kind == 2 && CozyRules.SegmentDistance(before, a.p, position) <= 13 + a.radius) { DamagePlayer(a.source, a.p); a.life = 0; }
                if (a.kind == 3 && a.age >= 1 && (a.p - position).sqrMagnitude < (60 + 13) * (60 + 13)) DamagePlayer(a.source, a.p);
                if (Finished) return;
                if (a.kind < 2)
                {
                    // Sort by distance along the shot: nearest collision consumes a non-piercing letter.
                    var targets = enemies.FindAll(e => e.hp > 0 && e.warmup <= 0 && !a.hit.Contains(e.id));
                    if (a.kind == 0) targets.Sort((x, y) => (x.p - before).sqrMagnitude.CompareTo((y.p - before).sqrMagnitude));
                    foreach (var e in targets)
                    {
                        bool contact = a.kind == 0 ? CozyRules.SegmentDistance(before, a.p, e.p) <= a.radius + e.Radius : a.age >= .2f && Vector2.Distance(a.p, e.p) <= a.radius * Mathf.Clamp01((a.age - .2f) / .35f) + e.Radius;
                        if (!contact) continue;
                        Hit(e, a.damage, a.kind == 1 ? 32 : 0); a.hit.Add(e.id);
                        if (a.kind == 0 && --a.pierce <= 0) { a.life = 0; break; }
                    }
                    RemoveDead();
                }
                if (a.life <= 0) attacks.RemoveAt(i);
            }
        }
        public bool InObjectiveZone()
        {
            if (objectiveRemaining <= 0) return false;
            foreach (var p in objectivePoints) if (Vector2.Distance(position, p) <= 90) return true;
            return region == Region.PostOffice && position.magnitude <= 90;
        }
        public void Hit(RunEnemy e, float baseDamage, float knockback, bool fromWind = false)
        {
            if (e.hp <= 0 || Finished) return;
            float scale = 1 + .1f * passives[0] + (InObjectiveZone() ? .12f * passives[11] : 0);
            if (e.role >= EnemyRole.Elite) scale += .1f * passives[10] + (relics[2] ? .2f : 0);
            e.hp -= baseDamage * scale;
            if (e.role != EnemyRole.Boss) e.p += (e.p - position).normalized * knockback * (1 + .15f * passives[5]) * (e.role == EnemyRole.Elite ? .5f : 1);
            if (e.hp > 0) return;
            score += CozyRules.EnemyScore[(int)e.role]; kills++;
            int gain = CozyRules.EnemyXP[(int)e.role];
            if (gain > 0) DropXP(e.p, gain);
            if (!fromWind && relics[3] && ++windKills >= 30)
            {
                windKills = 0;
                foreach (var other in enemies) if (other.hp > 0 && Vector2.Distance(other.p, position) < 140) Hit(other, 10, 0, true);
            }
            if (e.role == EnemyRole.Boss) { phase = RunPhase.Won; resultReason = "victory"; score += 500 + hp * 50; velocity = Vector2.zero; }
        }
        void RemoveDead()
        {
            var children = new List<Vector2>();
            foreach (var e in enemies) if (e.hp <= 0 && e.role == EnemyRole.Splitter) { children.Add(e.p + Vector2.left * 16); children.Add(e.p + Vector2.right * 16); }
            enemies.RemoveAll(e => e.hp <= 0);
            if (!Finished) foreach (var p in children) Spawn(EnemyRole.Child, p);
        }
        public void DamagePlayer(string source, Vector2 attacker)
        {
            if (invincible > 0 || Finished) return;
            if (shield) { shield = false; invincible = 1 + .1f * passives[9]; return; }
            hp = Mathf.Max(0, hp - (difficulty == Difficulty.Cozy ? 1 : 2));
            lastDamage = source; invincible = 1 + .1f * passives[9];
            Vector2 away = (position - attacker).normalized;
            if (away.sqrMagnitude < .001f) away = Vector2.down;
            position = ResolvePosition(position + away * 18);
            if (hp <= 0) { phase = RunPhase.Lost; resultReason = source; velocity = Vector2.zero; choices.Clear(); }
        }
        void DropXP(Vector2 p, float amount)
        {
            // Merge crowded drops while preserving their value; no unbounded pickup objects.
            var nearest = pickups.Find(x => x.kind == 0 && Vector2.Distance(x.p, p) < 20);
            if (nearest == null && pickups.Count >= 256) nearest = pickups.Find(x => x.kind == 0);
            if (nearest != null) nearest.value += amount;
            else pickups.Add(new RunPickup { p = p, value = amount });
        }
        public void AddXP(float amount)
        {
            xp += amount * (1 + .08f * passives[7]);
            while (level < CozyRules.MaxLevel && xp >= CozyRules.XPRequired(level)) { xp -= CozyRules.XPRequired(level); level++; pendingLevels++; }
            if (level == CozyRules.MaxLevel && xp >= 1) { int whole = (int)xp; score += whole; xp -= whole; }
        }
        public void Heal(int amount) { hp = Mathf.Min(MaxHP, hp + amount + passives[8]); }
        void CollectPickups(float dt)
        {
            for (int i = pickups.Count - 1; i >= 0; i--)
            {
                var p = pickups[i]; p.life -= dt;
                float range = p.kind == 0 ? AbsorbRadius : 28;
                if (Vector2.Distance(p.p, position) <= range)
                { if (p.kind == 0) AddXP(p.value); else Heal((int)p.value); pickups.RemoveAt(i); }
                else if (p.life <= 0) pickups.RemoveAt(i);
            }
        }
        public void StartObjective()
        {
            objectiveIndex = nextObjective++; objectiveRemaining = region == Region.PostOffice ? 60 : 45;
            objectiveHeld = 0; objectiveCollected = 0; carrying = false; collected = new bool[5];
            var points = CozyRules.ObjectivePoints(region);
            objectivePoints = region == Region.Springs ? new[] { points[objectiveIndex % 3] } : points;
            for (int i = 0; i < objectivePoints.Length && region == Region.Snowfield; i++)
            {
                if (Vector2.Distance(objectivePoints[i], position) >= 180) continue;
                objectivePoints[i] = ResolvePosition(-objectivePoints[i]);
            }
        }
        void UpdateObjective(float dt)
        {
            if (objectiveRemaining <= 0) return;
            objectiveRemaining = Mathf.Max(0, objectiveRemaining - dt);
            if (objectiveRemaining <= 0) { carrying = false; return; }
            if (region == Region.Springs)
            { if (Vector2.Distance(position, objectivePoints[0]) <= 90) objectiveHeld += dt; if (objectiveHeld >= 10) CompleteObjective(); return; }
            for (int i = 0; i < objectivePoints.Length; i++)
            {
                if (collected[i] || carrying || Vector2.Distance(position, objectivePoints[i]) > 28) continue;
                collected[i] = true;
                if (region == Region.PostOffice) carrying = true; else objectiveCollected++;
            }
            if (region == Region.PostOffice && carrying && position.magnitude < 44) { carrying = false; objectiveCollected++; }
            if (objectiveCollected >= (region == Region.Snowfield ? 5 : 3)) CompleteObjective();
        }
        public void CompleteObjective()
        {
            if (objectiveRemaining <= 0) return;
            objectiveRemaining = 0; objectives++; score += 300; relicPending = true;
            // Completion-triggered effects are applied after selection, so a new map/yuzu also works now.
        }
        void ObjectiveRewardEffects()
        {
            if (relics[1]) Heal(2);
            if (relics[4]) { foreach (var p in pickups) if (p.kind == 0) AddXP(p.value); pickups.RemoveAll(p => p.kind == 0); }
        }
        public void ResolveChoices()
        {
            if (Finished || phase == RunPhase.Paused || phase == RunPhase.LevelChoice || phase == RunPhase.RelicChoice) return;
            if (pendingLevels > 0) { phase = RunPhase.LevelChoice; RollChoices(); }
            else if (relicPending) { phase = RunPhase.RelicChoice; RollChoices(); }
        }
        public void RollChoices()
        {
            choices.Clear(); var eligible = new List<int>();
            if (phase == RunPhase.RelicChoice)
            {
                for (int i = 0; i < relics.Length; i++) if (!relics[i]) eligible.Add(i);
                while (choices.Count < 2 && eligible.Count > 0) { int index = RandomInt(eligible.Count); choices.Add(eligible[index]); eligible.RemoveAt(index); }
                return;
            }
            if (weaponLevel < 5) eligible.Add(-1);
            int equipped = 0; foreach (int p in passives) if (p > 0) equipped++;
            for (int i = 0; i < 12; i++) if (passives[i] < 3 && (passives[i] > 0 || equipped < 4)) eligible.Add(i);
            int choiceLevel = level - pendingLevels + 1;
            if (choiceLevel <= 8 && choiceLevel % 2 == 0 && weaponLevel < 5) { choices.Add(-1); eligible.Remove(-1); }
            while (choices.Count < 3 && eligible.Count > 0) { int index = RandomInt(eligible.Count); choices.Add(eligible[index]); eligible.RemoveAt(index); }
            if (choices.Count < 3) choices.Add(-2);
        }
        public bool Reroll()
        {
            if (phase != RunPhase.LevelChoice || choosingBranch || rerolls <= 0) return false;
            rerolls--; RollChoices(); return true;
        }
        public bool Choose(int choice, int selectedBranch = 0)
        {
            if ((phase != RunPhase.LevelChoice && phase != RunPhase.RelicChoice) || !choices.Contains(choice)) return false;
            if (phase == RunPhase.RelicChoice)
            {
                relics[choice] = true; if (choice == 0) shield = true; if (choice == 5) hp = Mathf.Min(MaxHP, hp + 2);
                relicPending = false; ObjectiveRewardEffects();
            }
            else
            {
                if (choice == -1)
                {
                    if (weaponLevel == 4 && selectedBranch != 1 && selectedBranch != 2) { choosingBranch = true; return false; }
                    weaponLevel++; if (weaponLevel == 5) branch = selectedBranch;
                }
                else if (choice == -2) Heal(2);
                else { passives[choice]++; if (choice == 4) hp = Mathf.Min(MaxHP, hp + 2); }
                pendingLevels--;
            }
            choosingBranch = false; choices.Clear(); phase = RunPhase.Playing; ResolveChoices(); return true;
        }
        public int CalculateStars() => 5 * Mathf.FloorToInt(Mathf.Min(elapsed + .001f, 720) / 60) + 15 * objectives + (phase == RunPhase.Won ? 40 : 0);
        public string RecordKey => rulesVersion + "." + region + "." + animal + "." + difficulty + "." + (endless ? "endless" : "story");
    }
}
