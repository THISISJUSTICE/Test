using System.Collections.Generic;
using UnityEngine;

namespace Commar.CubicLand.Cube
{
    internal sealed class GolemGroundDistanceProbe
    {
        private const int BOX_CAST_HIT_BUFFER_SIZE = 32;
        private const int GROUND_LAYER_MASK = ~0;
        private const float MAX_GROUND_DISTANCE = 100f;

        private readonly IGolemObject _golemObject;
        private readonly IGolemGeometryProvider _geometryProvider;
        private readonly Component _context;

        private readonly List<GolemCubeData> _visibleCubes = new List<GolemCubeData>();
        private readonly RaycastHit[] _hits = new RaycastHit[BOX_CAST_HIT_BUFFER_SIZE];

        private bool _loggedMissingCollider;
        private bool _isLoggedCastOverflow;

        internal GolemGroundDistanceProbe(IGolemObject golemObject, IGolemGeometryProvider geometryProvider, Component context)
        {
            _golemObject = golemObject;
            _geometryProvider = geometryProvider;
            _context = context;
        }

        public bool GetGroundDistance(out float distance)
        {
            distance = float.PositiveInfinity;

            Rigidbody rigidbody = _golemObject?.Rigidbody;
            if (rigidbody == null)
            {
                Debug.LogError($"{nameof(GolemGroundDistanceProbe)} cannot query ground without a Rigidbody.", _context);
                return false;
            }

            if (_geometryProvider == null)
            {
                Debug.LogError($"{nameof(GolemGroundDistanceProbe)} cannot query ground without a geometry provider.", _context);
                return false;
            }

            _geometryProvider.FindVisibleCubeDatas(Direction3D.Down, _visibleCubes);

            bool foundCollider = false;
            bool foundGround = false;
            for (int i = 0; i < _visibleCubes.Count; i++)
            {
                BoxCollider collider = _golemObject.FindCube(_visibleCubes[i].shapePoisition)?.Collider as BoxCollider;
                if (!IsUsableSelfCollider(collider, rigidbody))
                    continue;

                if (!TryGetFaceCastShape(collider, out Vector3 center, out Vector3 halfExtents,
                    out Quaternion orientation, out float distanceOffset))
                    continue;

                foundCollider = true;

                int hitCount = Physics.BoxCastNonAlloc(center, halfExtents, Vector3.down, _hits,
                    orientation, MAX_GROUND_DISTANCE, GROUND_LAYER_MASK, QueryTriggerInteraction.Ignore);

                if (hitCount >= _hits.Length && !_isLoggedCastOverflow)
                {
                    _isLoggedCastOverflow = true;
                    Debug.LogWarning($"{nameof(GolemGroundDistanceProbe)} filled its box cast hit buffer. Ground distance may be inaccurate.", _context);
                }

                for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
                {
                    RaycastHit hit = _hits[hitIndex];
                    if (!IsValidGroundHit(hit.collider, rigidbody))
                        continue;

                    if (float.IsNaN(hit.distance) || float.IsInfinity(hit.distance))
                        continue;

                    distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance + distanceOffset));
                    foundGround = true;
                }
            }

            if (!foundCollider)
            {
                if (!_loggedMissingCollider)
                {
                    _loggedMissingCollider = true;
                    Debug.LogWarning($"{nameof(GolemGroundDistanceProbe)} could not find an active visible BoxCollider owned by its Rigidbody.", _context);
                }
            }
            else
                _loggedMissingCollider = false;

            return foundGround;
        }

        public void Clear()
        {
            _visibleCubes.Clear();
        }

        private bool IsUsableSelfCollider(BoxCollider collider, Rigidbody rigidbody)
        {
            return collider != null && collider.enabled && collider.gameObject.activeInHierarchy
                && !collider.isTrigger && collider.attachedRigidbody == rigidbody;
        }

        private bool IsValidGroundHit(Collider collider, Rigidbody rigidbody)
        {
            if (collider == null || collider.isTrigger
                || collider.attachedRigidbody == rigidbody)
                return false;

            return !collider.transform.IsChildOf(rigidbody.transform);
        }

        private bool TryGetFaceCastShape(BoxCollider collider, out Vector3 center, out Vector3 halfExtents,
            out Quaternion orientation, out float distanceOffset)
        {
            Transform colliderTransform = collider.transform;
            orientation = colliderTransform.rotation;

            Vector3 scale = colliderTransform.lossyScale.Abs();
            Vector3 boxHalfExtents = Vector3.Scale(collider.size * 0.5f, scale);
            Vector3 localDown = Quaternion.Inverse(orientation) * Vector3.down;

            int faceAxis = 0;
            float closestAxisDot = Mathf.Abs(localDown.x);
            if (Mathf.Abs(localDown.y) > closestAxisDot)
            {
                faceAxis = 1;
                closestAxisDot = Mathf.Abs(localDown.y);
            }
            if (Mathf.Abs(localDown.z) > closestAxisDot)
                faceAxis = 2;

            Vector3 localFaceNormal = Vector3.zero;
            localFaceNormal[faceAxis] = localDown[faceAxis] >= 0f ? 1f : -1f;
            Vector3 faceNormal = orientation * localFaceNormal;

            float separation = Physics.defaultContactOffset;
            float castHalfThickness = separation * 0.5f;
            float faceExtent = boxHalfExtents[faceAxis];

            halfExtents = boxHalfExtents;
            halfExtents[faceAxis] = castHalfThickness;
            center = colliderTransform.position + faceNormal * (faceExtent + castHalfThickness + separation);
            distanceOffset = Mathf.Max(0f, Vector3.Dot(faceNormal, Vector3.down)) * (castHalfThickness * 2f + separation);

            return IsFinite(center) && IsFinite(halfExtents)
                && halfExtents.x > 0f && halfExtents.y > 0f && halfExtents.z > 0f;
        }

        private bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}