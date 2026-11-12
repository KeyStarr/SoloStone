namespace SabberStoneCore.Model
{
    public partial class Game
    {
        // Call on a copy of the game made for the NPC's search only. The setup config still holds both
        // players' full original deck lists, which would show the NPC the human's deck. The lists are only
        // read while a game is first set up, so clearing them in the copy changes nothing else.
        public void RedactSetupDecksForFairView()
        {
            _gameConfig.Player1Deck = null;
            _gameConfig.Player2Deck = null;
        }
    }
}
