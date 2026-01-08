// smoke test 
// XUnit testing library, kindof like doing: const { test, expect } = require("jest");
// good testing library for ASP.NET Core, as Jest is to Node apps
using Xunit;
// For Structure: DeployentRisk -> API -> Tests (remember C# practices with OOP, Scopes, etc.)
namespace DeploymentRisk.Api.Tests
{
    public class UnitTest1
    {
        // XUnit test/method
        // in Jest it would be: test("description", () => { ... });
        [Fact]
        public void TruthyTest()
        {
            // Assert checks whether a condition is true
            // If this condition is false, the test will fail
            // Like in node: expect(true).toBe(true);
            Assert.True(true);
        }
    }
}
