using System.Diagnostics;
using chess_engine.Models;

namespace chess_engine.Helpers
{
    // Precomputed view of the position from the side-to-move's perspective.
    // Produced once by Compute(), consumed by the legal-move generator and IsInCheck.
    public class AttackData
    {
        private static readonly bool d = true;

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

            if (d) Debug.WriteLine($"Computing attack data for {Piece.GetColorText(opponent)}");

            for (int square = 0; square < 64; square++)
            {
                int piece = board.Squares[square];
                if (!Piece.IsColor(piece, opponent))
                    continue;

                int type = Piece.TypeOf(piece);
                switch (type)
                {
                    case Piece.Pawn:
                        AddPawnAttacks(data, square, opponent, ownKing);
                        break;
                    case Piece.Knight:
                        AddKnightAttacks(data, square, ownKing);
                        break;
                    case Piece.King:
                        AddKingAttacks(data, square);
                        break;
                    case Piece.Bishop:
                    case Piece.Rook:
                    case Piece.Queen:
                        AddSliderAttacks(board, data, square, type, ownKing);
                        break;
                }
            }

            if (d)
            {
                Debug.WriteLine($"Recorded attacks: {data.AttackMap.Count}");
                Debug.WriteLine($"Check block mask: {string.Join(", ", data.CheckBlockMask ?? [])}");
            }

            // Normalize check state: double check → empty block mask (no non-king move legal).
            if (data.Checkers.Count >= 2)
            {
                if (d) Debug.WriteLine($"Two or more checks possible only king moves legal");
                data.CheckBlockMask = [];
            }
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

                if (d) Debug.WriteLine($"Check block mask: {string.Join(", ", data.CheckBlockMask ?? [])}");
            }

            if (ownKing != -1)
                FindPins(board, data, ownKing, ownColor, opponent);

            if (d)
            {
                Debug.WriteLine($"Done - Computing attack data for {Piece.GetColorText(opponent)}");
                Debug.WriteLine(new string('-', 40));
            }

            return data;
        }

        // Pawn attacks are the two forward-diagonals only. Pushes and EP targets
        // are NOT attacks in the king-safety sense.
        private static void AddPawnAttacks(AttackData data, int square, int pawnColor, int ownKing)
        {
            int file = square % 8;
            int rank = square / 8;

            if (pawnColor == Piece.White)
            {
                if (rank < 7)
                {
                    if (file > 0)
                        RecordAttack(data, square + 7, square, ownKing, isSlider: false, rayOffset: 0);

                    if (file < 7)
                        RecordAttack(data, square + 9, square, ownKing, isSlider: false, rayOffset: 0);
                }
            }
            else
            {
                if (rank > 0)
                {
                    if (file > 0)
                        RecordAttack(data, square - 9, square, ownKing, isSlider: false, rayOffset: 0);

                    if (file < 7)
                        RecordAttack(data, square - 7, square, ownKing, isSlider: false, rayOffset: 0);
                }
            }
        }

        private static readonly (int deltaFile, int deltaRank)[] KnightJumps =
        [
            (+1, +2), (-1, +2), (+1, -2), (-1, -2),
            (+2, +1), (-2, +1), (+2, -1), (-2, -1),
        ];

        private static void AddKnightAttacks(AttackData data, int square, int ownKing)
        {
            int file = square % 8;
            int rank = square / 8;

            foreach (var (deltaFile, deltaRank) in KnightJumps)
            {
                int targetFile = file + deltaFile;
                int targetRank = rank + deltaRank;
                if (targetFile < 0 || targetFile > 7 || targetRank < 0 || targetRank > 7)
                    continue;

                int targetSquare = targetRank * 8 + targetFile;
                RecordAttack(data, targetSquare, square, ownKing, isSlider: false, rayOffset: 0);
            }
        }

        private static void AddKingAttacks(AttackData data, int square)
        {
            int file = square % 8;
            int rank = square / 8;

            for (int deltaFile = -1; deltaFile <= 1; deltaFile++)
            {
                for (int deltaRank = -1; deltaRank <= 1; deltaRank++)
                {
                    if (deltaFile == 0 && deltaRank == 0)
                        continue;

                    int targetFile = file + deltaFile;
                    int targetRank = rank + deltaRank;
                    if (targetFile < 0 || targetFile > 7 || targetRank < 0 || targetRank > 7)
                        continue;

                    int targetSquare = targetRank * 8 + targetFile;
                    // Kings can't check kings (would be illegal position), but mark as attacked anyway.
                    data.AttackMap.Add(targetSquare);
                }
            }
        }

        private static void AddSliderAttacks(Board board, AttackData data, int square, int pieceType, int ownKing)
        {
            int startDir = pieceType == Piece.Bishop ? 4 : 0;
            int endDir = pieceType == Piece.Rook ? 4 : 8;

            for (int dir = startDir; dir < endDir; dir++)
            {
                int offset = MoveGenerator.DirectionOffsets[dir];
                int maxSteps = MoveGenerator.NumSquaresToEdge[square][dir];
                bool passedKing = false;

                for (int step = 1; step <= maxSteps; step++)
                {
                    int targetSquare = square + offset * step;
                    data.AttackMap.Add(targetSquare);

                    int pieceAtTarget = board.Squares[targetSquare];
                    if (pieceAtTarget == Piece.None)
                        continue;

                    if (targetSquare == ownKing && !passedKing)
                    {
                        data.Checkers.Add(square);

                        // Single-check block mask: the slider's square itself (capture) plus
                        // every square strictly between slider and king (interpose).
                        data.CheckBlockMask ??= [];
                        data.CheckBlockMask.Add(square);
                        for (int s = 1; s < step; s++)
                            data.CheckBlockMask.Add(square + offset * s);

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
                    if (piece == Piece.None)
                        continue;

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
                    if (!Piece.IsColor(piece, opponent))
                        break;

                    int type = Piece.TypeOf(piece);
                    bool matches =
                        type == Piece.Queen ||
                        (diagonal && type == Piece.Bishop) ||
                        (!diagonal && type == Piece.Rook);

                    if (!matches)
                        break;

                    // firstOwn is pinned. Allowed targets: every square between king
                    // and pinner (exclusive of king) plus the pinner's own square.
                    var pinLine = new HashSet<int>();
                    for (int s = 1; s < step; s++)
                        pinLine.Add(ownKing + offset * s);

                    pinLine.Add(target);
                    if (d) Debug.WriteLine($"Pin line: {string.Join(", ", pinLine)}");

                    data.PinLines[firstOwn] = pinLine;
                    break;
                }
            }
        }

        // Helper so pawn/knight attack recording can report a checker uniformly.
        // (Sliders have their own path because of X-ray + block-mask logic.)
        private static void RecordAttack(AttackData data, int target, int attackerSquare, int ownKing, bool isSlider, int rayOffset)
        {
            data.AttackMap.Add(target);
            if (d) Debug.WriteLine($"Record attack for {attackerSquare} to {target}");

            if (target == ownKing)
            {
                data.Checkers.Add(attackerSquare);
                // Non-slider check: only way to resolve (besides king move) is to capture the checker.
                data.CheckBlockMask ??= [];
                data.CheckBlockMask.Add(attackerSquare);
                if (d) Debug.WriteLine(" The attack is a check!");
            }
        }
    }
}
