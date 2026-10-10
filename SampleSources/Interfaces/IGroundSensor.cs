using System;
using UnityEngine;

namespace Commar.CubicLand.Cube
{
    public interface IGroundSensor
    {
        public bool IsGrounded { get; }

        public Collider GroundedCollider { get; }
        public Rigidbody GroundedRigidbody { get; }

        public event Action OnGrounded;

        public bool GetGroundDistance(out float distance);
        public void NotifyAirborne();
    }
}