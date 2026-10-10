namespace Commar.CubicLand.Cube
{
    public enum GolemMoveState
    {
        Idle, Moving, Charging
    }

    public interface IGolemMotionController
    {
        public GolemMoveState MoveState { get; }
        public bool IsStun { get; }
        public bool IsAirborne { get; }
        public IGolemGeometryProvider GeometryProvider { get; }

        public void Move(Direction direction);
        public void Rotate(bool isRight);
        public void StartJumpCharge();
        public void ReleaseJump();
    }
}