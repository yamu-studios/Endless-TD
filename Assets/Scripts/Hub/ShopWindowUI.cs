// ============================================================================
// ETD.Hub - ShopWindowUI.cs  [UPDATED]
// All supplies items now track and persist purchase counts.
// Traits use per-trait save levels via SaveData.TraitUpgradeIds/Levels.
// Re-roll token shows current stockpile count in slots.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Core;
using ETD.Data;
using ETD.Meta;

namespace ETD.Hub
{
    public class ShopWindowUI : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private GameDatabase _database;

        [Header("Supplies Section")]
        [SerializeField] private Transform    _suppliesContainer;
        [SerializeField] private GameObject   _shopIconItemPrefab;

        [Header("Traits Section")]
        [SerializeField] private Transform _traitsContainer;

        [Header("Info Panel")]
        [SerializeField] private Image    _infoIcon;
        [SerializeField] private TMP_Text _infoName;
        [SerializeField] private TMP_Text _infoExplanation;
        [SerializeField] private Transform   _slotsContainer;
        [SerializeField] private GameObject  _slotPrefab;
        [SerializeField] private TMP_Text _infoCost;
        [SerializeField] private Button   _buyButton;
        [SerializeField] private TMP_Text _buyButtonText;
        [SerializeField] private Button _refundButton;
        [SerializeField] private TMP_Text _refundButtonText;

        [Header("Colors")]
        [SerializeField] private Color _selectedBorderColor = new Color(0.3f, 0.85f, 1f);
        [SerializeField] private Color _defaultBorderColor  = new Color(0.3f, 0.3f, 0.3f);

        [Header("Pricing")]
        [SerializeField] private int   _traitSlotPrice        = 50;
        [SerializeField] private int   _rerollPrice           = 30;
        [SerializeField] private int   _metaBonusPrice        = 80;
        [SerializeField] private int   _xpMultiplierPrice     = 60;
        [SerializeField] private int   _goldMultiplierPrice   = 60;
        [SerializeField] private int   _maxHPPrice            = 70;
        [SerializeField] private int   _traitUpgradeBasePrice = 40;
        [SerializeField] private float _upgradePriceScale     = 1.5f;
        [SerializeField] private int   _spellUpgradePrice     = 90;

        [Header("Close")]
        [SerializeField] private Button _closeButton;

        [Header("Icons")]
        [SerializeField] private Sprite traitSlotIcon;
        [SerializeField] private Sprite secondToughtsIcon;
        [SerializeField] private Sprite coinMagnetIcon;
        [SerializeField] private Sprite wisdomCrystalIcon;
        [SerializeField] private Sprite fortifiedCoreIcon;
        [SerializeField] private Sprite fortuneVaultIcon;
        [SerializeField] private Sprite arcaneFocusIcon;

        private MetaProgressionManager _metaManager;
        private readonly List<ShopIconItem> _allItems = new();
        private ShopIconItem _selectedItem;
        private ShopItemData _selectedData;
        private int currentShopIndex = 0;

        private void OnEnable()
        {
            _metaManager = ServiceLocator.Get<MetaProgressionManager>();
            _closeButton?.onClick.RemoveAllListeners();
            _closeButton?.onClick.AddListener(() => gameObject.SetActive(false));
            _buyButton?.onClick.RemoveAllListeners();
            _buyButton?.onClick.AddListener(OnBuyClicked);
            _refundButton?.onClick.RemoveAllListeners();
            _refundButton?.onClick.AddListener(OnRefundClicked);
            GenerateAll();
        }
        private void Awake()
        {
            EventBus.Subscribe<LanguageChangedEvent>(OnLanguageChanged);
        }
       
        private void OnLanguageChanged(LanguageChangedEvent evt)
        {
            if (gameObject.activeSelf) OnEnable(); // regenerate list with new language
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<LanguageChangedEvent>(OnLanguageChanged);
        }
        // =================================================================
        // GENERATE
        // =================================================================

