namespace Commar.CubicLand.Cube
{
    public class GolemCubeFactory : ObjectCubeFactory
    {
        protected override string LoadKey => "ObjectCube";

        public GolemCubeFactory(IObjectPool objectPool, IAsyncAssetLoader assetLoader, ICubeSpawnEffect spawnEffect)
            : base(objectPool, assetLoader, spawnEffect)
        { }
    }
}