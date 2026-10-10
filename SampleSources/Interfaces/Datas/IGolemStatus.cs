using UnityEngine;

namespace Commar.CubicLand.Cube
{
    public interface IGolemStatusPoint
    {
        public StatusPoint StatusPoint { get; }
        public int MoveSpeedPoint { get; }
        public int TotalCubeCount { get; }
        public int TotalStatusPoint { get; }
        public int TotalPoint { get; }

        public void EnhanceStatusPoint(StatusPoint addStatusPoint, int addMoveSpeedPoint);
        public bool AddCube(Vector3Int parentPosition, Direction3D direction);
    }

    public interface IGolemStatusValue
    {
        public StatusValue StatusValue { get; }
        public int MoveSpeed { get; }
        public int CurrentCubeCount { get; }
    }
}