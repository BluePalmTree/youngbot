namespace chess_engine.Helpers
{
    public static class BitUtils
    {
        public static List<int> ToIntList(ulong value, int length = 64)
        {
            List<int> list = [];

            for (int i = 0; i < length; i++)
            {
                if ((value & 1UL << i) != 0)
                    list.Add(i);
            }

            return list;
        }

        public static ulong OrListTogether(IEnumerable<ulong> ulongs)
        {
            ulong ret = 0;

            foreach (ulong l in ulongs)
                ret |= l;

            return ret;
        }
    }
}