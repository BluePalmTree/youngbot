using System.Text;

namespace chess_engine.Helpers
{
    public static class StringUtils
    {
        public static string ToBinary(long value, int width = 64)
        {
            string raw = Convert.ToString(value, 2).PadLeft(width, '0');
            // Insert an underscore every 4 bits, right-to-left
            var sb = new StringBuilder("0b_");
            for (int i = 0; i < raw.Length; i++)
            {
                if (i > 0 && (raw.Length - i) % 8 == 0)
                    sb.Append('_');

                sb.Append(raw[i]);
            }

            return sb.ToString();
        }
    }
}