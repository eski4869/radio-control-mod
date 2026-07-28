using System;

namespace RadioControlMod
{
    [Flags]
    internal enum PlayerTargets
    {
        None = 0,
        Player1 = 1,
        Player2 = 2,
        Player3 = 4,
        Player4 = 8
    }
}
