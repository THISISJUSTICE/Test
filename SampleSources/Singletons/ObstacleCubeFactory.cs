namespace Commar.CubicLand.Cube
{
    public class ObstacleCubeFactory : ObjectCubeFactory
    {
        private readonly ICubeCollisionResolver _cubeCollisionResolver;

        protected override string LoadKey => "ObstacleCube";

        public ObstacleCubeFactory(IObjectPool objectPool, IAsyncAssetLoader assetLoader, ICubeSpawnEffect spawnEffect, ICubeCollisionResolver cubeCollisionResolver)
            : base(objectPool, assetLoader, spawnEffect)
        {
            _cubeCollisionResolver = cubeCollisionResolver;
        }

        protected override void OnCubeInitialized(CubeObject cube)
        {
            base.OnCubeInitialized(cube);

            cube.GetComponent<ObstacleCube>().Initialize(_cubeCollisionResolver);
        }
    }
}