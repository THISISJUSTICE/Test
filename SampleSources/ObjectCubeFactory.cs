namespace Commar.CubicLand.Cube
{
    public abstract class ObjectCubeFactory : BaseCubeFactory
    {
        public ObjectCubeFactory(IObjectPool objectPool, IAsyncAssetLoader assetLoader, ICubeSpawnEffect spawnEffect)
            : base(objectPool, assetLoader, spawnEffect)
        { }

        protected override ICubeTrait CreateCubeTrait()
        {
            return new ObjectCubeTrait();
        }
    }
}