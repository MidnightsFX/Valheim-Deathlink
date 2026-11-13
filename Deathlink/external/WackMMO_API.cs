using System;
using System.Reflection;

namespace Deathlink.external
{
    public static class EpicMMOSystem_API
    {
        public static API_State state = API_State.NotReady;
        private static MethodInfo eGetLevel;
        private static MethodInfo eAddExp;

        public enum API_State
        {
            NotReady, NotInstalled, Ready,
        }

        public static int GetLevel()
        {
            Init();
            return eGetLevel != null ? (int)eGetLevel.Invoke(null, null) : 0;
        }

        public static void AddExp(int value)
        {
            Init();
            eAddExp?.Invoke(null, new object[] { value });
        }

        public static void Init()
        {
            if (state is API_State.Ready or API_State.NotInstalled) return;
            if (Type.GetType("EpicMMOSystem.EpicMMOSystem, EpicMMOSystem") == null)
            {
                state = API_State.NotInstalled;
                return;
            }

            // Resolved before claiming Ready, so an EpicMMOSystem update that moves its API leaves this
            // disabled instead of throwing from Awake and taking Deathlink down with it.
            Type actionsMO = Type.GetType("API.EMMOS_API, EpicMMOSystem");
            eGetLevel = actionsMO?.GetMethod("GetLevel", BindingFlags.Public | BindingFlags.Static);
            eAddExp = actionsMO?.GetMethod("AddExp", BindingFlags.Public | BindingFlags.Static);
            state = eGetLevel != null && eAddExp != null ? API_State.Ready : API_State.NotInstalled;
        }
    }
}