        private void GenerateAll()
        {
            ClearItems();
            GenerateSupplies();
            GenerateTraits();
            if (_allItems.Count > 0 ) OnItemClicked(_allItems[currentShopIndex]);
        }

       
        private void GenerateSupplies()
        {
            ClearContainer(_suppliesContainer);
            var save = SaveSystem.Load();

            int traitSlotsBought = save.TraitSlotCount - GameConstants.MAX_TRAIT_SLOTS_DEFAULT;
            int traitSlotsMax    = GameConstants.MAX_TRAIT_SLOTS_LIMIT - GameConstants.MAX_TRAIT_SLOTS_DEFAULT;

            // Trait Slot — MaxLevel = traitSlotsMax, CurrentLevel = how many bought
            AddSupply(new ShopItemData
            {
                Id = "basic_trait_slot", Name = "Trait Slot",
                Icon = traitSlotIcon,
                Description = "Expand your planning with an additional trait slot. More traits active means stronger builds.",
                CurrentLevel = traitSlotsBought, MaxLevel = traitSlotsMax,
                Price = _traitSlotPrice, Category = ShopCategory.Basics,
                isSupply = true,
                LocalizationKey = "trait_slot"
            });

            // Re-Roll — unlimited but show current token stockpile in slots (max display 10)
            AddSupply(new ShopItemData
            {
                Id = "basic_reroll", Name = "Second Thoughts",
                Icon = secondToughtsIcon,
                Description = "Grants one re-roll token. Use during level-up to draw fresh spec card options. Tokens stockpile.",
                CurrentLevel = save.RerollTokens, MaxLevel = 10,
                Price = _rerollPrice, Category = ShopCategory.Basics,
                isSupply = true,
                IsUnlimited = false,
                LocalizationKey = "reroll"
            });

            // Meta Bonus
            AddSupply(new ShopItemData
            {
                Id = "basic_meta_bonus", Name = "Coin Magnet",
                Icon = coinMagnetIcon,
                Description = "Permanently increases the meta-currency you earn from each run. Stacks additively.",
                CurrentLevel = save.ShopMetaBonusLevel, MaxLevel = 5,
                Price = _metaBonusPrice, Category = ShopCategory.Basics,
                isSupply = true,
                LocalizationKey = "meta_bonus"
            });

            // XP Multiplier
            AddSupply(new ShopItemData
            {
                Id = "basic_xp_multiplier", Name = "Wisdom Crystal",
                Icon = wisdomCrystalIcon,
                Description = "Each crystal permanently increases all XP gained by 15%. Stack five for +75% XP.",
                CurrentLevel = save.ShopXPMultiplierLevel, MaxLevel = 5,
                Price = _xpMultiplierPrice, Category = ShopCategory.Basics,
                isSupply = true,
                LocalizationKey = "xp"
            });

            // Gold Multiplier
            AddSupply(new ShopItemData
            {
                Id = "basic_gold_multiplier", Name = "Fortune Vault",
                Icon=fortifiedCoreIcon,
                Description = "Permanently adds 15% more gold from enemy kills. Stack five for +75% gold per run.",
                CurrentLevel = save.ShopGoldMultiplierLevel, MaxLevel = 5,
                Price = _goldMultiplierPrice, Category = ShopCategory.Basics,
                isSupply = true,
                LocalizationKey = "gold"
            });

            // Max HP
            AddSupply(new ShopItemData
            {
                Id = "basic_max_hp", Name = "Fortified Core",
                Icon = fortuneVaultIcon,
                Description = "Adds one permanent extra life to every future run. The difference between defeat and victory.",
                CurrentLevel = save.ShopMaxHPLevel, MaxLevel = 5,
                Price = _maxHPPrice, Category = ShopCategory.Basics,
                isSupply = true,
                LocalizationKey = "hp"
            });

            // v1.0 active spell cooldown upgrade (Arcane Focus). Icon left unassigned —
            // Sprite is assigned through the Arcane Focus icon field above.
            AddSupply(new ShopItemData
            {
                Id = "basic_spell_upgrade", Name = "Arcane Focus",
                Icon = arcaneFocusIcon,
                Description = "Permanently reduces your active spell's cooldown by 5%. Stack ten for -50% cooldown.",
                CurrentLevel = save.ShopSpellUpgradeLevel, MaxLevel = 10,
                Price = _spellUpgradePrice, Category = ShopCategory.Basics,
                isSupply = true,
                LocalizationKey = "spell_upgrade"
            });
        }

