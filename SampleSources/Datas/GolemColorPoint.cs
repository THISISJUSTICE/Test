using UnityEngine;

namespace Commar.CubicLand.Cube
{
    public readonly struct GolemColorPoint
    {
        public readonly int r;
        public readonly int g;
        public readonly int b;

        public readonly static GolemColorPoint Zero = new GolemColorPoint(0, 0, 0);

        public GolemColorPoint(int r, int g, int b)
        {
            if (r < 0) r = 0;
            if (g < 0) g = 0;
            if (b < 0) b = 0;

            this.r = r;
            this.g = g;
            this.b = b;
        }

        public GolemColorPoint MakeChild()
        {
            const float rate = CubeConfig.Status.CHILD_REDUCE_RATE;
            return new GolemColorPoint(Mathf.FloorToInt(r * rate), Mathf.FloorToInt(g * rate), Mathf.FloorToInt(b * rate));
        }
    }
}