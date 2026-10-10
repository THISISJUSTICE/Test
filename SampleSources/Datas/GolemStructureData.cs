using System;
using System.Collections.Generic;
using UnityEngine;

namespace Commar.CubicLand.Cube
{
    public class GolemStructureData
    {
        /*TODO: 저장 대상*/
        private readonly Dictionary<Vector3Int, GolemCubeData> _cubeDatas; // (0,0,0)은 Core (R:x+1, L:x-1, U:y+1, D:y-1, F:z+1, B:z-1)

        /*TODO: 저장 대상*/
        private readonly Dictionary<Vector3Int, List<Vector3Int>> _children;
        private readonly Dictionary<Vector3Int, Vector3Int> _parents;
        private readonly Dictionary<Vector3Int, int> _childDepths;

        private readonly List<Vector3Int> _availables = new List<Vector3Int>();
        private readonly Dictionary<Vector3Int, GolemCubeData> _visibleCubes = new Dictionary<Vector3Int, GolemCubeData>();

        public IReadOnlyDictionary<Vector3Int, GolemCubeData> CubeDatas => _cubeDatas;
        public IReadOnlyDictionary<Vector3Int, List<Vector3Int>> Children => _children;
        public IReadOnlyDictionary<Vector3Int, Vector3Int> Parents => _parents;
        public IReadOnlyDictionary<Vector3Int, int> ChildDepths => _childDepths;

        public GolemStructureData(Dictionary<Vector3Int, GolemCubeData> cubeDatas = null,
            Dictionary<Vector3Int, List<Vector3Int>> children = null)
        {
            if (cubeDatas != null)
                _cubeDatas = cubeDatas;
            else
                _cubeDatas = new Dictionary<Vector3Int, GolemCubeData>()
                { { CubeConfig.CORE_POSITION, new GolemCubeData(StatusPoint.Zero, CubeConfig.CORE_POSITION, GolemColorPoint.Zero) } };

            if (children != null)
                _children = children;
            else
                _children = new Dictionary<Vector3Int, List<Vector3Int>>();

            _parents = new Dictionary<Vector3Int, Vector3Int>();
            BuildParents();

            _childDepths = new Dictionary<Vector3Int, int>();
            _childDepths[CubeConfig.CORE_POSITION] = 0;
            BuildChildDepths();
        }

        public GolemStructureData Clone()
        {
            Dictionary<Vector3Int, GolemCubeData> cubeDatas = new Dictionary<Vector3Int, GolemCubeData>();
            foreach (KeyValuePair<Vector3Int, GolemCubeData> cubeData in CubeDatas)
                cubeDatas[cubeData.Key] = cubeData.Value.Clone();

            Dictionary<Vector3Int, List<Vector3Int>> children = new Dictionary<Vector3Int, List<Vector3Int>>();
            if (Children != null)
            {
                foreach (KeyValuePair<Vector3Int, List<Vector3Int>> data in Children)
                {
                    List<Vector3Int> list = new List<Vector3Int>();
                    list.AddRange(data.Value);
                    children.Add(data.Key, list);
                }
            }

            return new GolemStructureData(cubeDatas, children);
        }

        public int GetDirectionEdge(Direction3D direction)
        {
            int vectorIndex = (int)direction / 2;
            bool isMax = (int)direction % 2 == 0;

            SetAvailables();

            int result = 0;

            foreach (Vector3Int position in _availables)
            {
                if (!_cubeDatas.TryGetValue(position, out GolemCubeData data))
                    continue;

                if (isMax)
                    result = Mathf.Max(result, data.shapePoisition[vectorIndex]);
                else
                    result = Mathf.Min(result, data.shapePoisition[vectorIndex]);
            }

            return result;
        }

        public void FindEdgeCubes(Direction3D direction, IList<GolemCubeData> list)
        {
            if (list == null)
                return;

            int vectorIndex = (int)direction / 2;
            int edge = GetDirectionEdge(direction);

            list.Clear();

            foreach (Vector3Int position in _availables)
            {
                if (!_cubeDatas.TryGetValue(position, out GolemCubeData data))
                    continue;

                if (data.shapePoisition[vectorIndex] == edge)
                    list.Add(data);
            }
        }