        private void GenerateTraits()
        {
            ClearContainer(_traitsContainer);
            if (_database?.Traits == null) return;

            var save     = SaveSystem.Load();
            var unlocked = new HashSet<string>(save.UnlockedTraitIds ?? System.Array.Empty<string>());

            foreach (var trait in _database.Traits)
            {
                if (!trait.IsUnlockedByDefault && !unlocked.Contains(trait.Id)) continue;

                int level = GetTraitSaveLevel(save, trait.Id);
                int price = Mathf.RoundToInt(_traitUpgradeBasePrice * Mathf.Pow(_upgradePriceScale, level));

                AddSupply(new ShopItemData
                {
                    Id           = $"trait_{trait.Id}",
                    Name         = trait.DisplayName,
                    Description  = trait.Description,//\n\n{trait.Description}
                    Icon         = trait.Icon,
                    CurrentLevel = level,
                    MaxLevel     = 10,
                    Price        = price,
                    Category     = ShopCategory.Traits,
                    DataId       = trait.Id,
                    LocalizationKey = trait.LocalizationKey
                });
            }
        }

        private void AddSupply(ShopItemData data)
        {
            var go   = Instantiate(_shopIconItemPrefab, data.Category == ShopCategory.Basics
                ? _suppliesContainer : _traitsContainer);
            var item = go.GetComponent<ShopIconItem>();
            if (item == null) item = go.AddComponent<ShopIconItem>();
            item.Setup(data, _selectedBorderColor, _defaultBorderColor, OnItemClicked);
            _allItems.Add(item);
        }

        // =================================================================
        // ITEM CLICKED → INFO
        // =================================================================

        private void OnItemClicked(ShopIconItem clicked)
        {
            _selectedItem?.SetBorderSelected(false);
            _selectedItem = clicked;
            clicked.SetBorderSelected(true);
            _selectedData = clicked.Data;
            currentShopIndex = _allItems.IndexOf(clicked);
            RefreshInfoPanel();
        }

        private void RefreshInfoPanel()
        {
            var data = _selectedData;
            if (data == null) return;

            bool maxed     = !data.IsUnlimited && data.CurrentLevel >= data.MaxLevel;
            bool canAfford = _metaManager != null && _metaManager.MetaCurrency >= data.Price;

            string header = data.isSupply ? "shop_" : "trait_";
            if (_infoIcon        != null) { _infoIcon.sprite = data.Icon; _infoIcon.enabled = data.Icon != null; }
            if (_infoName        != null) _infoName.text        = SOLocalization.GetName(header + data.LocalizationKey, data.Name);
            if (_infoExplanation != null) _infoExplanation.text = SOLocalization.GetDesc(header+ data.LocalizationKey, data.Description);
            if (_infoCost        != null) _infoCost.text        =  maxed ? LocalizationManager.Get("shop_maxed", "MAXED") : $"{data.Price}";

            BuildSlots(data);

            if (_buyButton    != null) _buyButton.interactable = !maxed && canAfford;
            if (_buyButtonText != null)
                _buyButtonText.text = maxed ? LocalizationManager.Get("shop_maxed", "MAXED") : canAfford ? LocalizationManager.Get("shop_purchase", "Purchase") : LocalizationManager.Get("shop_not_enough", "Not Enough");

            // Refund button — visible only if at least one purchase was made
            bool canRefund = data.CurrentLevel > 0
                && data.Category == ShopCategory.Basics
                && data.Id != "basic_reroll"; // re-roll: handled separately below

            // Re-roll special case: refund if tokens > 0
            var save = SaveSystem.Load();
            if (data.Id == "basic_reroll")
                canRefund = save.RerollTokens > 0;

            if (_refundButton != null) _refundButton.gameObject.SetActive(canRefund);
            if (_refundButtonText != null) _refundButtonText.text = LocalizationManager.Get("shop_refund", "Refund");
        }

        // =================================================================
        // SLOTS
        // =================================================================

        private void BuildSlots(ShopItemData data)
        {
            if (_slotsContainer == null || _slotPrefab == null) return;
            for (int i = _slotsContainer.childCount - 1; i >= 0; i--)
                Destroy(_slotsContainer.GetChild(i).gameObject);

            //// Re-roll: show up to 10 stockpiled tokens, all "purchased"
            //if (data.IsUnlimited)
            //{
            //    int display = Mathf.Min(data.CurrentLevel, 10);
            //    for (int i = 0; i < display; i++)
            //    {
            //        var go   = Instantiate(_slotPrefab, _slotsContainer);
            //        var slot = go.GetComponent<ShopSlot>();
            //        if (slot == null) slot = go.AddComponent<ShopSlot>();
            //        slot.SetPurchased(true);
            //    }
            //    return;
            //}

            int max = Mathf.Min(data.MaxLevel, 10);
            for (int i = 0; i < max; i++)
            {
                var go   = Instantiate(_slotPrefab, _slotsContainer);
                var slot = go.GetComponent<ShopSlot>();
                if (slot == null) slot = go.AddComponent<ShopSlot>();
                slot.SetPurchased(i < data.CurrentLevel);
            }
        }

