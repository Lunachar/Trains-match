using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace SortingStation.Tests
{
    public sealed class SortingBoardTests
    {
        private static readonly MatchToken Red = new MatchToken(MatchTokenKind.Color, "red", "Красный", "красный", Color.red, "□");
        private static readonly MatchToken Blue = new MatchToken(MatchTokenKind.Color, "blue", "Синий", "синий", Color.blue, "▲");
        private static readonly MatchToken Green = new MatchToken(MatchTokenKind.Color, "green", "Зелёный", "зелёный", Color.green, "■");

        [Test]
        public void ColorBoard_CorrectTargetCompletesPiece()
        {
            SortingBoard board = new SortingBoard(GameMode.Colors, new[] { Red, Blue }, 2, 10);
            BoardPiece piece = board.Pieces[0];

            Assert.That(board.SelectPiece(piece.Id).Kind, Is.EqualTo(BoardActionKind.Selected));
            BoardActionResult result = board.SelectColorTarget(piece.Token.Id);

            Assert.That(result.Kind == BoardActionKind.Correct || result.Kind == BoardActionKind.Completed, Is.True);
            Assert.That(piece.Completed, Is.True);
        }

        [Test]
        public void ColorBoard_IncorrectTargetKeepsFirstSelection()
        {
            SortingBoard board = new SortingBoard(GameMode.Colors, new[] { Red, Blue }, 2, 20);
            BoardPiece piece = board.Pieces[0];
            string wrong = piece.Token.Id == Red.Id ? Blue.Id : Red.Id;
            board.SelectPiece(piece.Id);

            BoardActionResult result = board.SelectColorTarget(wrong);

            Assert.That(result.Kind, Is.EqualTo(BoardActionKind.Incorrect));
            Assert.That(board.SelectedPieceId, Is.EqualTo(piece.Id));
            Assert.That(piece.Completed, Is.False);
        }

        [Test]
        public void PairBoard_CreatesExactlyTwoOfEveryToken()
        {
            SortingBoard board = new SortingBoard(GameMode.Numbers, new[] { Red, Blue, Green }, 3, 30);

            Assert.That(board.Pieces, Has.Count.EqualTo(6));
            Assert.That(board.Pieces.GroupBy(piece => piece.Token.Id).All(group => group.Count() == 2), Is.True);
        }

        [Test]
        public void PairBoard_WrongChoiceDoesNotEraseProgressOrSelection()
        {
            SortingBoard board = new SortingBoard(GameMode.Letters, new[] { Red, Blue }, 2, 40);
            BoardPiece first = board.Pieces[0];
            BoardPiece wrong = board.Pieces.First(piece => piece.Token != first.Token);
            board.SelectPiece(first.Id);

            BoardActionResult result = board.SelectPiece(wrong.Id);

            Assert.That(result.Kind, Is.EqualTo(BoardActionKind.Incorrect));
            Assert.That(board.SelectedPieceId, Is.EqualTo(first.Id));
            Assert.That(board.Pieces.All(piece => !piece.Completed), Is.True);
        }

        [Test]
        public void PairBoard_AllCorrectPairsCompleteBoard()
        {
            SortingBoard board = new SortingBoard(GameMode.Letters, new[] { Red, Blue }, 2, 50);
            foreach (IGrouping<string, BoardPiece> pair in board.Pieces.GroupBy(piece => piece.Token.Id).ToArray())
            {
                BoardPiece[] pieces = pair.ToArray();
                board.SelectPiece(pieces[0].Id);
                board.SelectPiece(pieces[1].Id);
            }

            Assert.That(board.IsComplete, Is.True);
        }

        [Test]
        public void Constructor_DeduplicatesTokensAndLimitsDifficultyToFour()
        {
            SortingBoard board = new SortingBoard(GameMode.Colors, new[] { Red, Red, Blue, Green, new MatchToken(MatchTokenKind.Color, "yellow", "Жёлтый", "жёлтый", Color.yellow, "●"), new MatchToken(MatchTokenKind.Color, "extra", "Лишний", "лишний", Color.white, "★") }, 9, 60);

            Assert.That(board.Pieces, Has.Count.EqualTo(4));
            Assert.That(board.Pieces.Select(piece => piece.Token.Id).Distinct().Count(), Is.EqualTo(4));
        }
    }
}