        public void FindVisibleCubes(Direction3D direction, IList<GolemCubeData> list)
        {
            if (list == null)
                return;

            int vectorIndex = (int)direction / 2;
            bool isMax = (int)direction % 2 == 0;

            SetAvailables();
            _visibleCubes.Clear();
            list.Clear();

            foreach (Vector3Int position in _availables)
            {
                if (!_cubeDatas.TryGetValue(position, out GolemCubeData data))
                    continue;

                Vector3Int column = position;
                column[vectorIndex] = 0;

                if (!_visibleCubes.TryGetValue(column, out GolemCubeData visibleCube))
                {
                    _visibleCubes.Add(column, data);
                    continue;
                }

                int axisPosition = position[vectorIndex];
                int visibleAxisPosition = visibleCube.shapePoisition[vectorIndex];

                if ((isMax && axisPosition > visibleAxisPosition)
                    || (!isMax && axisPosition < visibleAxisPosition))
                    _visibleCubes[column] = data;
            }

            foreach (GolemCubeData visibleCube in _visibleCubes.Values)
                list.Add(visibleCube);
        }

        public void GetAddablePositions(Vector3Int parentPosition, IList<Vector3Int> list)
        {
            if (list == null)
                return;

            list.Clear();
            int length = Enum.GetValues(typeof(Direction3D)).Length;

            for (int i = 0; i < length; i++)
            {
                Vector3Int position = parentPosition + DirectionUtils.GetDirection3DValue((Direction3D)i);
                if (!_cubeDatas.ContainsKey(position))
                    list.Add(position);
            }
        }

        public void FindAllChildren(Vector3Int parentPosition, IList<GolemCubeData> list)
        {
            if (list == null)
                return;

            list.Clear();
            AddChildren(parentPosition, list);
        }

        public GolemCubeData FindCreatableCube(Vector3Int childPosition)
        {
            if (!_parents.TryGetValue(childPosition, out Vector3Int position)
                || !_cubeDatas.TryGetValue(position, out GolemCubeData current)
                || !_parents.TryGetValue(position, out Vector3Int parentPosition)
                || !_cubeDatas.TryGetValue(parentPosition, out GolemCubeData parent))
                return null;

            if (parent.IsBreaked)
                return FindCreatableCube(position);

            return current;
        }

        internal bool AddCube(Vector3Int parentPosition, Direction3D direction, out Vector3Int position)
        {
            position = parentPosition;
            if (!_cubeDatas.TryGetValue(parentPosition, out GolemCubeData parent))
                return false;

            position = parentPosition + DirectionUtils.GetDirection3DValue(direction);
            if (_cubeDatas.ContainsKey(position))
                return false;

            _cubeDatas[position] = parent.MakeChild(position);
            if (!_children.TryGetValue(parentPosition, out List<Vector3Int> children))
            {
                children = new List<Vector3Int>();
                _children[parentPosition] = children;
            }

            children.Add(position);
            _parents[position] = parentPosition;

            if (!_childDepths.TryGetValue(parentPosition, out int depth))
                depth = 0;
            _childDepths[position] = depth + 1;

            return true;
        }

        private void SetAvailables()
        {
            _availables.Clear();
            foreach (GolemCubeData data in _cubeDatas.Values)
            {
                if (!data.IsBreaked)
                    _availables.Add(data.shapePoisition);
            }
        }

        private void AddChildren(Vector3Int parentPosition, IList<GolemCubeData> list)
        {
            if (!_cubeDatas.ContainsKey(parentPosition) || !_children.ContainsKey(parentPosition) || _children[parentPosition].Count == 0)
                return;

            foreach (Vector3Int position in _children[parentPosition])
            {
                list.Add(_cubeDatas[position]);
                AddChildren(position, list);
            }
        }

        private void BuildParents()
        {
            foreach (Vector3Int parent in _children.Keys)
            {
                if (!_children.TryGetValue(parent, out List<Vector3Int> children)
                    || children == null || children.Count == 0)
                    continue;

                foreach (Vector3Int child in children)
                    _parents[child] = parent;
            }
        }

        private void BuildChildDepths()
        {
            Queue<Vector3Int> queue = new Queue<Vector3Int>();
            queue.Enqueue(CubeConfig.CORE_POSITION);

            while (queue.Count > 0)
            {
                Vector3Int parent = queue.Dequeue();
                int nextDepth = _childDepths[parent] + 1;

                if (!_children.TryGetValue(parent, out List<Vector3Int> children))
                    continue;

                foreach (Vector3Int child in children)
                {
                    _childDepths[child] = nextDepth;
                    queue.Enqueue(child);
                }
            }
        }
    }
}