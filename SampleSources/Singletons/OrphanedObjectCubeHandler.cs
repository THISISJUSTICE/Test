using VContainer;

namespace Commar.CubicLand.Cube
{
    public class OrphanedObjectCubeHandler : IOrphanedCubeHandler
    {
        private readonly ICubeFactory _cubeFactory;

        public OrphanedObjectCubeHandler([Key(CubeFactoryKey.Obstacle)] ICubeFactory cubeFactory)
        {
            _cubeFactory = cubeFactory;
        }

        public void HandleOrphanedCube(CubeObject cube)
        {
            ObstacleCube obstacleCube = _cubeFactory.CreateCube(cube.CubeData.Clone(), CubeSpawnOptions.IMMEDIATE).GetComponent<ObstacleCube>();
            obstacleCube.transform.position = cube.transform.position;
            obstacleCube.transform.rotation = cube.transform.rotation;
            obstacleCube.NormalizePose();
        }
    }
}