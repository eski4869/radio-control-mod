using System;
using System.Reflection;
using EntityComponent;
using JumpKing.Player;

namespace RadioControlMod
{
    internal interface IPlayerResolver
    {
        PlayerEntity Resolve(string user);
    }

    internal static class PlayerResolver
    {
        private static readonly IPlayerResolver SinglePlayer =
            new SinglePlayerResolver();
        private static IPlayerResolver _current = SinglePlayer;
        private static int _lastResolveAssemblyCount = -1;

        public static void ResolveProvider()
        {
            if (!ReferenceEquals(_current, SinglePlayer))
            {
                return;
            }

            int assemblyCount = AppDomain.CurrentDomain.GetAssemblies().Length;
            if (_lastResolveAssemblyCount == assemblyCount)
            {
                return;
            }

            _lastResolveAssemblyCount = assemblyCount;
            IPlayerResolver optionalResolver =
                ReflectedPlayerResolver.TryCreate();
            if (optionalResolver != null)
            {
                _current = optionalResolver;
            }
        }

        public static PlayerEntity Resolve(string user)
        {
            ResolveProvider();
            return _current.Resolve(user);
        }
    }

    internal sealed class SinglePlayerResolver : IPlayerResolver
    {
        public PlayerEntity Resolve(string user)
        {
            return EntityManager.instance == null ? null :
                EntityManager.instance.Find<PlayerEntity>();
        }
    }

    internal sealed class ReflectedPlayerResolver : IPlayerResolver
    {
        private const string ApiTypeName =
            "LocalMultiplayerMod.LocalMultiplayerApi";

        private delegate PlayerEntity ResolvePlayerDelegate(string user);

        private readonly ResolvePlayerDelegate _resolvePlayer;

        private ReflectedPlayerResolver(ResolvePlayerDelegate resolvePlayer)
        {
            _resolvePlayer = resolvePlayer;
        }

        public PlayerEntity Resolve(string user)
        {
            return _resolvePlayer(user);
        }

        public static IPlayerResolver TryCreate()
        {
            Type apiType = FindApiType();
            if (apiType == null)
            {
                return null;
            }

            MethodInfo method = apiType.GetMethod(
                "ResolvePlayer",
                BindingFlags.Public | BindingFlags.Static
            );
            if (method == null)
            {
                return null;
            }

            var resolver = Delegate.CreateDelegate(
                typeof(ResolvePlayerDelegate),
                method,
                false
            ) as ResolvePlayerDelegate;
            return resolver == null ? null :
                new ReflectedPlayerResolver(resolver);
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
    }
}
