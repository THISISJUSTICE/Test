using System;
using UnityEngine;

namespace Commar.CubicLand.Cube
{
    [RequireComponent(typeof(GolemCore))]
    public class GolemGroundSensor : MonoBehaviour, IGroundSensor
    {
        private IGolemObject _golemObject;
        private GolemGroundSupportTracker _groundSupportTracker;
        private GolemGroundDistanceProbe _groundDistanceProbe;

        private bool _needIgnoreGround;

        public bool IsGrounded => _groundSupportTracker != null && _groundSupportTracker.IsGrounded;
        public Collider GroundedCollider => _groundSupportTracker?.GroundedCollider;
        public Rigidbody GroundedRigidbody => _groundSupportTracker?.GroundedRigidbody;

        public event Action OnGrounded;

        private void Awake()
        {
            _golemObject = GetComponent<IGolemObject>();
            _groundSupportTracker = new GolemGroundSupportTracker(_golemObject, this);
        }

        private void OnDisable()
        {
            _needIgnoreGround = false;
            ClearGroundState();
        }

        private void OnCollisionEnter(Collision collision)
        {
            UpdateSupportCollision(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            if (_needIgnoreGround)
            {
                Rigidbody rigidbody = _golemObject?.Rigidbody;
                if (rigidbody != null && rigidbody.linearVelocity.y > 0f)
                    return;

                _needIgnoreGround = false;
            }

            UpdateSupportCollision(collision);
        }

        private void OnCollisionExit(Collision collision)
        {
            _groundSupportTracker.RemoveCollision(collision);
        }

        public void Initialize(IGolemGeometryProvider geometryProvider)
        {
            _groundDistanceProbe = new GolemGroundDistanceProbe(_golemObject, geometryProvider, this);
        }

        public bool GetGroundDistance(out float distance)
        {
            return _groundDistanceProbe.GetGroundDistance(out distance);
        }

        public void NotifyAirborne()
        {
            _needIgnoreGround = true;
            ClearGroundState();
        }

        private void UpdateSupportCollision(Collision collision)
        {
            if (!_groundSupportTracker.UpdateCollision(collision))
                return;

            _needIgnoreGround = false;
            OnGrounded?.Invoke();
        }

        private void ClearGroundState()
        {
            _groundSupportTracker?.Clear();
            _groundDistanceProbe?.Clear();
        }
    }
}