using Game.Domain.Cards;

namespace Game.Runtime.Cards
{
    public static class AuthoritativeDrawResultFormatter
    {
        private const string DefaultFailureMessage = "Draw failed.";

        public static string Format(AuthoritativeDrawResult result)
        {
            if (result == null)
            {
                return DefaultFailureMessage;
            }

            return result.Status switch
            {
                AuthoritativeDrawStatus.Success => FormatSuccess(result),
                AuthoritativeDrawStatus.NotEnoughEnergy => "Not enough energy.",
                AuthoritativeDrawStatus.DeckEmpty => "Deck is empty.",
                _ => FallbackMessage(result)
            };
        }

        private static string FormatSuccess(AuthoritativeDrawResult result)
        {
            string message = "Card: " + result.DrawnCardId;
            if (!string.IsNullOrEmpty(result.MinigameId))
            {
                message += " | Minigame: " + result.MinigameId;
            }

            return message;
        }

        private static string FallbackMessage(AuthoritativeDrawResult result)
        {
            if (!string.IsNullOrEmpty(result.Message))
            {
                return result.Message;
            }

            return DefaultFailureMessage;
        }
    }
}
