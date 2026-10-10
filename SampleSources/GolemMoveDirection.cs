using UnityEngine;

namespace Commar.CubicLand.Cube
{
    internal struct GolemMoveDirection
    {
        private readonly Vector3[] _directions;

        public Vector3 Right { get; private set; }
        public Vector3 Left { get => -Right; }
        public Vector3 Up { get; private set; }
        public Vector3 Down { get => -Up; }
        public Vector3 Front { get; private set; }
        public Vector3 Back { get => -Front; }

        public GolemMoveDirection(Vector3 front, Vector3 right, Vector3 up)
        {
            Right = right.normalized;
            Up = up.normalized;
            Front = front.normalized;

            _directions = new Vector3[6];
        }

        public void Rotate(Quaternion rotation)
        {
            Right = rotation * Right;
            Up = rotation * Up;
            Front = rotation * Front;
        }

        public Quaternion GetRotation()
        {
            float m00 = Right.x, m01 = Up.x, m02 = Front.x;
            float m10 = Right.y, m11 = Up.y, m12 = Front.y;
            float m20 = Right.z, m21 = Up.z, m22 = Front.z;

            float trace = m00 + m11 + m22;
            Quaternion q;

            if (trace > 0f)
            {
                float s = Mathf.Sqrt(trace + 1f) * 2f;
                q.w = 0.25f * s;
                q.x = (m21 - m12) / s;
                q.y = (m02 - m20) / s;
                q.z = (m10 - m01) / s;
            }
            else if (m00 > m11 && m00 > m22)
            {
                float s = Mathf.Sqrt(1f + m00 - m11 - m22) * 2f;
                q.w = (m21 - m12) / s;
                q.x = 0.25f * s;
                q.y = (m01 + m10) / s;
                q.z = (m02 + m20) / s;
            }
            else if (m11 > m22)
            {
                float s = Mathf.Sqrt(1f + m11 - m00 - m22) * 2f;
                q.w = (m02 - m20) / s;
                q.x = (m01 + m10) / s;
                q.y = 0.25f * s;
                q.z = (m12 + m21) / s;
            }
            else
            {
                float s = Mathf.Sqrt(1f + m22 - m00 - m11) * 2f;
                q.w = (m10 - m01) / s;
                q.x = (m02 + m20) / s;
                q.y = (m12 + m21) / s;
                q.z = 0.25f * s;
            }

            return q;
        }

        public Vector3[] GetDirections()
        {
            int index = 0;
            _directions[index++] = Right;
            _directions[index++] = Left;
            _directions[index++] = Up;
            _directions[index++] = Down;
            _directions[index++] = Front;
            _directions[index++] = Back;

            return _directions;
        }

        public Vector3 GetDirection(Direction direction)
        {
            switch (direction)
            {
                case Direction.Front:
                    return Front;
                case Direction.Back:
                    return Back;
                case Direction.Left:
                    return Left;
                case Direction.Right:
                    return Right;
            }

            return Vector3.zero;
        }

        public Vector3 GetDirection(Direction3D direction)
        {
            switch (direction)
            {
                case Direction3D.Front:
                    return Front;
                case Direction3D.Back:
                    return Back;
                case Direction3D.Up:
                    return Up;
                case Direction3D.Down:
                    return Down;
                case Direction3D.Left:
                    return Left;
                case Direction3D.Right:
                    return Right;
            }

            return Vector3.zero;
        }
    }
}