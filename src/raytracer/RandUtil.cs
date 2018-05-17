using System.Numerics;

namespace RayTracer
{
    public static class RandUtil
    {
        public static uint XorShift(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 15;
            return state;
        }

        public static float RandomFloat(ref uint state)
        {
            return XorShift(ref state) * (1f / 4294967296f);
        }

        public static Vector3 RandomInUnitDisk(ref uint state)
        {
            Vector3 p;
            do
            {
                p = 2f * new Vector3(RandomFloat(ref state), RandomFloat(ref state), 0) - new Vector3(1, 1, 0);
            } while (Vector3.Dot(p, p) >= 1f);
            return p;
        }

        public static Vector3 RandomInUnitSphere(ref uint state)
        {
            Vector3 ret;
            do
            {
                ret = 2f * new Vector3(RandomFloat(ref state), RandomFloat(ref state), RandomFloat(ref state)) - Vector3.One;
            } while (ret.LengthSquared() >= 1f);
            return ret;
        }
    }
}
