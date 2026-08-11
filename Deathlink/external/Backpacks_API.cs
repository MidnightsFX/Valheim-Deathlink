using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using YamlDotNet.Core.Tokens;
using static Deathlink.external.EpicMMOSystem_API;
using static MeleeWeaponTrail;
using static Mono.Security.X509.X520;

namespace Backpacks;

[PublicAPI]
public static class API {

    public static API_State state = API_State.NotReady;
    private static MethodInfo eCountItemsInBackpacks;
    private static MethodInfo eAddItemToBackpack;
    private static MethodInfo eDeleteItemsFromBackpacks;
    private static MethodInfo eGetEquippedBackpackInventory;
    private static MethodInfo eGetAllBackpackInventories;

    public static int CountItemsInBackpacks(Inventory inventory, string name, bool onlyRemoveable = true) {
        return (int)eCountItemsInBackpacks?.Invoke(null, new object[] { inventory, name, onlyRemoveable });
    }

    public static bool IsItemInBackpacks(Inventory inventory, string name) => CountItemsInBackpacks(inventory, name) > 0;

    public static bool AddItemToBackpack(ItemDrop.ItemData backpack, ItemDrop.ItemData item) {
        return (bool)eAddItemToBackpack?.Invoke(null, new object[] { backpack, item });
    }

    public static bool DeleteItemsFromBackpacks(Inventory inventory, string name, int count = 1) {
        return (bool)eDeleteItemsFromBackpacks?.Invoke(null, new object[] { inventory, name, count });
    }

#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
    public static Inventory? GetEquippedBackpackInventory() => (Inventory)eGetEquippedBackpackInventory?.Invoke(null, new object[] { });
#pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.

    public static List<Inventory> GetAllBackpackInventories(Inventory inventory) {
        return (List<Inventory>)eGetAllBackpackInventories?.Invoke(null, new object[] { inventory });
    }

    public static void Init() {
        if (state is API_State.Ready or API_State.NotInstalled) return;
        if (Type.GetType("Backpacks.API, Backpacks") == null) {
            state = API_State.NotInstalled;
            return;
        }

        state = API_State.Ready;

        Type BackpackAPI = Type.GetType("Backpacks.API, Backpacks");
        eCountItemsInBackpacks = BackpackAPI.GetMethod("CountItemsInBackpacks", BindingFlags.Public | BindingFlags.Static);
        eAddItemToBackpack = BackpackAPI.GetMethod("AddItemToBackpack", BindingFlags.Public | BindingFlags.Static);
        eDeleteItemsFromBackpacks = BackpackAPI.GetMethod("DeleteItemsFromBackpacks", BindingFlags.Public | BindingFlags.Static);
        eGetEquippedBackpackInventory = BackpackAPI.GetMethod("GetEquippedBackpackInventory", BindingFlags.Public | BindingFlags.Static);
        eGetAllBackpackInventories = BackpackAPI.GetMethod("GetAllBackpackInventories", BindingFlags.Public | BindingFlags.Static);
    }

}