using System;
using System.Threading.Tasks;
using Game.Domain.Player;

namespace Game.Domain.Cards
{
    public enum AuthoritativeDrawEffectType
    {
        AddCoins = 0,
        AddEnergy = 1,
        LaunchMinigame = 2
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
        public static readonly int[] AllowedMultipliers = { 1, 2, 4, 8 };

        public readonly int DrawCost;
        public readonly int RequestedMultiplier;
        public readonly AuthoritativeDrawCardDefinition[] Cards;
        public readonly string DrawId;

        public AuthoritativeDrawRequest(
            int drawCost,
            int requestedMultiplier,
            AuthoritativeDrawCardDefinition[] cards,
            string drawId)
        {
            DrawCost = drawCost;
            RequestedMultiplier = IsAllowedMultiplier(requestedMultiplier) ? requestedMultiplier : 1;
            Cards = cards ?? Array.Empty<AuthoritativeDrawCardDefinition>();
            DrawId = drawId ?? string.Empty;
        }

        public bool IsValid
        {
            get { return !string.IsNullOrWhiteSpace(DrawId); }
        }

        private static bool IsAllowedMultiplier(int candidate)
        {
            int i;
            for (i = 0; i < AllowedMultipliers.Length; i++)
            {
                if (AllowedMultipliers[i] == candidate)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public enum AuthoritativeDrawStatus
    {
        Success = 0,
        NotEnoughEnergy = 1,
        DeckEmpty = 2,
        InvalidRequest = 3,
        Unavailable = 4,
        Error = 5,
        AlreadyProcessed = 6
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

        public static AuthoritativeDrawResult AlreadyProcessed(PlayerProfileSnapshot snapshot)
        {
            return new AuthoritativeDrawResult(
                AuthoritativeDrawStatus.AlreadyProcessed,
                snapshot,
                string.Empty,
                string.Empty,
                "Draw already processed.");
        }
    }

    public interface IAuthoritativeDrawService
    {
        bool IsReady { get; }
        Task<AuthoritativeDrawResult> TryDrawAsync(AuthoritativeDrawRequest request);
    }
}
