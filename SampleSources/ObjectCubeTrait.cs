using UnityEngine;

namespace Commar.CubicLand.Cube
{
    public class ObjectCubeTrait : ICubeTrait
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

            float rateHP = (float)cubeData.StatusValue.HP / cubeData.StatusValue.MaxHP;
            _propertyBlock.SetFloat("_Range", Mathf.Clamp01(1f - rateHP));
            _propertyBlock.SetFloat("_FadeAlpha", Mathf.Clamp01(1f - rateHP) * 0.5f);

            return _propertyBlock;
        }

        public float CalculateMass(StatusValue statusValue)
        {
            return CubeUtil.CalcualteCubeObjectMass(statusValue);
        }
    }
}