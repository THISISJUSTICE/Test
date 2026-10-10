using UnityEngine;

namespace Commar.CubicLand.Cube
{
    public interface ICubeCollisionResolver
    {
        public void OnCollision(GameObject gameObject, ICubeMotionAdjuster motionAdjuster, Collision collision, bool onlyCubeCollision);
    }
}