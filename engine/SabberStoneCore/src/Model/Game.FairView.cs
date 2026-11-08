namespace SabberStoneCore.Model
{
    public partial class Game
    {
        public void RedactSetupDecksForFairView()
        {
            _gameConfig.Player1Deck = null;
            _gameConfig.Player2Deck = null;
        }
    }
}
