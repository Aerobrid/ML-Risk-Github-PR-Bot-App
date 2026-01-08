using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using DeploymentRisk.Api.Services;
using DeploymentRisk.Api.Services.Scoring;
using DeploymentRisk.Api.Models;

// unit test
namespace DeploymentRisk.Api.Tests
{
    public class RiskAssessmentServiceTests
    {
        // Simple fake scorer implementing IRiskScorer
        // Returns a deterministic score and a single risk factor
        private class FakeScorer : IRiskScorer
        {
            // only get no set -> read-only properties
            public string Name { get; }
            public bool IsEnabled { get; }
            private readonly double _score;

            // constructor: set name -> enabled flag -> fixed score returned
            public FakeScorer(string name, bool enabled, double score)
            {
                Name = name;
                IsEnabled = enabled;
                _score = score;
            }

            // Return a ScorerResult synchronously (wrapped in a Task)
            public Task<ScorerResult> ScoreAsync(RiskContext context)
            {
                var res = new ScorerResult
                {
                    Score = _score,
                    // simple threshold -> label mapping using ternary operator
                    Level = _score < 0.3 ? "LOW" : _score < 0.6 ? "MEDIUM" : "HIGH",
                    // $"" is string interpolation in C#, interesting
                    RiskFactors = new List<string> { $"score-{_score}" }
                };
                return Task.FromResult(res);
            }
        }

        // XUnit test
        [Fact]
        public async Task AssessAsync_CombinesWeightedScores_CalculatesOverall()
        {
            // Arrange: two scorers with fixed outputs
            var scorers = new List<IRiskScorer>
            {
                new FakeScorer("RuleBased", true, 0.2),
                new FakeScorer("MLModel", true, 0.8)
            };

            // Provide weights via in-memory IConfiguration
            var inMemorySettings = new Dictionary<string, string>
            {
                { "RiskScoring:Weights:RuleBased", "0.5" },
                { "RiskScoring:Weights:MLModel", "0.5" }
            };

            // setup config 
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            // create service with mock logger
            var mockLogger = new Mock<ILogger<RiskAssessmentService>>();
            var service = new RiskAssessmentService(scorers, config, mockLogger.Object);
            // create data for service assessment
            var context = new RiskContext { Owner = "owner", Repo = "repo" };

            // Act (call method to test)
            var result = await service.AssessAsync(context);

            // Assert: numeric score and derived level, plus risk factors present
            // Weighted average: (0.2*0.5 + 0.8*0.5) / (0.5+0.5) = 0.5
            Assert.Equal(0.5, result.OverallScore, 3);
            Assert.Equal("HIGH", result.OverallLevel);
            Assert.Contains("score-0.2", result.AllRiskFactors);
            Assert.Contains("score-0.8", result.AllRiskFactors);
        }
    }
}
