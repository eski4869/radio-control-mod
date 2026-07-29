using System;
using System.Reflection;
using JumpKing.Player;

namespace RadioControlMod
{
    internal static class LocalMultiplayerClient
    {
        private const string ApiTypeName =
            "LocalMultiplayerMod.LocalMultiplayerApi";

        private delegate int GetPlayerCountDelegate();
        private delegate int ResolvePlayerMaskDelegate(string user);
        private delegate void SubmitInputDelegate(
            int playerNumber,
            InputComponent.State held,
            InputComponent.State pressed
        );

        private static bool _resolved;
        private static GetPlayerCountDelegate _getPlayerCount;
        private static ResolvePlayerMaskDelegate _resolvePlayerMask;
        private static SubmitInputDelegate _submitInput;

        public static void Resolve()
        {
            if (_resolved)
            {
                return;
            }

            _resolved = true;
            Type apiType = FindApiType();
            if (apiType == null)
            {
                return;
            }

            _getPlayerCount = CreateDelegate<GetPlayerCountDelegate>(
                apiType,
                "GetPlayerCount"
            );
            _resolvePlayerMask = CreateDelegate<ResolvePlayerMaskDelegate>(
                apiType,
                "ResolvePlayerMask"
            );
            _submitInput = CreateDelegate<SubmitInputDelegate>(
                apiType,
                "SubmitInput"
            );
        }

        public static int GetPlayerCount()
        {
            Resolve();
            return _getPlayerCount == null ? 1 : _getPlayerCount();
        }

        public static int ResolvePlayerMask(string user)
        {
            Resolve();
            if (_resolvePlayerMask != null)
            {
                return _resolvePlayerMask(user);
            }

            return 1;
        }

        public static void SubmitInputStates()
        {
            Resolve();
            if (_submitInput == null)
            {
                return;
            }

            Submit(2, PlayerTargets.Player2);
            Submit(3, PlayerTargets.Player3);
            Submit(4, PlayerTargets.Player4);
        }

        private static void Submit(int playerNumber, PlayerTargets target)
        {
            _submitInput(
                playerNumber,
                RadioVirtualInput.GetPlayerState(target, false),
                RadioVirtualInput.GetPlayerState(target, true)
            );
        }

        private static Type FindApiType()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(ApiTypeName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static T CreateDelegate<T>(Type apiType, string methodName)
            where T : class
        {
            MethodInfo method = apiType.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static
            );
            return method == null ? null :
                Delegate.CreateDelegate(typeof(T), method, false) as T;
        }
    }
}
