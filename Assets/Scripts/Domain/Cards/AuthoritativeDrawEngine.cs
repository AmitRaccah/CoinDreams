using System;
using System.Collections.Generic;
using Game.Domain.Steal;
using Game.Domain.Player;
using Game.Domain.Time;

namespace Game.Domain.Cards
{
    public static class AuthoritativeDrawEngine
    {
        public static AuthoritativeDrawResult TryExecute(
            PlayerProfileSnapshot snapshot,
            AuthoritativeDrawRequest request,
            IRandomSource randomSource,
            ITimeProvider timeProvider)
        {
            if (snapshot == null)
            {
                return AuthoritativeDrawResult.Invalid("Player snapshot is null.");
            }

            if (request == null)
            {
                return AuthoritativeDrawResult.Invalid("Draw request is null.");
            }

            if (randomSource == null)
            {
                return AuthoritativeDrawResult.Invalid("Random source is null.");
            }

            if (timeProvider == null)
            {
                return AuthoritativeDrawResult.Invalid("Time provider is null.");
            }

            if (string.IsNullOrWhiteSpace(request.DrawId))
            {
                return AuthoritativeDrawResult.Invalid("Missing DrawId.");
            }

            if (request.DrawCost < 0)
            {
                return AuthoritativeDrawResult.Invalid("Draw cost must be zero or positive.");
            }

            if (ContainsDrawId(snapshot.processedImpactIds, request.DrawId))
            {
                return AuthoritativeDrawResult.AlreadyProcessed(snapshot);
            }

            try
            {
                return ExecuteCore(snapshot, request, randomSource, timeProvider);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ArgumentException exception)
            {
                return AuthoritativeDrawResult.Invalid(exception.Message);
            }
            catch (Exception exception)
            {
                return AuthoritativeDrawResult.Error(exception.Message);
            }
        }

        private static AuthoritativeDrawResult ExecuteCore(
            PlayerProfileSnapshot snapshot,
            AuthoritativeDrawRequest request,
            IRandomSource randomSource,
            ITimeProvider timeProvider)
        {
            CardDefinition[] runtimeCards = BuildRuntimeCards(request.Cards);

            PlayerProfile profile = PlayerProfile.FromSnapshot(snapshot, timeProvider);

            if (runtimeCards.Length == 0)
            {
                PlayerProfileSnapshot emptySnapshot = profile.CreateSnapshot();
                return AuthoritativeDrawResult.DeckEmpty(emptySnapshot);
            }

            DrawModifiersService modifiers = new DrawModifiersService(request.RequestedMultiplier);
            CapturingStealCardLauncher stealCardLauncher = new CapturingStealCardLauncher();

            RewardContext rewardContext = new RewardContext(
                profile.Energy,
                profile.Currency,
                modifiers,
                stealCardLauncher);

            ICardDeck deck = new WeightedRandomCardDeck(runtimeCards, randomSource);
            int effectiveDrawCost = ScaleDrawCost(request.DrawCost, request.RequestedMultiplier);
            DrawCardUseCase drawUseCase = new DrawCardUseCase(
                profile.Energy,
                deck,
                rewardContext,
                effectiveDrawCost);

            CardDefinition drawnCard;
            bool success = drawUseCase.TryDraw(out drawnCard);

            PlayerProfileSnapshot updatedSnapshot = profile.CreateSnapshot();

            if (!success)
            {
                return AuthoritativeDrawResult.NotEnoughEnergy(updatedSnapshot);
            }

            StampDrawId(updatedSnapshot, request.DrawId);

            string cardId = drawnCard != null ? drawnCard.Id : string.Empty;
            return AuthoritativeDrawResult.Success(
                updatedSnapshot,
                cardId,
                stealCardLauncher.LastLaunchedTriggerId);
        }

        private static void StampDrawId(PlayerProfileSnapshot resultSnapshot, string drawId)
        {
            string[] processed = resultSnapshot.processedImpactIds ?? Array.Empty<string>();
            if (Array.Exists(processed, x => x == drawId))
            {
                return;
            }

            string[] withDraw = new string[processed.Length + 1];
            Array.Copy(processed, withDraw, processed.Length);
            withDraw[processed.Length] = drawId;
            resultSnapshot.processedImpactIds = withDraw;
        }

        private static bool ContainsDrawId(string[] processedImpactIds, string drawId)
        {
            if (processedImpactIds == null || processedImpactIds.Length == 0)
            {
                return false;
            }

            int i;
            for (i = 0; i < processedImpactIds.Length; i++)
            {
                if (string.Equals(processedImpactIds[i], drawId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static CardDefinition[] BuildRuntimeCards(
            AuthoritativeDrawCardDefinition[] sourceCards)
        {
            if (sourceCards == null || sourceCards.Length == 0)
            {
                return Array.Empty<CardDefinition>();
            }

            List<CardDefinition> cards = new List<CardDefinition>(sourceCards.Length);

            int i;
            for (i = 0; i < sourceCards.Length; i++)
            {
                AuthoritativeDrawCardDefinition sourceCard = sourceCards[i];
                if (sourceCard == null)
                {
                    continue;
                }

                string cardId = sourceCard.CardId;
                if (string.IsNullOrWhiteSpace(cardId))
                {
                    continue;
                }

                int weight = sourceCard.Weight;
                if (weight <= 0)
                {
                    continue;
                }

                IRewardEffect[] effects = BuildEffects(sourceCard.Effects);
                cards.Add(new CardDefinition(cardId.Trim(), weight, effects));
            }

            if (cards.Count == 0)
            {
                return Array.Empty<CardDefinition>();
            }

            return cards.ToArray();
        }

        private static IRewardEffect[] BuildEffects(
            AuthoritativeDrawEffectDefinition[] sourceEffects)
        {
            if (sourceEffects == null || sourceEffects.Length == 0)
            {
                return Array.Empty<IRewardEffect>();
            }

            List<IRewardEffect> effects = new List<IRewardEffect>(sourceEffects.Length);

            int i;
            for (i = 0; i < sourceEffects.Length; i++)
            {
                IRewardEffect effect;
                if (!AuthoritativeEffectRegistry.TryCreate(sourceEffects[i], out effect))
                {
                    continue;
                }

                effects.Add(effect);
            }

            if (effects.Count == 0)
            {
                return Array.Empty<IRewardEffect>();
            }

            return effects.ToArray();
        }

        private static int ScaleDrawCost(int drawCost, int multiplier)
        {
            if (drawCost <= 0)
            {
                return 0;
            }

            long scaled = (long)drawCost * multiplier;
            if (scaled > int.MaxValue)
            {
                return int.MaxValue;
            }

            return (int)scaled;
        }

        private sealed class CapturingStealCardLauncher : IStealCardLauncher
        {
            public string LastLaunchedTriggerId { get; private set; }

            public CapturingStealCardLauncher()
            {
                LastLaunchedTriggerId = string.Empty;
            }

            public void Launch(string triggerId)
            {
                LastLaunchedTriggerId = triggerId ?? string.Empty;
            }
        }
    }
}
