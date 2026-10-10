using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Commar.CubicLand.Cube
{
    internal class GolemMotionMotor : IOnEnablable, IGolemGeometryProvider
    {
        private readonly IGolemObject _golemObject;
        private GolemMoveDirection _moveDirection;

        private float _chargedHeight = 0f;

        private readonly Vector3[] _objectDirections = new Vector3[6];

        private readonly List<Vector3Int> _tempList = new List<Vector3Int>();
        private readonly Dictionary<Direction3D, Vector3Int> _tempDictionary = new Dictionary<Direction3D, Vector3Int>();
        private readonly List<GolemCubeData> _edgeCubes = new List<GolemCubeData>();

        private static readonly Direction3D[] PRIORITY_CANDIDATES = new Direction3D[]
        {
            Direction3D.Front,
            Direction3D.Right,
            Direction3D.Back,
            Direction3D.Left
        };
        private static readonly Direction3D[] REVERSE_PRIORITY_CANDIDATES = new Direction3D[]
        {
            Direction3D.Back,
            Direction3D.Left,
            Direction3D.Right,
            Direction3D.Front
        };

        public CubeObject RotateAxisCube { get; private set; }
        public CubeObject UpEdgeCube { get; private set; }
        public CubeObject BackEdgeCube { get; private set; }
        public CubeObject FrontEdgeCube { get; private set; }

        public int GolemWidth { get; private set; }
        public int GolemHeight { get; private set; }
        public int GolemBack { get; private set; }

        public Quaternion ViewRotation { get; private set; }

        private Transform Transform => _golemObject.Rigidbody.transform;

        internal GolemMotionMotor(IGolemObject golemObject)
        {
            _golemObject = golemObject;
        }

        public void OnEnable()
        {
            _chargedHeight = 0;

            _moveDirection = new GolemMoveDirection(Transform.forward, Transform.right, Transform.up);
            UpdateGeometryData();
        }

        public void FindEdgeCubeDatas(Direction3D direction, IList<GolemCubeData> cubeDatas)
        {
            _golemObject.StructureData.FindEdgeCubes(ConvertObjectDirection(direction), cubeDatas);
        }

        public void FindVisibleCubeDatas(Direction3D direction, IList<GolemCubeData> cubes)
        {
            _golemObject.StructureData.FindVisibleCubes(ConvertObjectDirection(direction), cubes);
        }

        public bool ReleaseJumpForce()
        {
            if (_chargedHeight > 0f && !_golemObject.Rigidbody.IsUnderGravity())
            {
                _golemObject.Rigidbody.AddForce(Vector3.up * CubeUtil.CalculateLiftForce(_golemObject.Rigidbody, _chargedHeight), ForceMode.Impulse);
                _chargedHeight = 0f;
                return true;
            }

            return false;
        }

        public IEnumerator MoveWithRoll(Direction direction, float duration)
        {
            Vector3 moveDirection = _moveDirection.GetDirection(direction).normalized;
            float halfLine = CubeConfig.CUBE_BASE_LENGTH / 2f;

            int currentDownLength = GetGolemLength(Direction3D.Down);
            int nextDownLength = GetGolemLength(DirectionUtils.ConvertDirection2DTo3D(direction));

            float currentHeight = (currentDownLength * 2f + 1f) * halfLine;
            float nextHeight = (nextDownLength * 2f + 1f) * halfLine;
            float rollAxisAngle = Utils.GetAngleBH(nextHeight, currentHeight);
            float rollHypot = Utils.GetHypotenuseBH(nextHeight, currentHeight);
            float previousAngle = 0f;

            Quaternion startRotation = _golemObject.Rigidbody.rotation;
            Quaternion targetRotation = Quaternion.FromToRotation(moveDirection, _moveDirection.Down) * startRotation;
            FixedStepTimer timer = new FixedStepTimer(duration);

            bool once = true;

            while (!timer.IsCompleted)
            {
                float ratio = timer.Progress;
                Quaternion rotation = Quaternion.Slerp(startRotation, targetRotation, ratio);
                float angle = Quaternion.Angle(startRotation, rotation);

                Vector3 position = _golemObject.Rigidbody.position;
                position += Vector3.up * (Utils.GetHeightLineHyA(rollHypot, angle + rollAxisAngle)
                    - Utils.GetHeightLineHyA(rollHypot, previousAngle + rollAxisAngle));
                position += -moveDirection * (Utils.GetBaseLineHyA(rollHypot, angle + rollAxisAngle)
                    - Utils.GetBaseLineHyA(rollHypot, previousAngle + rollAxisAngle));

                _golemObject.Rigidbody.MovePosition(position);
                _golemObject.Rigidbody.MoveRotation(rotation);

                previousAngle = angle;

                if (angle > 50f && once)
                {
                    once = false;
                    UpdateGeometryData();
                }

                yield return timer.Advance();
            }

            _golemObject.Rigidbody.position = CubeUtil.GetNormalizedPosition(_golemObject.Rigidbody.position);
            _golemObject.Rigidbody.rotation = CubeUtil.GetNormalizedRotation(_golemObject.Rigidbody.rotation);
            UpdateGeometryData();
        }

        public IEnumerator Tumble(Direction direction, float duration)
        {
            // TODO: 구현
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// 바닥에 붙었을 때만 정상 동작함
        /// </summary>
        public IEnumerator MoveWithJump(Direction direction, float duration)
        {
            yield return MoveWithJumpCoroutine(direction, duration, CubeConfig.CUBE_BASE_LENGTH * 0.5f);
        }

        public IEnumerator Move(Direction direction, float duration)
        {
            yield return MoveWithJumpCoroutine(direction, duration, 0f);
        }

        public IEnumerator Rotate(Direction direction, float duration)
        {
            Vector3Int rotateAxisPosition = FindRotateAxis(direction);
            RotateAxisCube = _golemObject.FindCube(rotateAxisPosition);

            Rigidbody rigidbody = _golemObject.Rigidbody;
            Quaternion startRotation = rigidbody.rotation;
            Vector3 moveDirection = _moveDirection.GetDirection(direction);
            Vector3 axisLocalPosition = (Vector3)(rotateAxisPosition - CubeConfig.CORE_POSITION) * CubeConfig.CUBE_BASE_LENGTH;
            Vector3 axisPosition = rigidbody.position + startRotation * axisLocalPosition;
            axisPosition.y = 0f;
            Quaternion moveRotation = Quaternion.FromToRotation(_moveDirection.Front, moveDirection);
            Quaternion targetRotation = moveRotation * startRotation;

            Quaternion startViewRotation = ViewRotation;
            Quaternion targetViewRotation = moveRotation * startViewRotation;

            FixedStepTimer timer = new FixedStepTimer(duration);
            bool once = true;

            while (!timer.IsCompleted)
            {
                float ratio = timer.Progress;
                Quaternion rotation = Quaternion.Slerp(startRotation, targetRotation, ratio);

                rigidbody.MoveRotation(rotation);
                ViewRotation = Quaternion.Slerp(startViewRotation, targetViewRotation, ratio);

                if (ratio > 0.55f && once)
                {
                    once = false;
                    _moveDirection.Rotate(moveRotation);
                }

                Vector3 rotatedAxisPosition = rigidbody.position + rotation * axisLocalPosition;
                rotatedAxisPosition.y = 0f;
                Vector3 positionOffset = axisPosition - rotatedAxisPosition;
                positionOffset.y = 0f;
                rigidbody.position += positionOffset;

                yield return timer.Advance();
            }

            Quaternion normalizedRotation = CubeUtil.GetNormalizedRotation(targetRotation);
            Vector3 rotatedFinalAxisPosition = rigidbody.position + normalizedRotation * axisLocalPosition;
            rotatedFinalAxisPosition.y = 0f;
            Vector3 finalPositionOffset = axisPosition - rotatedFinalAxisPosition;
            finalPositionOffset.y = 0f;

            rigidbody.rotation = normalizedRotation;
            rigidbody.position = CubeUtil.GetNormalizedPosition(rigidbody.position + finalPositionOffset);

            ViewRotation = _moveDirection.GetRotation();
        }

        public IEnumerator ChargeJump()
        {
            if (_chargedHeight > 0f)
                yield break;

            Utils.GetClosestAxisVector(Transform.GetDirections(), Vector3.up, out int index);
            index /= 2;
            Vector3 scaleDirection = new Vector3[] { Vector3.right, Vector3.up, Vector3.forward }[index];

            float golemHeight = GolemHeight * CubeConfig.CUBE_BASE_LENGTH;
            float maxHeight = CubeConfig.CUBE_BASE_LENGTH / 2f + (GolemHeight - 1) * CubeConfig.CUBE_BASE_LENGTH;
            Vector3 previousScale = Transform.localScale;

            float duration = GolemHeight * CubeConfig.GOLEM_JUMP_CHARGE_TIME;
            FixedStepTimer timer = new FixedStepTimer(duration);

            while (!timer.IsCompleted)
            {
                float ratio = timer.Progress;

                Transform.localScale = Vector3.one - scaleDirection * ratio / 2f;
                Transform.position += (Transform.localScale[index] - previousScale[index]) * golemHeight / 2f * Vector3.up;
                _chargedHeight = Mathf.Max(maxHeight * ratio, CubeConfig.CUBE_BASE_LENGTH / 2f);

                previousScale = Transform.localScale;

                yield return timer.Advance();
            }
        }

        public IEnumerator RestoreScale()
        {
            if (Transform.localScale == Vector3.one)
                yield break;

            Vector3 startScale = Transform.localScale;
            Utils.GetClosestAxisVector(Transform.GetDirections(), Vector3.up, out int index);
            index /= 2;
            float golemHeight = GolemHeight * CubeConfig.CUBE_BASE_LENGTH;

            float scaleRate = (1f - startScale[index]) / 0.5f;
            float duration = CubeConfig.GOLEM_SIZE_UP_TIME * scaleRate;
            FixedStepTimer timer = new FixedStepTimer(duration);
            float previousAxisScale = startScale[index];

            while (!timer.IsCompleted)
            {
                float ratio = timer.Progress;
                Vector3 scale = Vector3.Lerp(startScale, Vector3.one, ratio);

                Transform.position += (scale[index] - previousAxisScale) * golemHeight / 2f * Vector3.up;
                Transform.localScale = scale;
                previousAxisScale = scale[index];

                yield return timer.Advance();
            }

            _golemObject.Rigidbody.position += (1f - previousAxisScale) * golemHeight / 2f * Vector3.up;
            Transform.localScale = Vector3.one;
        }

        private IEnumerator MoveWithJumpCoroutine(Direction direction, float duration, float moveHeight)
        {
            Vector3 moveDirection = _moveDirection.GetDirection(direction).normalized;
            Vector3 startPosition = Transform.position;
            Vector3 targetPosition = startPosition + moveDirection * CubeConfig.CUBE_BASE_LENGTH;

            FixedStepTimer timer = new FixedStepTimer(duration);

            while (!timer.IsCompleted)
            {
                float ratio = timer.Progress;
                float heightRatio = ratio < 0.5f ? ratio * 2f : (1f - ratio) * 2f;

                Vector3 position = Vector3.Lerp(startPosition, targetPosition, ratio);
                position.y += moveHeight * heightRatio;
                _golemObject.Rigidbody.MovePosition(position);

                yield return timer.Advance();
            }

            _golemObject.Rigidbody.position = CubeUtil.GetNormalizedPosition(targetPosition);
        }

        private void UpdateGeometryData()
        {
            RotateAxisCube = _golemObject.FindCube(FindRotateAxis(Direction.Right));
            UpEdgeCube = _golemObject.FindCube(FindAxisCandidates(Direction3D.Up)[0]);
            BackEdgeCube = _golemObject.FindCube(FindAxisCandidates(Direction3D.Back)[0]);
            FrontEdgeCube = _golemObject.FindCube(FindAxisCandidates(Direction3D.Front)[0]);

            GolemWidth = GetGolemLength(Direction3D.Left) + GetGolemLength(Direction3D.Right) + 1;
            GolemHeight = GetGolemLength(Direction3D.Down) + GetGolemLength(Direction3D.Up) + 1;
            GolemBack = GetGolemLength(Direction3D.Back) + 1;

            ViewRotation = _moveDirection.GetRotation();
        }

        private Vector3[] GetObjectDirections()
        {
            int index = 0;
            _objectDirections[index++] = Transform.right;
            _objectDirections[index++] = -Transform.right;
            _objectDirections[index++] = Transform.up;
            _objectDirections[index++] = -Transform.up;
            _objectDirections[index++] = Transform.forward;
            _objectDirections[index++] = -Transform.forward;

            return _objectDirections;
        }

        private Direction3D ConvertObjectDirection(Direction3D direction)
        {
            Vector3[] objectDirections = GetObjectDirections();
            Vector3 moveDirection = _moveDirection.GetDirection(direction);

            return DirectionUtils.ConvertDirection(objectDirections, moveDirection);
        }

        private int GetGolemLength(Direction3D direction)
        {
            return Mathf.Abs(_golemObject.StructureData.GetDirectionEdge(ConvertObjectDirection(direction)));
        }

        private List<Vector3Int> FindAxisCandidates(Direction3D moveDirection)
        {
            _tempList.Clear();

            FindEdgeCubeDatas(moveDirection, _edgeCubes);
            if (_edgeCubes.Count <= 0) // Error
            {
                _tempList.Add(CubeConfig.CORE_POSITION);
                return _tempList;
            }

            if (_edgeCubes.Count == 1)
            {
                _tempList.Add(_edgeCubes[0].shapePoisition);
                return _tempList;
            }

            float nearestDist = Vector3Int.Distance(CubeConfig.CORE_POSITION, _edgeCubes[0].shapePoisition);
            foreach (GolemCubeData cubeData in _edgeCubes)
            {
                float dist = Vector3Int.Distance(CubeConfig.CORE_POSITION, cubeData.shapePoisition);
                if (dist < nearestDist)
                {
                    _tempList.Clear();
                    _tempList.Add(cubeData.shapePoisition);
                    nearestDist = dist;
                }
                else if (dist == nearestDist)
                    _tempList.Add(cubeData.shapePoisition);
            }

            return _tempList;
        }

        private Vector3Int FindRotateAxis(Direction direction)
        {
            List<Vector3Int> nearestCubes = FindAxisCandidates(Direction3D.Down);

            if (nearestCubes.Count == 1)
                return nearestCubes[0];

            _tempDictionary.Clear();
            foreach (Vector3Int position in nearestCubes)
            {
                Vector3 dir = _golemObject.Rigidbody.rotation * (Vector3)(position - CubeConfig.CORE_POSITION);
                dir.y = 0f;

                Direction3D key = DirectionUtils.ConvertDirection(_moveDirection.GetDirections(), dir);
                _tempDictionary[key] = position;
            }

            Direction3D[] priorities = PRIORITY_CANDIDATES;
            if (direction == Direction.Left)
                priorities = REVERSE_PRIORITY_CANDIDATES;

            for (int i = 0; i < priorities.Length; i++)
            {
                if (_tempDictionary.TryGetValue(priorities[i], out Vector3Int position))
                    return position;
            }

            return CubeConfig.CORE_POSITION;
        }
    }
}