using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Commar.CubicLand.Cube
{
    [RequireComponent(typeof(CubeObject), typeof(Rigidbody))]
    public class GolemCore : MonoBehaviour, IGolemObject
    {
        private ICubeCollisionResolver _cubeCollisionResolver;
        private IOrphanedCubeHandler _orphanedCubeHandler;
        private ICubeFactory _cubeFactory;
        private ICubeMotionAdjuster _motionAdjuster;

        private GolemData _golemData;

        private readonly HashSet<IOnEnablable> _onEnablables = new HashSet<IOnEnablable>();
        private readonly HashSet<IFixedUpdatable> _fixedUpdatables = new HashSet<IFixedUpdatable>();

        private readonly Dictionary<Vector3Int, GolemCubeHandle> _cubes = new Dictionary<Vector3Int, GolemCubeHandle>();

        private readonly List<GolemCubeData> _tempChildren = new List<GolemCubeData>();

        private Rigidbody _rigidbody;

        internal event Action<CubeObject> onReleased;

        Rigidbody IGolemObject.Rigidbody => _rigidbody;
        public GolemStructureData StructureData => _golemData.structureData;
        public IGolemMotionController MotionController { get; private set; }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            if (_golemData != null)
            {
                bool hasNull = false;

                foreach (IOnEnablable onEnalblable in _onEnablables)
                {
                    if (onEnalblable == null)
                    {
                        hasNull = true;
                        continue;
                    }

                    onEnalblable.OnEnable();
                }

                if (hasNull)
                    _onEnablables.Remove(null);
            }
        }

        private void OnDisable()
        {
            foreach (GolemCubeHandle handle in _cubes.Values)
            {
                if (handle.IsValid)
                    handle.cube.onCubeDestoried -= OnParentCubeDestoried;
            }

            _cubes.Clear();

            if (_golemData != null)
            {
                _golemData.onCubeChanged -= OnCubeChanged;
                _golemData.onCubeCreated -= OnCubeCreated;
            }

            _golemData = null;
        }

        private void FixedUpdate()
        {
            bool hasNull = false;

            foreach (IFixedUpdatable fixedUpdatable in _fixedUpdatables)
            {
                if (fixedUpdatable == null)
                {
                    hasNull = true;
                    continue;
                }

                fixedUpdatable.FixedUpdate();
            }

            if (hasNull)
                _fixedUpdatables.Remove(null);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision == null)
                return;

            foreach (ContactPoint contact in collision.contacts)
                _cubeCollisionResolver.OnCollision(contact.thisCollider.gameObject, _motionAdjuster, collision, true);
            UpdateGolemMass();
        }

        internal void Initialize(ICubeCollisionResolver cubeCollisionResolver, IOrphanedCubeHandler orphanedCubeHandler, ICubeFactory cubeFactory, GolemMotionController motionController)
        {
            _cubeCollisionResolver = cubeCollisionResolver;
            _orphanedCubeHandler = orphanedCubeHandler;
            _cubeFactory = cubeFactory;
            _motionAdjuster = motionController;
            MotionController = motionController;
        }

        public void Initialize(GolemData golemData, IList<GolemCubeHandle> handles)
        {
            if (_golemData != null)
            {
                _golemData.onCubeChanged -= OnCubeChanged;
                _golemData.onCubeCreated -= OnCubeCreated;
            }

            _golemData = golemData;
            golemData.onCubeChanged += OnCubeChanged;
            golemData.onCubeCreated += OnCubeCreated;

            _cubes.Clear();

            foreach (GolemCubeHandle handle in handles)
            {
                if (!handle.IsValid)
                    continue;

                _cubes.Add(handle.data.shapePoisition, handle);
                handle.cube.onCubeDestoried += OnParentCubeDestoried;
            }

            _golemData.SetAttackMode(false);
            UpdateGolemMass();

            OnEnable();
        }

        void IGolemObject.AddUnityRoutine(IOnEnablable onEnablable)
        {
            _onEnablables.Add(onEnablable);
        }

        void IGolemObject.AddUnityRoutine(IFixedUpdatable fixedUpdatable)
        {
            _fixedUpdatables.Add(fixedUpdatable);
        }

        void IGolemObject.SetAttackMode(bool attackMode)
        {
            _golemData.SetAttackMode(attackMode);
        }

        Coroutine IGolemObject.StartCoroutine(IEnumerator routine)
        {
            return StartCoroutine(routine);
        }

        void IGolemObject.StopCoroutine(Coroutine coroutine)
        {
            StopCoroutine(coroutine);
        }

        public float CalculateMoveTime(float initTime, float minTime, float maxTime)
        {
            float weightFactor = Mathf.Pow(_rigidbody.mass, CubeConfig.WEIGHT_EXPONENT) / Mathf.Pow(CubeUtil.CalculateBasicCubeObjectMass(), CubeConfig.WEIGHT_EXPONENT);
            float speedFactor = Mathf.Pow((float)CubeConfig.Status.INIT_MOVE_SPEED / _golemData.MoveSpeed, CubeConfig.SPEED_EXPONENT);
            float moveTime = initTime * weightFactor * speedFactor;

            return Mathf.Clamp(moveTime, minTime, maxTime);
        }

        public CubeObject FindCube(Vector3Int position)
        {
            if (_golemData == null)
                return null;

            if (_cubes.TryGetValue(position, out GolemCubeHandle handle))
                return handle.cube;
            return null;
        }

        public static bool AttachCube(GolemCubeHandle parentHandle, GolemCubeHandle childHandle)
        {
            if (!parentHandle.IsValid || !childHandle.IsValid)
                return false;

            Vector3Int childPosition = childHandle.data.shapePoisition;
            childHandle.cube.transform.SetParent(parentHandle.cube.transform);
            childHandle.cube.transform.localPosition = (Vector3)(childPosition - parentHandle.data.shapePoisition) * CubeConfig.CUBE_BASE_LENGTH;
            childHandle.cube.transform.localRotation = Quaternion.identity;
            childHandle.cube.transform.localScale = Vector3.one;

            return true;
        }

        private void OnParentCubeDestoried(CubeObject cube)
        {
            GolemCubeHandle handle = new GolemCubeHandle(cube);
            cube.onCubeDestoried -= OnParentCubeDestoried;

            if (!handle.IsValid)
                return;

            _cubes.Remove(handle.data.shapePoisition);
            handle.data.IsBreaked = true;

            StructureData.FindAllChildren(handle.data.shapePoisition, _tempChildren);
            foreach (GolemCubeData child in _tempChildren)
            {
                if (!_cubes.TryGetValue(child.shapePoisition, out GolemCubeHandle childHandle))
                    continue;

                _orphanedCubeHandler?.HandleOrphanedCube(childHandle.cube);
                _cubeFactory.DestoryCube(childHandle.cube);
                childHandle.cube.onCubeDestoried -= OnParentCubeDestoried;
                _cubes.Remove(child.shapePoisition);
                childHandle.data.StatusValue.ApplyDamage(childHandle.data.StatusValue.MaxHP);
                childHandle.data.IsBreaked = true;
            }

            UpdateGolemMass();

            if (handle.data.shapePoisition == CubeConfig.CORE_POSITION)
                onReleased?.Invoke(cube);
        }

        private void OnCubeChanged()
        {
            foreach (GolemCubeHandle handle in _cubes.Values)
                handle.cube.UpdateCubeObject();

            UpdateGolemMass();
        }

        private void OnCubeCreated(GolemCubeData cubeData)
        {
            Vector3Int position = cubeData.shapePoisition;
            if (_cubes.ContainsKey(position)
                || !StructureData.Parents.TryGetValue(position, out Vector3Int parentPosition)
                || !_cubes.TryGetValue(parentPosition, out GolemCubeHandle parentHandle))
            {
                Debug.LogError($"Invalid Try Create Cube: {name}'s {position}");
                cubeData.IsBreaked = true;
                return;
            }

            GolemCubeHandle handle = new GolemCubeHandle(_cubeFactory.CreateCube(cubeData, CubeSpawnOptions.ANIMATED));
            if (!handle.IsValid)
            {
                Debug.LogError($"Created GolemCubeHandle is invalid: {name}'s {position}");
                cubeData.IsBreaked = true;
                return;
            }

            AttachCube(parentHandle, handle);

            _cubes.Add(position, handle);
            handle.cube.onCubeDestoried += OnParentCubeDestoried;

            UpdateGolemMass();
        }

        private void UpdateGolemMass()
        {
            float mass = 0f;
            foreach (GolemCubeHandle handle in _cubes.Values)
            {
                mass += handle.cube.Mass;
            }
            _rigidbody.mass = mass;
        }
    }
}
