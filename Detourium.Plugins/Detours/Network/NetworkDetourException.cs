using System;

namespace Detourium.Detours.Network
{
    [Serializable]
    public class NetworkDetourException : Exception
    {
        public NetworkDetourException() { }
        public NetworkDetourException(string message) : base(message) { }
    }
}
