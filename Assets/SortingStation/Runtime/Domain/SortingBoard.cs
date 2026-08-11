using System;
using System.Collections.Generic;
using System.Linq;

namespace SortingStation
{
    public enum BoardActionKind
    {
        Ignored,
        Selected,
        Deselected,
        Correct,
        Incorrect,
        Completed
    }

    public readonly struct BoardActionResult
    {
        public BoardActionKind Kind { get; }
        public int PrimaryPieceId { get; }
        public int SecondaryPieceId { get; }
        public MatchToken Token { get; }

        public BoardActionResult(BoardActionKind kind, int primaryPieceId, int secondaryPieceId, MatchToken token)
        {
            Kind = kind;
            PrimaryPieceId = primaryPieceId;
            SecondaryPieceId = secondaryPieceId;
            Token = token;
        }
    }

    public sealed class BoardPiece
    {
        public int Id { get; }
        public MatchToken Token { get; }
        public bool Completed { get; internal set; }

        public BoardPiece(int id, MatchToken token)
        {
            Id = id;
            Token = token;
        }
    }

    public sealed class SortingBoard
    {
        private readonly List<BoardPiece> pieces;
        private readonly GameMode mode;
        private int? selectedPieceId;

        public IReadOnlyList<BoardPiece> Pieces => pieces;
        public int? SelectedPieceId => selectedPieceId;
        public bool IsComplete => pieces.Count > 0 && pieces.All(piece => piece.Completed);

        public SortingBoard(GameMode mode, IEnumerable<MatchToken> sourceTokens, int optionCount, int seed)
        {
            if (mode == GameMode.CabRide)
            {
                throw new ArgumentException("Cab ride does not use a sorting board.", nameof(mode));
            }

            this.mode = mode;
            MatchToken[] available = (sourceTokens ?? Enumerable.Empty<MatchToken>())
                .Where(token => !string.IsNullOrWhiteSpace(token.Id))
                .GroupBy(token => token.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .Take(Math.Max(2, Math.Min(4, optionCount)))
                .ToArray();

            if (available.Length < 2)
            {
                throw new ArgumentException("A sorting board needs at least two distinct tokens.", nameof(sourceTokens));
            }

            pieces = new List<BoardPiece>();
            int id = 0;
            foreach (MatchToken token in available)
            {
                pieces.Add(new BoardPiece(id++, token));
                if (mode != GameMode.Colors)
                {
                    pieces.Add(new BoardPiece(id++, token));
                }
            }

            Shuffle(pieces, new Random(seed));
        }

        public BoardActionResult SelectPiece(int pieceId)
        {
            BoardPiece piece = pieces.FirstOrDefault(item => item.Id == pieceId);
            if (piece == null || piece.Completed)
            {
                return Result(BoardActionKind.Ignored, pieceId, -1, default);
            }

            if (mode == GameMode.Colors)
            {
                selectedPieceId = pieceId;
                return Result(BoardActionKind.Selected, pieceId, -1, piece.Token);
            }

            if (!selectedPieceId.HasValue)
            {
                selectedPieceId = pieceId;
                return Result(BoardActionKind.Selected, pieceId, -1, piece.Token);
            }

            if (selectedPieceId.Value == pieceId)
            {
                selectedPieceId = null;
                return Result(BoardActionKind.Deselected, pieceId, -1, piece.Token);
            }

            BoardPiece first = pieces.First(item => item.Id == selectedPieceId.Value);
            if (first.Token == piece.Token)
            {
                first.Completed = true;
                piece.Completed = true;
                selectedPieceId = null;
                return Result(IsComplete ? BoardActionKind.Completed : BoardActionKind.Correct, first.Id, piece.Id, piece.Token);
            }

            return Result(BoardActionKind.Incorrect, first.Id, piece.Id, piece.Token);
        }

        public BoardActionResult SelectColorTarget(string tokenId)
        {
            if (mode != GameMode.Colors || !selectedPieceId.HasValue)
            {
                return Result(BoardActionKind.Ignored, -1, -1, default);
            }

            BoardPiece piece = pieces.First(item => item.Id == selectedPieceId.Value);
            if (string.Equals(piece.Token.Id, tokenId, StringComparison.Ordinal))
            {
                piece.Completed = true;
                selectedPieceId = null;
                return Result(IsComplete ? BoardActionKind.Completed : BoardActionKind.Correct, piece.Id, -1, piece.Token);
            }

            return Result(BoardActionKind.Incorrect, piece.Id, -1, piece.Token);
        }

        public bool CancelSelection()
        {
            if (!selectedPieceId.HasValue) return false;
            selectedPieceId = null;
            return true;
        }

        private static BoardActionResult Result(BoardActionKind kind, int primary, int secondary, MatchToken token)
        {
            return new BoardActionResult(kind, primary, secondary, token);
        }

        private static void Shuffle<T>(IList<T> list, Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int swap = random.Next(i + 1);
                (list[i], list[swap]) = (list[swap], list[i]);
            }
        }
    }
}
