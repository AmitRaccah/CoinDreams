using Game.Domain.Cards;

namespace Game.Runtime.Cards
{
    public static class AuthoritativeDrawResultFormatter
    {
        public static string Format(AuthoritativeDrawResult result)
        {
            if (result == null)
            {
                return "Draw failed.";
            }

            if (result.Status == AuthoritativeDrawStatus.Success)
            {
                string message = "Card: " + result.DrawnCardId;
                if (!string.IsNullOrEmpty(result.MinigameId))
                {
                    message += " | Minigame: " + result.MinigameId;
                }

                return message;
            }

            if (result.Status == AuthoritativeDrawStatus.NotEnoughEnergy)
            {
                return "Not enough energy.";
            }

            if (result.Status == AuthoritativeDrawStatus.DeckEmpty)
            {
                return "Deck is empty.";
            }

            if (!string.IsNullOrEmpty(result.Message))
            {
                return result.Message;
            }

            return "Draw failed.";
        }
    }
}
