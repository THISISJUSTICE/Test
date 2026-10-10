using System;
using UnityEngine;

namespace Commar.CubicLand.Cube
{
    [RequireComponent(typeof(MeshRenderer), typeof(BoxCollider))]
    public class CubeObject : MonoBehaviour
    {
        private BoxCollider _collider;
        private MeshRenderer _meshRenderer;
        private ICubeTrait _cubeTrait;

        public event Action<CubeObject> onCubeDestoried;

        public CubeData CubeData { get; private set; }
        public float Mass { get; private set; }
        public Collider Collider => _collider;
        internal bool IsInitialized => _cubeTrait != null;

        private void Awake()
        {
            _meshRenderer = GetComponent<MeshRenderer>();
            _collider = GetComponent<BoxCollider>();
            _collider.size = Vector3.one * (CubeConfig.CUBE_BASE_LENGTH - Physics.defaultContactOffset * 4f);
        }

        internal void Initialize(CubeData cubeData, ICubeTrait cubeTrait)
        {
            _cubeTrait = cubeTrait;
            _cubeTrait.SetMeshRenderer(_meshRenderer);
            Initialize(cubeData);
        }

        public void Initialize(CubeData cubeData)
        {
            CubeData = cubeData;
            UpdateCubeObject();
        }

        public void OnDamaged(float selfMass, Vector3 impulse, CubeData collider, float extraDamage)
        {
            if (CubeData == null)
                return;

            if (collider != null)
                CubeData.ApplyDamage(selfMass, impulse, collider, extraDamage);
            else
                CubeData.ApplyDamage(selfMass, impulse);

            UpdateCubeObject();

            if (CubeData.StatusValue.HP <= 0)
                onCubeDestoried.Invoke(this);
        }

        public void UpdateCubeObject()
        {
            if (CubeData == null)
                return;

            Mass = _cubeTrait.CalculateMass(CubeData.StatusValue);
            _meshRenderer.SetPropertyBlock(_cubeTrait.BuildPropertyBlock(CubeData));
        }
    }
}