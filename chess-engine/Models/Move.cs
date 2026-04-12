namespace chess_engine.Models
{
    // ushort bit layout (future):
    // [15..10] from-square (6 bits, 0-63)
    // [9..4]   to-square   (6 bits, 0-63)
    // [3..0]   flags       (4 bits)
    public readonly struct Move
    {
        public static readonly Move None = default;

        public readonly int From;        // 0–63
        public readonly int To;          // 0–63
        public readonly MoveFlag Flag;
        public readonly bool PromotionNeeded;


        public Move(int from, int to, MoveFlag flag = MoveFlag.Normal, bool promotionNeeded = false)
        {
            From = from;
            To = to;
            Flag = flag;
            PromotionNeeded = promotionNeeded;
        }

        public bool IsPromotion => Flag is
            MoveFlag.PromoteQueen or MoveFlag.PromoteRook or
            MoveFlag.PromoteBishop or MoveFlag.PromoteKnight;

        public override string ToString()
        {
            var fromRank = Board.RankOf(From) + 1;
            var fromFile = Board.FileOf(From);
            var toRank = Board.RankOf(To) + 1;
            var toFile = Board.FileOf(To);
            return $"{(char)('a' + fromFile)}{fromRank}->{(char)('a' + toFile)}{toRank}";
        }
    }


    public enum MoveFlag : byte
    {
        Normal = 0,
        DoublePawnPush = 1,
        KingSideCastle = 2,
        QueenSideCastle = 3,
        EnPassant = 4,
        PromoteKnight = 5,
        PromoteBishop = 6,
        PromoteRook = 7,
        PromoteQueen = 8,
    }
}