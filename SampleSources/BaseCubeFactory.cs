using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace Commar.CubicLand.Cube
{
    public abstract class BaseCubeFactory : ICubeFactory, IInitializable, IDisposable, IOperationHandle
    {
        protected readonly IObjectPool _objectPool;
        protected readonly IAsyncAssetLoader _assetLoader;
        protected readonly ICubeSpawnEffect _spawnEffect;

        private readonly Dictionary<CubeObject, PooledObjectHandle> _managedCubes = new Dictionary<CubeObject, PooledObjectHandle>();

        protected GameObject _cubePrefab;

        public OperationResult Result { get; private set; }
        public bool IsCompleted { get; private set; }

        protected abstract string LoadKey { get; }
        protected virtual bool AutoReleaseOnDestroyed => true;

        protected BaseCubeFactory(IObjectPool objectPool, IAsyncAssetLoader assetLoader, ICubeSpawnEffect spawnEffect)
        {
            _objectPool = objectPool;
            _assetLoader = assetLoader;
            _spawnEffect = spawnEffect;
        }

        public virtual async void Initialize()
        {
            try
            {
                OperationResult<GameObject> result = await _assetLoader.LoadAssetAsync<GameObject>(LoadKey);

                _cubePrefab = result.Value;
                Result = new OperationResult(result.IsSuccess, result.ErrorMessage);
            }
            catch (Exception exception)
            {
                Result = OperationResult.GetFailedResult(exception.Message);
            }
            finally
            {
                IsCompleted = true;
            }
        }

        public virtual void Dispose()
        {
            // TODO: Mobile 프로젝트에서 진행한 Addressable 리팩토링 구조 가져오기
            _assetLoader.ReleaseAsset(LoadKey);
        }

        public CubeObject CreateCube(CubeData cubeData, CubeSpawnOptions options)
        {
            PooledObjectHandle handle = _objectPool.Instantiate(_cubePrefab);
            CubeObject cube = handle.GameObject.GetComponent<CubeObject>();
            if (!cube.IsInitialized)
            {
                cube.Initialize(cubeData, CreateCubeTrait());
                OnCubeInitialized(cube);
            }
            else
                cube.Initialize(cubeData);

            if (AutoReleaseOnDestroyed)
                cube.onCubeDestoried += DestoryCube;

            if (!_managedCubes.ContainsKey(cube))
                _managedCubes.Add(cube, handle);

            if (options.IsAnimating)
                _spawnEffect.Play(cube);

            return cube;
        }

        public virtual void DestoryCube(CubeObject cube)
        {
            cube.onCubeDestoried -= DestoryCube;

            if (_managedCubes.Remove(cube, out PooledObjectHandle handle))
                _objectPool.Destroy(handle);
            else
                UnityEngine.Object.Destroy(cube.gameObject);
        }

        protected abstract ICubeTrait CreateCubeTrait();

        protected virtual void OnCubeInitialized(CubeObject cube) { }
    }
}