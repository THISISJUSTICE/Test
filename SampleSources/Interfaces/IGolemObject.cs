using System.Collections;
using UnityEngine;

namespace Commar.CubicLand.Cube
{
    public interface IGolemObject
    {
        internal Rigidbody Rigidbody { get; }
        public GolemStructureData StructureData { get; }

        internal void AddUnityRoutine(IOnEnablable onEnablable);
        internal void AddUnityRoutine(IFixedUpdatable fixedUpdatable);
        internal void SetAttackMode(bool attackMode);
        internal Coroutine StartCoroutine(IEnumerator routine);
        internal void StopCoroutine(Coroutine coroutine);

        public float CalculateMoveTime(float initTime, float minTime, float maxTime);
        public CubeObject FindCube(Vector3Int position);
    }
}