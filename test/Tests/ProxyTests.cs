using Xunit;
using Patterns.Proxy;

namespace Tests
{
    public class ProxyTests
    {
        [Fact]
        public void ReadFile()
        {
            IReader reader = new FileReader();
            var output = reader.Read();
            Assert.Equal("isto é um teste para exprimentar o exemplo.", output);
        }

    }
}