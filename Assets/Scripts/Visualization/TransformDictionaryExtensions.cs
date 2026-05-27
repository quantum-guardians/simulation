using System.Collections.Generic;
using UnityEngine;

public static class TransformDictionaryExtensions
{
    public static bool TryGetKeyByValue(this Dictionary<int, Transform> map, Transform value, out int key)
    {
        foreach (var kv in map)
        {
            if (kv.Value == value)
            {
                key = kv.Key;
                return true;
            }

            if (value != null && value.IsChildOf(kv.Value))
            {
                key = kv.Key;
                return true;
            }
        }

        key = default;
        return false;
    }
}
