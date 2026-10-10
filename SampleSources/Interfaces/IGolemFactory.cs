namespace Commar.CubicLand.Cube
{
    public interface IGolemFactory
    {
        public GolemCore CreateGolem(string name, GolemData golemData, CubeSpawnOptions options);
    }
}