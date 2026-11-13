using System;
using System.Reflection;

namespace AlmanacClasses.API;

public static class ClassesAPI
{
    private static readonly MethodInfo API_AddExperience;
    private static readonly MethodInfo API_GetLevel;

    public static void AddEXP(int amount)
    {
        API_AddExperience?.Invoke(null, new object[] { amount });
    }

    public static int GetLevel()
    {
        return (int)(API_GetLevel?.Invoke(null, null) ?? 0);
    }

    static ClassesAPI()
    {
        if (Type.GetType("AlmanacClasses.API.API, AlmanacClasses") is not { } api)
        {
            return;
        }

        API_AddExperience = api.GetMethod("AddExperience", BindingFlags.Public | BindingFlags.Static);
        API_GetLevel = api.GetMethod("GetLevel", BindingFlags.Public | BindingFlags.Static);
    }
}
