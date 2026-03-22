using System;
using System.Threading.Tasks;
using Game.Domain.Player;

namespace Game.Domain.Cards
{
    public enum AuthoritativeDrawEffectType
    {
        AddCoins = 0,
        AddEnergy = 1,
        LaunchMinigame = 2,
        DoubleNextDraw = 3
    }

    public sealed class AuthoritativeDrawEffectDefinition
    {
        public readonly AuthoritativeDrawEffectType EffectType;
        public readonly int IntValue;
        public readonly string StringValue;

        public AuthoritativeDrawEffectDefinition(
            AuthoritativeDrawEffectType effectType,
            int intValue,
            string stringValue)
        {
            EffectType = effectType;
            IntValue = intValue;
            StringValue = stringValue ?? string.Empty;
        }
    }

    public sealed class AuthoritativeDrawCardDefinition
    {
        public readonly string CardId;
        public readonly int Weight;
        public readonly AuthoritativeDrawEffectDefinition[] Effects;

        public AuthoritativeDrawCardDefinition(
            string cardId,
            int weight,
            AuthoritativeDrawEffectDefinition[] effects)
        {
            CardId = cardId ?? string.Empty;
            Weight = weight;
            Effects = effects ?? Array.Empty<AuthoritativeDrawEffectDefinition>();
        }
    }

    public sealed class AuthoritativeDrawRequest
    {
        public readonly int DrawCost;
        public readonly AuthoritativeDrawCardDefinition[] Cards;

        public AuthoritativeDrawRequest(int drawCost, AuthoritativeDrawCardDefinition[] cards)
        {
            DrawCost = drawCost;
            Cards = cards ?? Array.Empty<AuthoritativeDrawCardDefinition>();
        }
    }

    public enum AuthoritativeDrawStatus
    {
        Success = 0,
        NotEnoughEnergy = 1,
        DeckEmpty = 2,
        InvalidRequest = 3,
        Unavailable = 4,
        Error = 5
    }

    public sealed class AuthoritativeDrawResult
    {
        public readonly AuthoritativeDrawStatus Status;
        public readonly PlayerProfileSnapshot Snapshot;
        public readonly string DrawnCardId;
        public readonly string MinigameId;
        public readonly string Message;

        public bool IsSuccess
        {
            get { return Status == AuthoritativeDrawStatus.Success; }
        }

        private AuthoritativeDrawResult(
            AuthoritativeDrawStatus status,
            PlayerProfileSnapshot snapshot,
            string drawnCardId,
            string minigameId,
            string message)
        {
            Status = status;
            Snapshot = snapshot;
            DrawnCardId = drawnCardId ?? string.Empty;
            MinigameId = minigameId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public static AuthoritativeDrawResult Success(
            PlayerProfileSnapshot snapshot,
            string drawnCardId,
            string minigameId)
        {
            return new AuthoritativeDrawResult(
                AuthoritativeDrawStatus.Success,
                snapshot,
                drawnCardId,
                minigameId,
                string.Empty);
        }

        public static AuthoritativeDrawResult NotEnoughEnergy(PlayerProfileSnapshot snapshot)
        {
            return new AuthoritativeDrawResult(
                AuthoritativeDrawStatus.NotEnoughEnergy,
                snapshot,
                string.Empty,
                string.Empty,
                "Not enough energy.");
        }

        public static AuthoritativeDrawResult DeckEmpty(PlayerProfileSnapshot snapshot)
        {
            return new AuthoritativeDrawResult(
                AuthoritativeDrawStatus.DeckEmpty,
                snapshot,
                string.Empty,
                string.Empty,
                "Deck is empty.");
        }

        public static AuthoritativeDrawResult Invalid(string message)
        {
            return new AuthoritativeDrawResult(
                AuthoritativeDrawStatus.InvalidRequest,
                null,
                string.Empty,
                string.Empty,
                message);
        }

        public static AuthoritativeDrawResult Unavailable(string message)
        {
            return new AuthoritativeDrawResult(
                AuthoritativeDrawStatus.Unavailable,
                null,
                string.Empty,
                string.Empty,
                message);
        }

        public static AuthoritativeDrawResult Error(string message)
        {
            return new AuthoritativeDrawResult(
                AuthoritativeDrawStatus.Error,
                null,
                string.Empty,
                string.Empty,
                message);
        }
    }

    public interface IAuthoritativeDrawService
    {
        bool IsReady { get; }
        Task<AuthoritativeDrawResult> TryDrawAsync(AuthoritativeDrawRequest request);
    }
}
