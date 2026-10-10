namespace Commar.CubicLand.Cube
{
    public readonly struct GolemCubeHandle
    {
        public readonly CubeObject cube;
        public readonly GolemCubeData data;

        public bool IsValid => cube != null && data != null;

        public GolemCubeHandle(CubeObject cube)
        {
            this.cube = cube;
            if (cube != null && cube.CubeData != null && cube.CubeData is GolemCubeData data)
                this.data = data;
            else
                this.data = null;
        }
    }
}