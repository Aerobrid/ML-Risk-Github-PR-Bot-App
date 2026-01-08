using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using DeploymentRisk.Api.Controllers;
using DeploymentRisk.Api.Models.Entities;
using DeploymentRisk.Api.Repositories;

namespace DeploymentRisk.Api.Tests
{
    public class DashboardControllerTests
    {
        [Fact]
        public async Task GetRecentAssessments_ReturnsOkWithList()
        {
            var mockRepo = new Mock<IRiskRepository>();
            mockRepo.Setup(r => r.GetRecentAssessmentsAsync(100))
                .ReturnsAsync(new List<RiskAssessmentEntity> { new RiskAssessmentEntity { RepositoryFullName = "owner/repo" } });
            // create controller with mock (fake) repo + logger
            var mockLogger = new Mock<ILogger<DashboardController>>();
            var controller = new DashboardController(mockRepo.Object, mockLogger.Object);
            // execute controller
            var result = await controller.GetRecentAssessments();
            // assert response header (HTTP 200 OK)
            var ok = Assert.IsType<OkObjectResult>(result);
            // assert response body
            var list = Assert.IsAssignableFrom<IEnumerable<RiskAssessmentEntity>>(ok.Value);
        }

        [Fact]
        public async Task GetStatistics_ComputesCountsAndAverage()
        {
            var mockRepo = new Mock<IRiskRepository>();
            var data = new List<RiskAssessmentEntity>
            {   
                // Fake Assessments
                new RiskAssessmentEntity { RiskLevel = "LOW", OverallRiskScore = 0.1 },
                new RiskAssessmentEntity { RiskLevel = "HIGH", OverallRiskScore = 0.9 },
                new RiskAssessmentEntity { RiskLevel = "MEDIUM", OverallRiskScore = 0.4 }
            };
            // whenever called -> return assessment data
            mockRepo.Setup(r => r.GetRecentAssessmentsAsync(1000)).ReturnsAsync(data);

            var mockLogger = new Mock<ILogger<DashboardController>>();
            var controller = new DashboardController(mockRepo.Object, mockLogger.Object);

            var result = await controller.GetStatistics();

            var ok = Assert.IsType<OkObjectResult>(result);

            // `!` tells the compiler value is NOT null
            var statsObj = ok.Value!;
            // runtime type info
            var t = statsObj.GetType();
            // properties
            var totalProp = t.GetProperty("Total");
            var lowProp = t.GetProperty("LowRisk");
            var medProp = t.GetProperty("MediumRisk");
            var highProp = t.GetProperty("HighRisk");
            var critProp = t.GetProperty("CriticalRisk");
            
            // assert/verify properties exist
            Assert.NotNull(totalProp);
            Assert.NotNull(lowProp);
            Assert.NotNull(medProp);
            Assert.NotNull(highProp);
            Assert.NotNull(critProp);
            
            // verify expected count
            Assert.Equal(3, (int)totalProp.GetValue(statsObj)!);
            Assert.Equal(1, (int)lowProp.GetValue(statsObj)!);
            Assert.Equal(1, (int)medProp.GetValue(statsObj)!);
            Assert.Equal(1, (int)highProp.GetValue(statsObj)!);
            Assert.Equal(0, (int)critProp.GetValue(statsObj)!);
        }
    }
}