        // =================================================================
        // BUY
        // =================================================================

        private void OnBuyClicked()
        {
            if (_selectedData == null || _metaManager == null) return;
            if (!_selectedData.IsUnlimited && _selectedData.CurrentLevel >= _selectedData.MaxLevel) return;

            bool ok = _selectedData.Category switch
            {
                ShopCategory.Basics => HandleBasicsPurchase(_selectedData),
                ShopCategory.Traits => HandleTraitPurchase(_selectedData),
                _ => false
            };
          
            //            _selectedData = _selectedItem.Data;
            if (ok)
            {
                GenerateAll();
                EventBus.Publish(new ShopItemPurchasedEvent
                {
                    ItemId = _selectedData.Id
                });
            }
      
        }

        private bool HandleBasicsPurchase(ShopItemData item)
        {
            if (_metaManager.MetaCurrency < item.Price) return false;
            var save = SaveSystem.Load();

            switch (item.Id)
            {
                case "basic_trait_slot":
                    return _metaManager.BuyTraitSlot(item.Price);

                case "basic_reroll":
                    if (save.ShopRerollLevel >= 10) return false;
                    if (!_metaManager.SpendMetaCurrency(item.Price)) return false;
                    save.RerollTokens++;
                    SaveSystem.Save(save);
                    return true;

                case "basic_meta_bonus":
                    if (save.ShopMetaBonusLevel >= 5) return false;
                    if (!_metaManager.SpendMetaCurrency(item.Price)) return false;
                    save.ShopMetaBonusLevel++;
                    save.PermanentBonuses[5] += 0.10f;
                    SaveSystem.Save(save);
                    return true;

                case "basic_xp_multiplier":
                    if (save.ShopXPMultiplierLevel >= 5) return false;
                    if (!_metaManager.SpendMetaCurrency(item.Price)) return false;
                    save.ShopXPMultiplierLevel++;
                    save.PermanentBonuses[1] += 0.15f;
                    SaveSystem.Save(save);
                    return true;

                case "basic_gold_multiplier":
                    if (save.ShopGoldMultiplierLevel >= 5) return false;
                    if (!_metaManager.SpendMetaCurrency(item.Price)) return false;
                    save.ShopGoldMultiplierLevel++;
                    save.PermanentBonuses[2] += 0.15f;
                    SaveSystem.Save(save);
                    return true;

                case "basic_spell_upgrade":
                    if (save.ShopSpellUpgradeLevel >= 10) return false;
                    if (!_metaManager.SpendMetaCurrency(item.Price)) return false;
                    save.ShopSpellUpgradeLevel++;
                    SaveSystem.Save(save);
                    return true;

                case "basic_max_hp":
                    if (save.ShopMaxHPLevel >= 5) return false;
                    if (!_metaManager.SpendMetaCurrency(item.Price)) return false;
                    save.ShopMaxHPLevel++;
                    save.PermanentBonuses[0] += 1f;
                    SaveSystem.Save(save);
                    return true;
            }
            return false;
        }

        private bool HandleTraitPurchase(ShopItemData item)
        {
            if (_metaManager.MetaCurrency < item.Price) return false;
            if (item.CurrentLevel >= item.MaxLevel) return false;

            var save  = SaveSystem.Load();
            int level = GetTraitSaveLevel(save, item.DataId);
            if (level >= item.MaxLevel) return false; // cap follows the shop item (10), no separate hardcoded limit

            if (!_metaManager.SpendMetaCurrency(item.Price)) return false;

            SetTraitSaveLevel(save, item.DataId, level + 1);
            SaveSystem.Save(save);
            return true;
        }

