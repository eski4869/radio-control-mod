using System;
using System.Reflection;
using EntityComponent;
using JumpKing.MiscEntities.WorldItems;
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

    /// <summary>
    /// Per-player item toggles.
    ///
    /// Left, right and jump reach a specific player through that player's own
    /// <see cref="InputComponent" />. Giant Boots and the Snake Ring cannot: the
    /// base game reads them off the single physical pad in <c>GameLoop</c> and
    /// applies them to a global skin list, so there is no per-player channel.
    ///
    /// Routing this mod's per-user boots/snake press through the Local
    /// Multiplayer API gives them one. Player 1 keeps going through the normal
    /// pad path so the vanilla toggle sound and shoe-swap logic still run; only
    /// the additional players, whose presses would otherwise be dropped entirely,
    /// come through here. Without the multiplayer mod present this does nothing.
    /// </summary>
    internal static class MultiplayerItems
    {
        private const string ApiTypeName =
            "LocalMultiplayerMod.LocalMultiplayerApi";

        private delegate bool ToggleItemDelegate(
            PlayerEntity player,
            int item,
            out bool equipped
        );

        private static ToggleItemDelegate _toggleItem;
        private static int _lastResolveAssemblyCount = -1;

        public static bool ToggleBoots(PlayerEntity player)
        {
            return Toggle(player, (int)Items.GiantBoots);
        }

        public static bool ToggleSnake(PlayerEntity player)
        {
            return Toggle(player, (int)Items.SnakeRing);
        }

        private static bool Toggle(PlayerEntity player, int item)
        {
            ResolveApi();
            if (_toggleItem == null || player == null)
            {
                return false;
            }

            bool equipped;
            return _toggleItem(player, item, out equipped);
        }

        private static void ResolveApi()
        {
            if (_toggleItem != null)
            {
                return;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            if (_lastResolveAssemblyCount == assemblies.Length)
            {
                return;
            }

            _lastResolveAssemblyCount = assemblies.Length;
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type apiType = assemblies[i].GetType(ApiTypeName, false);
                if (apiType == null)
                {
                    continue;
                }

                MethodInfo method = apiType.GetMethod(
                    "ToggleItem",
                    BindingFlags.Public | BindingFlags.Static
                );
                if (method != null)
                {
                    _toggleItem = Delegate.CreateDelegate(
                        typeof(ToggleItemDelegate),
                        method,
                        false
                    ) as ToggleItemDelegate;
                }

                return;
            }
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
