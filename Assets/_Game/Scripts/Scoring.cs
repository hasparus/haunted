namespace Demons
{
    public static class Scoring
    {
        public const int MaxDemons = 4;
        public const float ForgottenPenalty = 0.5f;
        const float PrestigePerPower = 10f;

        public static int StampCount(byte stamps)
        {
            int n = 0;
            for (; stamps != 0; stamps &= (byte)(stamps - 1)) n++;
            return n;
        }

        public static float Power(byte stamps)
        {
            int n = StampCount(stamps);
            return n == 0 ? 0 : 1 + 0.5f * (n - 1);
        }

        public static float Deliver(byte stamps, int deliverer, float[] prestige)
        {
            float power = Power(stamps);
            if (power == 0) return 0;
            float pot = power * PrestigePerPower;
            prestige[deliverer] += pot / 2;
            float share = pot / 2 / StampCount(stamps);
            for (int slot = 0; slot < MaxDemons; slot++)
                if ((stamps & (1 << slot)) != 0) prestige[slot] += share;
            return power;
        }
    }
}
