using UnityEngine;

/// <summary>
/// Scriptable Object defining a consumable or battle item's properties and effects.
/// 
/// PURPOSE:
/// Centralizes item configuration separate from inventory logic. Allows designers to quickly create
/// new items without touching code, and enables easy balancing of consumable effects and behavior.
/// 
/// HOW IT WORKS:
/// 1. BASIC INFO: itemID, itemName, icon, and description provide display and lookup data
/// 2. STACKING: skipTurn, isStackable, and maxStackSize control how items behave in inventory slots
/// 3. EFFECTS: consumableEffect enum determines what happens when item is used (heal HP, boost attack, etc.)
/// 4. POWER: effectPower is a multiplier/amount for the chosen effect (e.g., %100 healing, +10 attack boost)
/// 
/// WHAT IT AFFECTS:
/// - InventorySystem.cs: References itemData to display items in UI and determine stacking behavior
/// - BattleSystem.cs: Uses consumableEffect and effectPower to apply item effects during combat
/// - ItemUseLogic.cs: Checks skipTurn flag to determine if using item costs a turn
/// - Player inventory slots: Items are grouped/stacked based on maxStackSize and isStackable properties
/// </summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "Inventory/Item Data")]
public class InventoryItemData : ScriptableObject
{
    [Tooltip("WHAT: Unique identifier for this item type. HOW: Used for save file lookups and quest references. AFFECTS: Item persistence and quest system tracking.")]
    public string itemID;
    
    [Tooltip("WHAT: Display name shown in inventory UI. HOW: Rendered in item lists and details panels. AFFECTS: Player readability and UI presentation.")]
    public string itemName;
    
    [Tooltip("WHAT: Icon sprite displayed in inventory slots. HOW: Rendered in grid UI and item buttons. AFFECTS: Visual identification and UI clarity.")]
    public Sprite icon;
    
    [Tooltip("WHAT: Whether using item costs a turn in battle. HOW: Checked before item consumption in BattleSystem. AFFECTS: Combat flow and action economy.")]
    public bool skipTurn = true;
    
    [Tooltip("WHAT: Whether multiple instances stack in single slot. HOW: Checked during inventory add operation. AFFECTS: Inventory space management and organization.")]
    public bool isStackable;
    
    [Tooltip("WHAT: Maximum quantity per stack for stackable items. HOW: Enforced when adding items to inventory slots. AFFECTS: How many items fit per slot.")]
    public int maxStackSize = 99;
    public ItemCategory category;

    [TextArea]                 
    public string description;

    [Tooltip("WHAT: Type of effect when item is consumed. HOW: Enum determines which game system applies the effect. AFFECTS: What happens when player uses the item (heal/boost/revive).")]
    public ConsumableEffectType consumableEffect;
    
    [Tooltip("WHAT: Magnitude/amount of the effect power. HOW: Applied during item consumption based on effect type. AFFECTS: Healing amount, boost value, duration, etc.")]
    public int effectPower; // amount healed, % healed, buff amount, etc.
}

 public enum ItemCategory
{
    Consumable, 
    KeyItems, 
    SpecialItems, 
    BattleItems, 
    Gear, 
    Currency // if you want accessories
}

public enum ConsumableEffectType
{
    HealHP,
    HealPercent,
    BoostAttack,
    BoostDefense,
    CureEffects,
    Revive,
    None
}

