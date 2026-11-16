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

        // Call on a copy of the game made for the NPC's search only; its clones stay marked. A rule whose exact result is
        // costly may estimate in a search copy (Zephrys the Great's wish runs hundreds of simulations, and a search plays
        // him in dozens of lines). The authoritative game never estimates: what the NPC is finally offered is exact.
        public void MarkSearchCopy() => _gameConfig.SearchCopy = true;

        internal bool IsSearchCopy => _gameConfig.SearchCopy;
    }
}