        private void OnRefundClicked()
        {
            if (_selectedData == null) return;
            var save = SaveSystem.Load();

            bool refunded = false;

            switch (_selectedData.Id)
            {
                case "basic_trait_slot":
                    if (save.TraitSlotCount <= GameConstants.MAX_TRAIT_SLOTS_DEFAULT) break;
                    save.TraitSlotCount--;
                    save.MetaCurrency += _traitSlotPrice;
                    refunded = true;
                    break;

                case "basic_reroll":
                    if (save.RerollTokens <= 0) break;
                    save.RerollTokens--;
                    save.MetaCurrency += _rerollPrice;
                    refunded = true;
                    break;

                case "basic_meta_bonus":
                    if (save.ShopMetaBonusLevel <= 0) break;
                    save.ShopMetaBonusLevel--;
                    save.PermanentBonuses[5] -= 0.10f;
                    save.MetaCurrency += _metaBonusPrice;
                    refunded = true;
                    break;

                case "basic_xp_multiplier":
                    if (save.ShopXPMultiplierLevel <= 0) break;
                    save.ShopXPMultiplierLevel--;
                    save.PermanentBonuses[1] -= 0.15f;
                    save.MetaCurrency += _xpMultiplierPrice;
                    refunded = true;
                    break;

                case "basic_gold_multiplier":
                    if (save.ShopGoldMultiplierLevel <= 0) break;
                    save.ShopGoldMultiplierLevel--;
                    save.PermanentBonuses[2] -= 0.15f;
                    save.MetaCurrency += _goldMultiplierPrice;
                    refunded = true;
                    break;

                case "basic_max_hp":
                    if (save.ShopMaxHPLevel <= 0) break;
                    save.ShopMaxHPLevel--;
                    save.PermanentBonuses[0] -= 1f;
                    save.MetaCurrency += _maxHPPrice;
                    refunded = true;
                    break;

                default:
                    // Trait upgrade refund
                    if (_selectedData.Category == ShopCategory.Traits
                        && !string.IsNullOrEmpty(_selectedData.DataId))
                    {
                        int level = GetTraitSaveLevel(save, _selectedData.DataId);
                        if (level <= 0) break;
                        int refundPrice = Mathf.RoundToInt(
                            _traitUpgradeBasePrice * Mathf.Pow(_upgradePriceScale, level - 1));
                        SetTraitSaveLevel(save, _selectedData.DataId, level - 1);
                        save.MetaCurrency += refundPrice;
                        refunded = true;
                    }
                    break;
            }

            if (refunded)
            {
                SaveSystem.Save(save);
                EventBus.Publish(new MetaCurrencyChangedEvent
                { Current = save.MetaCurrency, Delta = 0 });
                GenerateAll();
            }
        }
        // =================================================================
        // TRAIT SAVE HELPERS
        // =================================================================

        private static int GetTraitSaveLevel(SaveData save, string traitId)
        {
            if (save.TraitUpgradeIds == null) return 0;
            for (int i = 0; i < save.TraitUpgradeIds.Length; i++)
                if (save.TraitUpgradeIds[i] == traitId)
                    return save.TraitUpgradeLevels != null && i < save.TraitUpgradeLevels.Length
                        ? save.TraitUpgradeLevels[i] : 0;
            return 0;
        }

        private static void SetTraitSaveLevel(SaveData save, string traitId, int level)
        {
            var ids    = new List<string>(save.TraitUpgradeIds   ?? System.Array.Empty<string>());
            var levels = new List<int>   (save.TraitUpgradeLevels ?? System.Array.Empty<int>());

            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] != traitId) continue;
                levels[i] = level;
                save.TraitUpgradeIds    = ids.ToArray();
                save.TraitUpgradeLevels = levels.ToArray();
                return;
            }

            ids.Add(traitId);
            levels.Add(level);
            save.TraitUpgradeIds    = ids.ToArray();
            save.TraitUpgradeLevels = levels.ToArray();
        }

        // =================================================================
        // HELPERS
        // =================================================================

        private void ClearItems()
        {
            for (int i = _allItems.Count - 1; i >= 0; i--)
                if (_allItems[i] != null) Destroy(_allItems[i].gameObject);
            _allItems.Clear();
            _selectedItem = null;
        }

        private void ClearContainer(Transform t)
        {
            if (t == null) return;
            for (int i = t.childCount - 1; i >= 0; i--) Destroy(t.GetChild(i).gameObject);
        }
    }

    
    // =========================================================================
    // DATA
    // =========================================================================
    public enum ShopCategory { Basics, Traits }
    [CreateAssetMenu(fileName = "New Shop Item", menuName = "ETD/Shop Data")]
    public class ShopItemData
    {
        public string Id;
        public string Name;
        public string Description;
        public Sprite Icon;
        public int    CurrentLevel;
        public int    MaxLevel;
        public int    Price;
        public bool   IsUnlimited;    // re-roll: no max, shows stockpile in slots
        public ShopCategory Category;
        public bool isSupply;
        public string DataId;
        [Header("Localization")]
        [Tooltip("Base key used for all localized fields. E.g. 'trait_tr001'")]
        public string LocalizationKey;
    }
}
