using System;
using System.Collections.Generic;
using UnityEngine;

namespace Commar.CubicLand.Cube
{
    public class CubeCollisionResolver : ICubeCollisionResolver, IHitNotifier, IFixedUpdatable
    {
        private readonly HashSet<(GameObject, GameObject)> _hitPairs = new HashSet<(GameObject, GameObject)>();
        private readonly HashSet<(GameObject, GameObject)> _resolvedCollisions = new HashSet<(GameObject, GameObject)>();

        private Action _defferedCollisionAction;
        public event Action<GameObject, Collision> onHit;

        public void FixedUpdate()
        {
            _defferedCollisionAction?.Invoke();
            _defferedCollisionAction = null;
            _hitPairs.Clear();
            _resolvedCollisions.Clear();
        }

        public void OnCollision(GameObject gameObject, ICubeMotionAdjuster motionAdjuster, Collision collision, bool onlyCubeCollision)
        {
            if (gameObject == null || motionAdjuster == null || collision == null)
                return;

            CubeObject cubeObject = gameObject.GetComponent<CubeObject>();
            if (cubeObject == null)
                return;

            // TODO: 추후 넉백 테스트해보고, relativeVelocity * mass와 비교하여 더 적절한 계산식으로 수립
            Vector3 impulse = -collision.impulse;
            CubeObject collisionCube = collision.gameObject.GetComponent<CubeObject>();

            if (collisionCube == null && onlyCubeCollision)
                return;

            (GameObject, GameObject) collisionDirection = (gameObject, collision.gameObject);
            if (!_resolvedCollisions.Add(collisionDirection))
                return;

            if (collisionCube == null)
            {
                _defferedCollisionAction += () =>
                {
                    cubeObject.OnDamaged(cubeObject.Mass, impulse, null, CalculateExtraDamage(cubeObject));
                    motionAdjuster.ApplyKnockback(impulse);
                };
            }
            else
            {
                _defferedCollisionAction += () =>
                {
                    cubeObject.OnDamaged(cubeObject.Mass, impulse, collisionCube.CubeData, CalculateExtraDamage(cubeObject));
                    motionAdjuster.ApplyKnockback(impulse);
                };
            }

            _defferedCollisionAction += () =>
            {
                (GameObject, GameObject) pair = MakePair(gameObject, collision.gameObject);
                if (_hitPairs.Add(pair))
                    onHit?.Invoke(gameObject, collision);
            };
        }

        private (GameObject, GameObject) MakePair(GameObject a, GameObject b)
        {
            if (a.GetInstanceID().CompareTo(b.GetInstanceID()) < 0)
                return (a, b);
            else
                return (b, a);
        }

        private float CalculateExtraDamage(CubeObject cubeObject)
        {
            float extraDamage = 0f;
            if (cubeObject.CubeData is GolemCubeData cubeData)
                extraDamage = cubeData.CalculateExtraDamage();

            return extraDamage;
        }
    }
}