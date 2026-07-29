using System;
using System.Reflection;
using EntityComponent;
using JumpKing.Player;

namespace RadioControlMod
{
    internal interface IPlayerResolver
    {
        PlayerEntity[] Resolve(string user);
    }

    internal static class PlayerResolver
    {
        private static readonly IPlayerResolver SinglePlayer =
            new SinglePlayerResolver();
        private static IPlayerResolver _current = SinglePlayer;
        private static bool _providerResolved;

        public static void ResolveProvider()
        {
            if (_providerResolved)
            {
                return;
            }

            _providerResolved = true;
            IPlayerResolver optionalResolver =
                ReflectedPlayerResolver.TryCreate();
            if (optionalResolver != null)
            {
                _current = optionalResolver;
            }
        }

        public static PlayerEntity[] Resolve(string user)
        {
            ResolveProvider();
            return _current.Resolve(user);
        }
    }

    internal sealed class SinglePlayerResolver : IPlayerResolver
    {
        public PlayerEntity[] Resolve(string user)
        {
            PlayerEntity player = EntityManager.instance == null ? null :
                EntityManager.instance.Find<PlayerEntity>();
            return player == null ?
                new PlayerEntity[0] : new PlayerEntity[] { player };
        }
    }

    internal sealed class ReflectedPlayerResolver : IPlayerResolver
    {
        private const string ApiTypeName =
            "LocalMultiplayerMod.LocalMultiplayerApi";

        private delegate PlayerEntity[] ResolvePlayersDelegate(string user);

        private readonly ResolvePlayersDelegate _resolvePlayers;

        private ReflectedPlayerResolver(ResolvePlayersDelegate resolvePlayers)
        {
            _resolvePlayers = resolvePlayers;
        }

        public PlayerEntity[] Resolve(string user)
        {
            PlayerEntity[] players = _resolvePlayers(user);
            return players ?? new PlayerEntity[0];
        }

        public static IPlayerResolver TryCreate()
        {
            Type apiType = FindApiType();
            if (apiType == null)
            {
                return null;
            }

            MethodInfo method = apiType.GetMethod(
                "ResolvePlayers",
                BindingFlags.Public | BindingFlags.Static
            );
            if (method == null)
            {
                return null;
            }

            var resolver = Delegate.CreateDelegate(
                typeof(ResolvePlayersDelegate),
                method,
                false
            ) as ResolvePlayersDelegate;
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
