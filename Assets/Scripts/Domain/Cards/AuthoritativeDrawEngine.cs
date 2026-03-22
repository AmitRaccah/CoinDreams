using System;
using System.Collections.Generic;
using Game.Domain.Cards.Effects;
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

            DrawModifiersService modifiers = new DrawModifiersService(snapshot.pendingDrawMultiplier);
            CapturingMinigameLauncher minigameLauncher = new CapturingMinigameLauncher();

            RewardContext rewardContext = new RewardContext(
                profile.Energy,
                profile.Currency,
                modifiers,
                minigameLauncher);

            ICardDeck deck = new WeightedRandomCardDeck(runtimeCards);
            DrawCardUseCase drawUseCase = new DrawCardUseCase(
                profile.Energy,
                deck,
                rewardContext,
                request.DrawCost);

            CardDefinition drawnCard;
            bool success = drawUseCase.TryDraw(out drawnCard);

            PlayerProfileSnapshot updatedSnapshot = profile.CreateSnapshot();
            updatedSnapshot.pendingDrawMultiplier = modifiers.GetPendingMultiplier();

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
                AuthoritativeDrawEffectDefinition sourceEffect = sourceEffects[i];
                IRewardEffect effect;
                if (!TryCreateEffect(sourceEffect, out effect))
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

        private static bool TryCreateEffect(
            AuthoritativeDrawEffectDefinition sourceEffect,
            out IRewardEffect effect)
        {
            effect = null;

            if (sourceEffect == null)
            {
                return false;
            }

            if (sourceEffect.EffectType == AuthoritativeDrawEffectType.AddCoins)
            {
                effect = new AddResourceEffect(RewardResourceType.Currency, sourceEffect.IntValue);
                return true;
            }

            if (sourceEffect.EffectType == AuthoritativeDrawEffectType.AddEnergy)
            {
                effect = new AddResourceEffect(RewardResourceType.Energy, sourceEffect.IntValue);
                return true;
            }

            if (sourceEffect.EffectType == AuthoritativeDrawEffectType.LaunchMinigame)
            {
                effect = new LaunchMinigameEffect(sourceEffect.StringValue);
                return true;
            }

            if (sourceEffect.EffectType == AuthoritativeDrawEffectType.DoubleNextDraw)
            {
                effect = new DoubleNextDrawEffect();
                return true;
            }

            return false;
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
            copy.pendingDrawMultiplier = snapshot.pendingDrawMultiplier;
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
