using System;
using UnityEngine;

namespace Cozy
{
    public enum Animal { Penguin, Capybara, Cat }
    public enum Region { Snowfield, Springs, PostOffice }
    public enum Difficulty { Cozy, Normal, Challenge }
    public enum RunPhase { Playing, LevelChoice, RelicChoice, Paused, Won, Lost, Retired }
    public enum EnemyRole { Basic, Tough, Charger, Ranged, Splitter, Support, Child, Elite, Boss }

    // GDD v1.0 pp.10-30. Coordinates use a centered canvas, positive Y upwards.
    public static class CozyRules
    {
        public const string Version = "commercial-0.3.0";
        public const int MaxLevel = 15, EnemyCap = 160, ProjectileCap = 48, AttackCap = 64;
        public const float Duration = 720, BossDuration = 90, Step = 1f / 60f;
        public static readonly float[] EnemyHP = { 10, 30, 18, 16, 24, 20, 6, 350, 1800 };
        public static readonly float[] EnemySpeed = { 65, 48, 55, 40, 55, 45, 80, 50, 35 };
        public static readonly int[] EnemyXP = { 1, 3, 2, 2, 2, 2, 0, 20, 0 };
        public static readonly int[] EnemyScore = { 10, 30, 20, 20, 20, 20, 0, 200, 1000 };
        public static readonly string[] AnimalsKO = { "펭귄", "카피바라", "고양이" };
        public static readonly string[] AnimalsEN = { "Penguin", "Capybara", "Cat" };
        public static readonly string[] RegionsKO = { "오로라 눈밭", "온천 마을", "달빛 우체국" };
        public static readonly string[] RegionsEN = { "Aurora Snowfield", "Hot Spring Village", "Moonlit Post Office" };
        public static readonly string[] DifficultyKO = { "포근", "보통", "도전" };
        public static readonly string[] DifficultyEN = { "Cozy", "Normal", "Challenge" };
        public static readonly string[] BossKO = { "큰 눈구름", "먹구름 솥", "밤편지 새" };
        public static readonly string[] BossEN = { "Great Snowcloud", "Storm Kettle", "Night Letter Bird" };
        public static readonly string[] PassiveKO = { "따뜻한 장갑", "가벼운 신발", "작은 시계", "넓은 리본", "포근한 담요", "튼튼한 방울", "자석 브로치", "호기심 수첩", "차 주머니", "은빛 단추", "등불 배지", "용기 핀" };
        public static readonly string[] PassiveEN = { "Warm Gloves", "Light Shoes", "Little Clock", "Wide Ribbon", "Cozy Blanket", "Sturdy Bell", "Magnet Brooch", "Curiosity Book", "Tea Pouch", "Silver Button", "Lantern Badge", "Courage Pin" };
        public static readonly string[] PassiveEffectKO = { "피해 +10%", "이동속도 +6%", "공격속도 +8%", "공격 범위 +8%", "최대 HP +2, HP +2", "넉백 +15%", "XP 흡수 반경 +25", "경험치 +8%", "회복 효과 +1 HP", "피격 무적 +0.1초", "정예·보스 피해 +10%", "목표 구역 피해 +12%" };
        public static readonly string[] PassiveEffectEN = { "Damage +10%", "Move speed +6%", "Attack speed +8%", "Attack range +8%", "Max HP +2, heal 2", "Knockback +15%", "XP pickup radius +25", "Experience +8%", "Healing +1 HP", "Invulnerability +0.1s", "Elite/boss damage +10%", "Objective area damage +12%" };
        public static readonly string[] RelicKO = { "눈결정", "유자 향", "별 우표", "바람 종", "낡은 지도", "친구의 배지" };
        public static readonly string[] RelicEN = { "Snow Crystal", "Yuzu Scent", "Star Stamp", "Wind Chime", "Old Map", "Friendship Badge" };
        public static readonly string[] RelicEffectKO = { "다음 피격 1회 방어", "목표 완료 시 HP +2", "정예·보스 피해 +20%", "30처치마다 주변에 피해 10", "목표 완료 시 필드 XP 전부 회수", "최대 HP +2, 이동속도 +5%" };
        public static readonly string[] RelicEffectEN = { "Block the next hit", "Heal 2 HP on objective success", "Elite/boss damage +20%", "10 area damage every 30 kills", "Collect all field XP on success", "Max HP +2, move speed +5%" };
        public static int XPRequired(int level) => 6 + 4 * level;
        public static float HPScale(Difficulty d) => d == Difficulty.Cozy ? .8f : d == Difficulty.Challenge ? 1.2f : 1;
        public static float SpeedScale(Difficulty d) => d == Difficulty.Cozy ? .85f : d == Difficulty.Challenge ? 1.1f : 1;
        public static float SpawnRate(float t) => t < 60 ? .8f : t < 180 ? 1.2f : t < 300 ? 1.8f : t < 480 ? 2.4f : t < 600 ? 3 : 3.6f;
        public static float HPScale(float t, bool endless) => t < 180 ? 1 : t < 300 ? 1.2f : t < 480 ? 1.5f : t < 600 ? 1.8f : 2.2f * (endless && t >= 720 ? Mathf.Pow(1.15f, 1 + Mathf.Floor((t - 720) / 120)) : 1);
        public static float SpeedScale(float t, bool endless) => (t < 180 ? 1 : t < 300 ? 1.05f : t < 480 ? 1.1f : t < 600 ? 1.15f : 1.2f) * (endless && t >= 720 ? Mathf.Min(1.5f, 1 + .03f * (1 + Mathf.Floor((t - 720) / 120))) : 1);
        public static Vector2 Point(float x, float y) => new Vector2(x - 640, 360 - y);
        public static Vector2[] Blockers(Region r) => r == Region.Snowfield ? new[] { Point(250, 250), Point(1030, 470) } : r == Region.Springs ? new[] { Point(370, 330), Point(910, 390) } : new[] { Point(420, 260), Point(850, 480) };
        public static float BlockerRadius(Region r) => r == Region.Snowfield ? 55 : r == Region.Springs ? 60 : 50;
        public static Vector2[] ObjectivePoints(Region r) => r == Region.Snowfield ? new[] { Point(220,180), Point(640,160), Point(1060,200), Point(1040,560), Point(250,550) } : r == Region.Springs ? new[] { Point(640,230), Point(640,490), Point(190,360) } : new[] { Point(220,180), Point(1060,180), Point(1020,560) };
        public static Vector2 Clamp(Vector2 p) => new Vector2(Mathf.Clamp(p.x, -592, 592), Mathf.Clamp(p.y, -312, 312));
        public static float SegmentDistance(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 d = b - a;
            return Vector2.Distance(p, a + d * (d.sqrMagnitude > .00001f ? Mathf.Clamp01(Vector2.Dot(p - a, d) / d.sqrMagnitude) : 0));
        }
        public static WeaponStats Weapon(Animal animal, int level, int branch)
        {
            if (animal == Animal.Penguin) return new WeaponStats { damage = level >= 5 ? 22 : level >= 4 ? 18 : level >= 2 ? 14 : 10, interval = level >= 4 ? 2 : 2.4f, range = level >= 5 ? branch == 1 ? 108 : 76 : 88, count = level >= 5 && branch == 1 ? 5 : level >= 3 ? 4 : 3 };
            if (animal == Animal.Capybara) return new WeaponStats { damage = level >= 5 ? 26 : level >= 4 ? 20 : level >= 2 ? 14 : 10, interval = level >= 5 && branch == 2 ? 1 : level >= 4 ? 1.45f : 1.6f, range = level >= 5 ? branch == 1 ? 195 : 150 : level >= 3 ? 165 : 145, count = 1 };
            return new WeaponStats { damage = level >= 5 ? branch == 1 ? 22 : 16 : level >= 4 ? 18 : level >= 2 ? 14 : 10, interval = level >= 4 ? .5f : level >= 3 ? .55f : .65f, range = 420, count = level >= 5 && branch == 2 ? 3 : 1 };
        }
    }
    public struct WeaponStats { public float damage, interval, range; public int count; }
}
