using System;
using System.Collections.Generic;
using System.Linq;

namespace Deathlink;

public static class Extensions
{
    private static readonly List<ItemDrop.ItemData.ItemType> EquipmentTypes = new List<ItemDrop.ItemData.ItemType>()
        {
            ItemDrop.ItemData.ItemType.Chest,
            ItemDrop.ItemData.ItemType.Hands,
            ItemDrop.ItemData.ItemType.Helmet,
            ItemDrop.ItemData.ItemType.Legs,
            ItemDrop.ItemData.ItemType.Bow,
            ItemDrop.ItemData.ItemType.Shield,
            ItemDrop.ItemData.ItemType.Tool,
            ItemDrop.ItemData.ItemType.Shoulder,
            ItemDrop.ItemData.ItemType.Utility,
            ItemDrop.ItemData.ItemType.OneHandedWeapon,
            ItemDrop.ItemData.ItemType.TwoHandedWeapon,
            ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft,
            ItemDrop.ItemData.ItemType.Attach_Atgeir,
            ItemDrop.ItemData.ItemType.Trinket
        };
    public static List<ItemDrop.ItemData> GetEquipment(this List<ItemDrop.ItemData> list)
    {
        return list.Where(x => EquipmentTypes.Contains(x.m_shared.m_itemType) ).ToList();
    }
    
    public static List<ItemDrop.ItemData> GetNotEquipment(this List<ItemDrop.ItemData> list)
    {
        return list.Where(x => !EquipmentTypes.Contains(x.m_shared.m_itemType) ).ToList();
    }

    public static bool IsEquipment(this ItemDrop.ItemData item)
    {
        if (EquipmentTypes.Contains(item.m_shared.m_itemType)) { return true; }
        return false;
    }
}