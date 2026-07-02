// ============================================================================
// ETD.UI - TurretCardPanel.cs
// Displays turret purchase cards at the bottom of game screen.
// Shows unlocked turrets and refreshes during the run when a new turret unlocks.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Core;
using ETD.Data;
using ETD.Gameplay;
using ETD.Inputs;

namespace ETD.UI
{
    public class TurretCardPanel : MonoBehaviour
    {
        [SerializeField] private Transform _cardContainer;
        [SerializeField] private GameObject _turretCardPrefab;
        [SerializeField] private RectTransform _cancelArea;
        [SerializeField] private GameDatabase _database;

        private readonly List<TurretCardUI> _cards = new();
        private readonly HashSet<string> _visibleTurretIds = new();
        private RunManager _runManager;
        private GameInputHandler _inputHandler;

        private void Start()
        {
            _runManager = ServiceLocator.Get<RunManager>();
            _inputHandler = FindFirstObjectByType<GameInputHandler>();

            EventBus.Subscribe<GoldChangedEvent>(OnGoldChanged);
            EventBus.Subscribe<UnlockNotificationEvent>(OnUnlockNotification);

            PopulateCards();
        }

        private void PopulateCards()
        {
            AddMissingUnlockedCards();
            RefreshAffordability();
        }

        private void AddMissingUnlockedCards()
        {
            if (_database == null || _database.Turrets == null) return;
            if (_cardContainer == null || _turretCardPrefab == null) return;

            var save = SaveSystem.Load();

            foreach (var turretData in _database.Turrets)
            {
                if (turretData == null) continue;
                if (_visibleTurretIds.Contains(turretData.Id)) continue;
                if (!IsTurretUnlocked(turretData, save)) continue;

                CreateCard(turretData);
            }
        }

        private bool IsTurretUnlocked(TurretData turretData, SaveData save)
        {
            if (turretData == null) return false;
            if (turretData.IsUnlockedByDefault) return true;

            return save != null
                && save.UnlockedTurretIds != null
                && System.Array.IndexOf(save.UnlockedTurretIds, turretData.Id) >= 0;
        }

        private void CreateCard(TurretData turretData)
        {
            var go = Instantiate(_turretCardPrefab, _cardContainer);
            var card = go.GetComponent<TurretCardUI>();
            if (card == null) card = go.AddComponent<TurretCardUI>();

            card.Initialize(turretData, OnCardClicked);
            _cards.Add(card);
            _visibleTurretIds.Add(turretData.Id);
        }

        private void OnUnlockNotification(UnlockNotificationEvent evt)
        {
            if (evt.UnlockType != (int)UnlockType.Turret) return;

            // A turret can unlock while the player is still in the run.
            // The save/cache is already updated by ChallengeTracker before this event,
            // so immediately add any newly unlocked turret cards instead of waiting
            // until the player returns to the hub or restarts the run.
            AddMissingUnlockedCards();
            RefreshAffordability();
        }

        private void OnCardClicked(TurretData data)
        {
            if (_runManager == null || _runManager.RunData.Gold < data.Cost) return;
            _inputHandler?.StartPlacement(data);
        }

        private void OnGoldChanged(GoldChangedEvent evt)
        {
            RefreshAffordability();
        }

        private void RefreshAffordability()
        {
            if (_runManager == null) return;
            int gold = _runManager.RunData.Gold;
            for (int i = 0; i < _cards.Count; i++)
            {
                _cards[i].SetAffordable(gold >= _cards[i].Data.Cost);
            }
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<GoldChangedEvent>(OnGoldChanged);
            EventBus.Unsubscribe<UnlockNotificationEvent>(OnUnlockNotification);
        }
    }
}
