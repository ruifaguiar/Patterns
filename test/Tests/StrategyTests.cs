using Patterns.Strategy;
using Xunit;

namespace Tests
{
    public class StrategyTests
    {
        [Fact]
        public void HighDiscountStrategyTests()
        {
            IDiscountStrategy discountStrategy = new HighDiscountStrategy();
            var mall = new ShoppingMall(discountStrategy);
            mall.CustomerName="Raquel";
            mall.BillAmount=400;
            var discount = mall.GetFinalBill()/mall.BillAmount;
            Assert.Equal(0.5,discount);
        }
    }
}