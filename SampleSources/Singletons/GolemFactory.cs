using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace Commar.CubicLand.Cube
{
    public sealed class GolemFactory : IGolemFactory
    {
        private readonly ICubeFactory _coreFactory;
        private readonly ICubeFactory _cubeFactory;

        public GolemFactory([Key(CubeFactoryKey.Core)] ICubeFactory coreFactory, [Key(CubeFactoryKey.Object)] ICubeFactory cubeFactory)
        {
            _coreFactory = coreFactory;
            _cubeFactory = cubeFactory;
        }

        public GolemCore CreateGolem(string name, GolemData golemData, CubeSpawnOptions options)
        {
            if (golemData == null)
            {
                Debug.LogError($"GolemData is null");
                return null;
            }

            GolemData instanceData = golemData.Clone();
            if (!instanceData.structureData.CubeDatas.TryGetValue(CubeConfig.CORE_POSITION, out GolemCubeData coreData))
            {
                Debug.LogError($"GolemData must contain core cube data.");
                return null;
            }

            CubeObject coreCube = _coreFactory.CreateCube(coreData, options);
            GolemCore golemCore = coreCube.GetComponent<GolemCore>();
            if (golemCore == null)
            {
                _coreFactory.DestoryCube(coreCube);
                Debug.LogError("The core factory must create a cube with GolemCore.");
                return null;
            }

            coreCube.name = name;
            coreCube.transform.SetParent(null);
            coreCube.transform.position = Vector3.zero;
            coreCube.transform.rotation = Quaternion.identity;
            coreCube.transform.localScale = Vector3.one;
            coreCube.Initialize(coreData);

            GolemCubeHandle coreHandle = new GolemCubeHandle(coreCube);
            List<GolemCubeHandle> handles = new List<GolemCubeHandle>() { coreHandle };
            CreateChildCubes(instanceData.structureData, coreData, coreHandle, handles, options);

            golemCore.Initialize(instanceData, handles);
            return golemCore;
        }

        private void CreateChildCubes(GolemStructureData structureData, GolemCubeData parentData, GolemCubeHandle parentHandle, List<GolemCubeHandle> handles, CubeSpawnOptions options)
        {
            if (!structureData.Children.TryGetValue(parentData.shapePoisition, out List<Vector3Int> childPositions)
                || childPositions == null || childPositions.Count == 0 || !parentHandle.IsValid)
                return;

            foreach (Vector3Int childPosition in childPositions)
            {
                if (!structureData.CubeDatas.TryGetValue(childPosition, out GolemCubeData childData)
                    || childData.IsBreaked)
                    continue;

                CubeObject childCube = _cubeFactory.CreateCube(childData, options);
                childCube.Initialize(childData);

                GolemCubeHandle childHandle = new GolemCubeHandle(childCube);
                GolemCore.AttachCube(parentHandle, childHandle);

                handles.Add(childHandle);
                CreateChildCubes(structureData, childData, childHandle, handles, options);
            }
        }
    }
}