using UnityEngine;

namespace Cozy
{
    [CreateAssetMenu(menuName = "Cozy/Feel settings")]
    public sealed class CozyTuning : ScriptableObject
    {
        [Header("Movement in 1280 x 720 design coordinates")]
        public float moveSpeed = 220;
        public float responseSeconds = .08f;
        public float joystickRadius = 68;
        [Range(0, .4f)] public float deadZone = .08f;
        [Header("Snowflakes")]
        public float orbitRadius = 88;
        public float orbitSeconds = 2.4f;
        public float attackRadius = 16;
        public float hitCooldown = .4f;
        [Header("Contact")]
        public int health = 5;
        public float invulnerability = 1;
        public int enemyLimit = 100;
        [Header("Feel review")]
        public bool gentleMode = true;
        public float gentleSpawnRate = .8f;

        public static Vector2 Movement(Vector2 input, float speed) => Vector2.ClampMagnitude(input, 1) * speed;
        public static float SpawnRate(float time) => time < 30 ? .8f : time < 60 ? 1.2f : time < 120 ? 1.8f : time < 180 ? 2.4f : time < 300 ? 3 : 3.6f;
        public static float ToughChance(float time) => time < 60 ? 0 : time < 120 ? .1f : time < 180 ? .15f : time < 300 ? .2f : .25f;
        public static float SpeedMultiplier(float time) => time < 30 ? 1 : time < 60 ? 1.05f : time < 120 ? 1.1f : time < 180 ? 1.15f : time < 300 ? 1.2f : 1.25f;
    }
}
