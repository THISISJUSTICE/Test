using UnityEngine;

namespace Commar.CubicLand.Cube
{
    public static class CubeConfig
    {
        public static readonly Vector3Int CORE_POSITION = Vector3Int.zero;

        public const float CUBE_BASE_LENGTH = 1f;
        public const float ONE_CUBE_MASS = 10f;
        public const float MASS_RATE = 0.2f;

        public const float DAMAGE_SCALING_FACTOR = 6f;
        public const float ADDITIONAL_DAMAGE_IMPULSE_DIVISOR = 0.3f;
        public const float ARMOR_EXPONENTIAL_SCALE = 50f;

        public const float MAX_CUBE_NOMALIZE_TIME = 0.2f;
        public static readonly float MAX_CUBE_NOMALIZE_DISTANCE = Mathf.Sqrt(CUBE_BASE_LENGTH * 2f);

        public const float STATUS_COLOR_RATE = 0.999f;
        public const float GOLEM_COLOR_RATE = 1f - (1f - STATUS_COLOR_RATE) / 3f;

        public const float RESTITUTION = 0.5f;

        public const float WEIGHT_EXPONENT = 0.2f;
        public const float SPEED_EXPONENT = 0.3f;

        public const float KNOCKBACK_DECELERATION = 2f;
        public const float MAX_KNOCKBACK_TIME = 0.5f;
        public const float MAX_KNOCKBACK_DISTANCE = 5f;
        public const float MIN_KNOCKBACK_DISTANCE = 0.7f;

        public const float GOLEM_INIT_MOVE_TIME = 0.4f;
        public const float GOLEM_MAX_MOVE_TIME = 0.8f;
        public const float GOLEM_MIN_MOVE_TIME = 0.03f;

        public const float GOLEM_ROTATE_FRICTION = 1.03f;
        public const float GOLEM_JUMP_CHARGE_TIME = 0.01f;
        public const float GOLEM_SIZE_UP_TIME = 0.1f;

        public static class Status
        {
            public const float CHILD_REDUCE_RATE = 0.7f;

            public const int INIT_MOVE_SPEED = 10;
            public const int ADD_MOVE_SPEED = 2;

            public const int INIT_HP = 10;
            public const int ADD_HP = 5;

            public const int INIT_ARMOR = 1;
            public const int ADD_ARMOR = 1;

            public const int INIT_ATTACK = 5;
            public const int ADD_ATTACK = 1;
        }
    }
}