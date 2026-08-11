using JetBrains.Annotations;
using System;
using UnityEngine;

#nullable enable
namespace AzuExtendedPlayerInventory {
    [PublicAPI]
    public class SlotInfo {
        public string[] SlotNames { get; set; } = new string[0];

        public Vector2[] SlotPositions { get; set; } = new Vector2[0];

        public Func<Player, ItemDrop.ItemData?>?[] GetItemFuncs { get; set; } = new Func<Player, ItemDrop.ItemData>[0];

        public Func<ItemDrop.ItemData, bool>?[] IsValidFuncs { get; set; } = new Func<ItemDrop.ItemData, bool>[0];
    }
}