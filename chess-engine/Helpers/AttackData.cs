using chess_engine.Models;

namespace chess_engine.Helpers
{
    // Precomputed view of the position from the side-to-move's perspective.
    // Produced once by Compute(), consumed by the legal-move generator and IsInCheck.
    public class AttackData
    {
        // Squares the opponent attacks. Sliders X-ray THROUGH our king only, so
        // the square immediately "behind" our king along an attacking ray is
        // also flagged — this is what stops the king from escaping backward.
        public HashSet<int> AttackMap = [];

        // Opponent pieces currently giving check to our king.
        public List<int> Checkers = [];

        // Squares a non-king move may land on to resolve check.
        //   null     → no check; no filter for non-king moves
        //   empty    → double check; no non-king move can help
        //   non-empty → single check; target must be in this set (block or capture)
        public HashSet<int>? CheckBlockMask;

        // For each own piece that is absolutely pinned to our king, the set of
        // squares it may legally move to (between-king-and-pinner exclusive of
        // king, plus the pinner's own square for pin-capturing).
        public Dictionary<int, HashSet<int>> PinLines = [];

        public bool InCheck => Checkers.Count >= 1;
        public bool InDoubleCheck => Checkers.Count >= 2;

        public static AttackData Compute(Board board, int ownColor)
        {
            var data = new AttackData();
            int opponent = ownColor == Piece.White ? Piece.Black : Piece.White;
            int ownKing = board.GetKingSquare(ownColor);

            for (int sq = 0; sq < 64; sq++)
            {
                int piece = board.Squares[sq];
                if (!Piece.IsColor(piece, opponent)) continue;

                int type = Piece.TypeOf(piece);
                switch (type)
                {
                    case Piece.Pawn:
                        AddPawnAttacks(data, sq, opponent, ownKing);
                        break;
                    case Piece.Knight:
                        AddKnightAttacks(data, sq, ownKing);
                        break;
                    case Piece.King:
                        AddKingAttacks(data, sq);
                        break;
                    case Piece.Bishop:
                    case Piece.Rook:
                    case Piece.Queen:
                        AddSliderAttacks(board, data, sq, type, ownKing);
                        break;
                }
            }

            // Normalize check state: double check → empty block mask (no non-king move legal).
            if (data.Checkers.Count >= 2)
                data.CheckBlockMask = [];
            // Count == 0: stays null (no filter). Count == 1: populated by slider/knight/pawn logic.

            // Special case: when single check comes from a pawn that just double-pushed,
            // an en-passant capture also resolves the check — even though the capturing
            // move lands on the EP target (empty square) rather than the checker's square.
            if (data.Checkers.Count == 1 && board.EnPassantSquare != -1 && data.CheckBlockMask != null)
            {
                int checker = data.Checkers[0];
                if (Piece.TypeOf(board.Squares[checker]) == Piece.Pawn)
                {
                    int diff = board.EnPassantSquare - checker;
                    if (diff == 8 || diff == -8)
                        data.CheckBlockMask.Add(board.EnPassantSquare);
                }
            }

            if (ownKing != -1)
                FindPins(board, data, ownKing, ownColor, opponent);

            return data;
        }

        // Pawn attacks are the two forward-diagonals only. Pushes and EP targets
        // are NOT attacks in the king-safety sense.
        private static void AddPawnAttacks(AttackData data, int sq, int pawnColor, int ownKing)
        {
            int file = sq % 8;
            int rank = sq / 8;

            if (pawnColor == Piece.White)
            {
                if (rank < 7)
                {
                    if (file > 0) RecordAttack(data, sq + 7, sq, ownKing, isSlider: false, rayOffset: 0);
                    if (file < 7) RecordAttack(data, sq + 9, sq, ownKing, isSlider: false, rayOffset: 0);
                }
            }
            else
            {
                if (rank > 0)
                {
                    if (file > 0) RecordAttack(data, sq - 9, sq, ownKing, isSlider: false, rayOffset: 0);
                    if (file < 7) RecordAttack(data, sq - 7, sq, ownKing, isSlider: false, rayOffset: 0);
                }
            }
        }

        private static readonly (int df, int dr)[] KnightJumps =
        {
            (+1, +2), (-1, +2), (+1, -2), (-1, -2),
            (+2, +1), (-2, +1), (+2, -1), (-2, -1),
        };

        private static void AddKnightAttacks(AttackData data, int sq, int ownKing)
        {
            int file = sq % 8;
            int rank = sq / 8;

            foreach (var (df, dr) in KnightJumps)
            {
                int tf = file + df;
                int tr = rank + dr;
                if (tf < 0 || tf > 7 || tr < 0 || tr > 7) continue;
                int target = tr * 8 + tf;
                RecordAttack(data, target, sq, ownKing, isSlider: false, rayOffset: 0);
            }
        }

