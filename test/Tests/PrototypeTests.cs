using Xunit;
using Patterns.Prototype;

namespace Tests
{
    public class PrototypeTests
    {
        [Fact]
        public void PrototypeExample()
        {
            var computer = new Computer
            {
                AmountOfCores = 4,
                AmountOfRam = 32,
                CpuFrequency = 3.4m,
                DriveType = "ssd",
                Gpu = new GraphicsCard()
                {
                    AmountOfRam = 16,
                    GpuFrequency = 1.4m
                }
            };

            var computer2 = (Computer)computer.Clone();
            Assert.NotSame(computer,computer2);
        }
    }
}