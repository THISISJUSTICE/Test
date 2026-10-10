using UnityEngine;

namespace Commar.CubicLand.Cube
{
    public interface ICubeMotionAdjuster
    {
        public void ApplyKnockback(Vector3 impulse);
        public void NormalizePose();
    }
}