        private static void AddKingAttacks(AttackData data, int sq)
        {
            int file = sq % 8;
            int rank = sq / 8;
            for (int df = -1; df <= 1; df++)
            {
                for (int dr = -1; dr <= 1; dr++)
                {
                    if (df == 0 && dr == 0) continue;
                    int tf = file + df;
                    int tr = rank + dr;
                    if (tf < 0 || tf > 7 || tr < 0 || tr > 7) continue;
                    int target = tr * 8 + tf;
                    // Kings can't check kings (would be illegal position), but mark as attacked anyway.
                    data.AttackMap.Add(target);
                }
            }
        }

        private static void AddSliderAttacks(Board board, AttackData data, int sq, int pieceType, int ownKing)
        {
            int startDir = pieceType == Piece.Bishop ? 4 : 0;
            int endDir = pieceType == Piece.Rook ? 4 : 8;

            for (int dir = startDir; dir < endDir; dir++)
            {
                int offset = MoveGenerator.DirectionOffsets[dir];
                int maxSteps = MoveGenerator.NumSquaresToEdge[sq][dir];
                bool passedKing = false;

                for (int step = 1; step <= maxSteps; step++)
                {
                    int target = sq + offset * step;
                    data.AttackMap.Add(target);

                    int pieceAtTarget = board.Squares[target];
                    if (pieceAtTarget == Piece.None) continue;

                    if (target == ownKing && !passedKing)
                    {
                        data.Checkers.Add(sq);

                        // Single-check block mask: the slider's square itself (capture) plus
                        // every square strictly between slider and king (interpose).
                        data.CheckBlockMask ??= new HashSet<int>();
                        data.CheckBlockMask.Add(sq);
                        for (int s = 1; s < step; s++)
                            data.CheckBlockMask.Add(sq + offset * s);

                        // X-ray: keep adding attacked squares past the king so the king
                        // can't escape backward along the ray.
                        passedKing = true;
                        continue;
                    }

                    // Any non-king blocker stops the ray (the blocker's square is itself attacked).
                    break;
                }
            }
        }

        // Pass 2: ray-scan outward from our king along each of 8 slider directions.
        // Rule: first piece encountered on the ray must be ours; second must be an
        // enemy slider of the matching axis. If both hold, the first piece is pinned.
        private static void FindPins(Board board, AttackData data, int ownKing, int ownColor, int opponent)
        {
            for (int dir = 0; dir < 8; dir++)
            {
                bool diagonal = dir >= 4;
                int offset = MoveGenerator.DirectionOffsets[dir];
                int maxSteps = MoveGenerator.NumSquaresToEdge[ownKing][dir];

                int firstOwn = -1;
                int firstOwnStep = -1;

                for (int step = 1; step <= maxSteps; step++)
                {
                    int target = ownKing + offset * step;
                    int piece = board.Squares[target];
                    if (piece == Piece.None) continue;

                    if (firstOwn == -1)
                    {
                        // First piece on the ray.
                        if (Piece.IsColor(piece, ownColor))
                        {
                            firstOwn = target;
                            firstOwnStep = step;
                            continue;
                        }
                        // First piece is opponent — no pin behind it. (If it was a slider
                        // of matching axis it's already recorded as a checker above.)
                        break;
                    }

                    // Second piece on the ray.
                    if (!Piece.IsColor(piece, opponent)) break;

                    int type = Piece.TypeOf(piece);
                    bool matches =
                        type == Piece.Queen ||
                        (diagonal && type == Piece.Bishop) ||
                        (!diagonal && type == Piece.Rook);

                    if (!matches) break;

                    // firstOwn is pinned. Allowed targets: every square between king
                    // and pinner (exclusive of king) plus the pinner's own square.
                    var pinLine = new HashSet<int>();
                    for (int s = 1; s < step; s++)
                        pinLine.Add(ownKing + offset * s);
                    pinLine.Add(target);
                    data.PinLines[firstOwn] = pinLine;
                    break;
                }
            }
        }

        // Helper so pawn/knight attack recording can report a checker uniformly.
        // (Sliders have their own path because of X-ray + block-mask logic.)
        private static void RecordAttack(AttackData data, int target, int attackerSq, int ownKing, bool isSlider, int rayOffset)
        {
            data.AttackMap.Add(target);
            if (target == ownKing)
            {
                data.Checkers.Add(attackerSq);
                // Non-slider check: only way to resolve (besides king move) is to capture the checker.
                data.CheckBlockMask ??= new HashSet<int>();
                data.CheckBlockMask.Add(attackerSq);
            }
        }
    }
}
