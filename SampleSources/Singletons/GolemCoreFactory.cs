using VContainer;

namespace Commar.CubicLand.Cube
{
    public class GolemCoreFactory : ObjectCubeFactory
    {
        private ICubeCollisionResolver _cubeCollisionResolver;
        private IOrphanedCubeHandler _orphanedCubeHandler;
        private ICubeFactory _cubeFactory;

        protected override string LoadKey => "GolemCore";
        protected override bool AutoReleaseOnDestroyed => false;

        public GolemCoreFactory(ICubeCollisionResolver cubeCollisionResolver, IOrphanedCubeHandler orphanedCubeHandler,
            [Key(CubeFactoryKey.Object)] ICubeFactory cubeFactory,
            IObjectPool objectPool, IAsyncAssetLoader assetLoader, ICubeSpawnEffect spawnEffect)
            : base(objectPool, assetLoader, spawnEffect)
        {
            _cubeCollisionResolver = cubeCollisionResolver;
            _orphanedCubeHandler = orphanedCubeHandler;
            _cubeFactory = cubeFactory;
        }

        protected override void OnCubeInitialized(CubeObject cube)
        {
            base.OnCubeInitialized(cube);

            GolemCore core = cube.GetComponent<GolemCore>();
            GolemGroundSensor groundSensor = cube.GetComponent<GolemGroundSensor>();
            GolemMotionController motionController = new GolemMotionController(core, groundSensor);

            groundSensor.Initialize(motionController.GeometryProvider);
            core.Initialize(_cubeCollisionResolver, _orphanedCubeHandler, _cubeFactory, motionController);
            core.onReleased += DestoryCube;
        }
    }
}