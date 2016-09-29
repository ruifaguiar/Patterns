using Xunit;
using Patterns.Singleton;


namespace Tests
{
    public class SingletonTests
    {
        [Fact]
        public void HighlanderAreSame()
        {
            Highlander john = Highlander.GetInstance();

            Highlander mark = Highlander.GetInstance();

            Assert.Same(john, mark);
        }
        [Fact]
        public void HighlanderThreadSafeAreSame()
        {
            //here we should try and make two thread create an instance at the same time.

            HighlanderThreadSafe john = HighlanderThreadSafe.GetInstance();

            HighlanderThreadSafe mark = HighlanderThreadSafe.GetInstance();

            Assert.Same(john, mark);
        }

        [Fact]
        public void HighlanderThreadSafeAreSameSimple()
        {
            //here we should try and make two thread create an instance at the same time.

            HighlanderThreadSafeSimpler john = HighlanderThreadSafeSimpler.Instance;

            HighlanderThreadSafeSimpler mark = HighlanderThreadSafeSimpler.Instance;

            Assert.Same(john, mark);
        }
    }


}
