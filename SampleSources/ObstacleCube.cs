using System.Collections;
using UnityEngine;

namespace Commar.CubicLand.Cube
{
    [RequireComponent(typeof(CubeObject), typeof(Rigidbody))]
    public class ObstacleCube : MonoBehaviour, ICubeMotionAdjuster
    {
        private ICubeCollisionResolver _cubeCollisionResolver;
        private Rigidbody _rigidbody;

        public CubeObject Cube { get; private set; }

        private void Awake()
        {
            Cube = GetComponent<CubeObject>();
            _rigidbody = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            SetMotionConstraints(true, true);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision == null)
                return;

            _cubeCollisionResolver.OnCollision(gameObject, this, collision, false);
        }

        internal void Initialize(ICubeCollisionResolver cubeCollisionResolver)
        {
            _cubeCollisionResolver = cubeCollisionResolver;
        }

        public void ApplyKnockback(Vector3 impulse)
        {
            impulse = CubeUtil.GetValidKnockbackImpulse(impulse, Cube.Mass);
            if (impulse.magnitude <= 0f)
                return;

            StopAllCoroutines();
            SetMotionConstraints(false, true);
            _rigidbody.AddForce(impulse, ForceMode.Impulse);
            StartCoroutine(HandleKnockback());
        }

        public void NormalizePose()
        {
            StopAllCoroutines();
            StartCoroutine(NormalizePoseCoroutine());
        }

        private IEnumerator HandleKnockback()
        {
            yield return YieldCache.WaitForFixedUpdate;

            float waitTime = CubeUtil.CalculateKnockbackTime(_rigidbody.linearVelocity, Cube.Mass);
            if (waitTime > 0f)
                yield return GlobalRoot.Instance.YieldCache.GetWaitForSeconds(waitTime);

            _rigidbody.ClearVelocity();
            yield return NormalizePoseCoroutine();
        }

        private IEnumerator NormalizePoseCoroutine()
        {
            SetMotionConstraints(false, false);
            yield return CubeUtil.NormalizePoseCoroutine(_rigidbody);
            SetMotionConstraints(true, true);
        }

        private void SetMotionConstraints(bool isFreezePosition, bool isFreezeRotation)
        {
            _rigidbody.FreezePositionXZ(isFreezePosition);
            _rigidbody.FreezeRotation(isFreezeRotation);
        }
    }
}