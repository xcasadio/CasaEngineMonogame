//-----------------------------------------------------------------------------
// MoveList.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------


namespace CasaEngine.Input
{
    public class MoveManager
    {
        private readonly Move[] moves;

        public MoveManager(IEnumerable<Move> moves)
        {
            // Store the list of moves in order of decreasing sequence length.
            // This greatly simplifies the logic of the DetectMove method.

#if EDITOR
            this.moves = moves.OrderByDescending(m => m.Sequence.Count).ToArray();
#else
            this.moves = moves.OrderByDescending(m => m.Sequence.Length).ToArray();
#endif
        }

        public Move DetectMove(InputManager input)
        {
            // Perform a linear search for a move which matches the input. This relies
            // on the moves array being in order of decreasing sequence length.
            foreach (Move move in moves)
            {
                if (input.Matches(move))
                {
                    return move;
                }
            }
            return null;
        }

        public int LongestMoveLength
        {
            get
            {
                // Since they are in decreasing order,
                // the first move is the longest.
#if EDITOR
                return moves[0].Sequence.Count;
#else
                return moves[0].Sequence.Length;
#endif
            }
        }
    }
}
