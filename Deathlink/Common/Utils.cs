using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using static Deathlink.Common.DataObjects;

namespace Deathlink.Common
{
    internal static class Utils
    {

        public static CodeMatcher CreateLabelOffset(this CodeMatcher matcher, out Label label, int offset = 0)
        {
            return matcher.CreateLabelAt(matcher.Pos + offset, out label);
        }

        public static bool PlayerHasUniqueKey(this Player player, string key)
        {
            foreach (string pkey in player.GetUniqueKeys()) {
                if (pkey.StartsWith(key)) { return true; }
            }
            return false;
        }

        public static bool PlayerRemoveUniqueKey(this Player player, string key)
        {
            List<string> keys = player.GetUniqueKeys();
            foreach (string pkey in keys) {
                if (pkey.StartsWith(key)) {
                    player.RemoveUniqueKey(pkey);
                    return true;
                }
            }
            return false;
        }

        public static void SafeInsertOrAppend(this Dictionary<ItemResults, List<ItemDrop.ItemData>> dict, ItemResults key, List<ItemDrop.ItemData> value)
        {
            if (!dict.ContainsKey(key)) {
                dict.Add(key, value);
            } else {
                dict[key].AddRange(value);
            }
        }
        public static void SafeInsertOrAppend(this Dictionary<ItemResults, List<ItemDrop.ItemData>> dict, ItemResults key, ItemDrop.ItemData value)
        {
            if (!dict.ContainsKey(key))
            {
                dict.Add(key, new List<ItemDrop.ItemData>() { value } );
            }
            else
            {
                dict[key].Add(value);
            }
        }
    }
}
