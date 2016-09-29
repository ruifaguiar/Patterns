using System;

namespace Patterns.Proxy
{
    public interface IReader : IDisposable
    {
        string Read();
    }
}