using UnityEngine;

namespace Commar.CubicLand.Cube
{
    public class SkillCubeTrait : ICubeTrait
    {
        private MaterialPropertyBlock _propertyBlock;

        public void SetMeshRenderer(MeshRenderer renderer)
        {
            _propertyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(_propertyBlock);
        }

        public MaterialPropertyBlock BuildPropertyBlock(CubeData cubeData)
        {
            _propertyBlock.SetColor("_Color", cubeData.Color);
            return _propertyBlock;
        }

        public float CalculateMass(StatusValue statusValue)
        {
            return CubeConfig.ONE_CUBE_MASS;
        }
    }
}