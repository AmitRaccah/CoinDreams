using System;
using System.Collections.Generic;
using Game.Domain.Minigames;
using Game.Domain.Player;
using Game.Domain.Time;

namespace Game.Domain.Cards
{
    public static class AuthoritativeDrawEngine
    {
        public static AuthoritativeDrawResult TryExecute(
            PlayerProfileSnapshot snapshot,
            AuthoritativeDrawRequest request)
        {
            if (snapshot == null)
            {
                return AuthoritativeDrawResult.Invalid("Player snapshot is null.");
            }

            if (request == null)
            {
                return AuthoritativeDrawResult.Invalid("Draw request is null.");
            }

            if (request.DrawCost < 0)
            {
                return AuthoritativeDrawResult.Invalid("Draw cost must be zero or positive.");
            }

            CardDefinition[] runtimeCards = BuildRuntimeCards(request.Cards);
            if (runtimeCards.Length == 0)
            {
                return AuthoritativeDrawResult.DeckEmpty(CopySnapshot(snapshot));
            }

            PlayerProfile profile;
            try
            {
                profile = PlayerProfile.FromSnapshot(snapshot, new TimeProvider());
            }
            catch (Exception exception)
            {
                return AuthoritativeDrawResult.Error(
                    "Failed to create profile from snapshot: " + exception.Message);
            }

            DrawModifiersService modifiers = new DrawModifiersService(request.RequestedMultiplier);
            CapturingMinigameLauncher minigameLauncher = new CapturingMinigameLauncher();

            RewardContext rewardContext = new RewardContext(
                profile.Energy,
                profile.Currency,
                modifiers,
                minigameLauncher);

            ICardDeck deck = new WeightedRandomCardDeck(runtimeCards);
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

            string cardId = drawnCard != null ? drawnCard.Id : string.Empty;
            return AuthoritativeDrawResult.Success(
                updatedSnapshot,
                cardId,
                minigameLauncher.LastLaunchedMinigameId);
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
                    weight = 1;
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

        private static PlayerProfileSnapshot CopySnapshot(PlayerProfileSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            PlayerProfileSnapshot copy = new PlayerProfileSnapshot();
            copy.playerId = snapshot.playerId;
            copy.revision = snapshot.revision;
            copy.coins = snapshot.coins;
            copy.currentEnergy = snapshot.currentEnergy;
            copy.regenMaxEnergy = snapshot.regenMaxEnergy;
            copy.storageMaxEnergy = snapshot.storageMaxEnergy;
            copy.regenIntervalSeconds = snapshot.regenIntervalSeconds;
            copy.lastRegenUtcTicks = snapshot.lastRegenUtcTicks;
            copy.villageLevels = CopyIntArray(snapshot.villageLevels);
            copy.processedImpactIds = CopyStringArray(snapshot.processedImpactIds);
            return copy;
        }

        private static int[] CopyIntArray(int[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<int>();
            }

            int[] copy = new int[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        private static string[] CopyStringArray(string[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<string>();
            }

            string[] copy = new string[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        private sealed class CapturingMinigameLauncher : IMinigameLauncher
        {
            public string LastLaunchedMinigameId { get; private set; }

            public CapturingMinigameLauncher()
            {
                LastLaunchedMinigameId = string.Empty;
            }

            public void Launch(string minigameId)
            {
                LastLaunchedMinigameId = minigameId ?? string.Empty;
            }
        }
    }
}
