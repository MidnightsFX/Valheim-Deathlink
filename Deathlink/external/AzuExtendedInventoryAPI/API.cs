using JetBrains.Annotations;
using System;
using System.Collections.Generic;

#nullable enable
namespace AzuExtendedPlayerInventory {
    [PublicAPI]
    public class API {
        public static event Action<Hud>? OnHudAwake;

        public static event Action<Hud>? OnHudAwakeComplete;

        public static event Action<Hud>? OnHudUpdate;

        public static event Action<Hud>? OnHudUpdateComplete;

        public static event API.SlotAddedHandler? SlotAdded;

        public static event API.SlotRemovedHandler? SlotRemoved;

        public static bool IsLoaded() => false;

        public static bool AddSlot(
          string slotName,
          Func<Player, ItemDrop.ItemData?> getItem,
          Func<ItemDrop.ItemData, bool> isValid,
          int index = -1) {
            return false;
        }

        public static bool RemoveSlot(string slotName) => false;

        public static SlotInfo GetSlots() => new SlotInfo();

        public static SlotInfo GetQuickSlots() => new SlotInfo();

        public static List<ItemDrop.ItemData> GetQuickSlotsItems() => new List<ItemDrop.ItemData>();

        public static int GetAddedRows(int width) => 0;

        public delegate void SlotAddedHandler(string slotName);

        public delegate void SlotRemovedHandler(string slotName);
    }
}
