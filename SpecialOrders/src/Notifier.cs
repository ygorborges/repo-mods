using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpecialOrders
{
    internal static class Notifier
    {
        internal static string ItemName(Item item)
        {
            try
            {
                if (item.itemNameLocalized != null)
                {
                    string localized = item.itemNameLocalized.GetLocalizedString();
                    if (!string.IsNullOrEmpty(localized))
                    {
                        return localized;
                    }
                }
            }
            catch (Exception)
            {
            }
            return item.itemName;
        }

        internal static void Arrived(string[] keys)
        {
            if (keys == null || keys.Length == 0 || Plugin.Instance == null)
            {
                return;
            }
            Plugin.Instance.StartCoroutine(ArrivedRoutine(keys));
        }

        private static IEnumerator ArrivedRoutine(string[] keys)
        {
            float waited = 0f;
            while ((GameDirector.instance == null || GameDirector.instance.currentState != GameDirector.gameState.Main) && waited < 30f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return new WaitForSeconds(3f);

            try
            {
                List<string> names = new List<string>();
                foreach (string key in keys)
                {
                    Item item;
                    names.Add(StatsManager.instance.itemDictionary.TryGetValue(key, out item) ? ItemName(item) : key);
                }
                Color gold = new Color(1f, 0.82f, 0.29f);
                SemiFunc.UIFocusText("SPECIAL ORDER ARRIVED: " + string.Join(", ", names.ToArray()), gold, Color.white, 4f);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Could not show the arrival message: " + ex.Message);
            }
        }
    }
}
