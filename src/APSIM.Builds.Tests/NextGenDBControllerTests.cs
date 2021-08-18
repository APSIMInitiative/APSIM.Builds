using System;
using System.Collections.Generic;
using APSIM.Builds;
using APSIM.Builds.Data;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using APSIM.Builds.Controllers;

namespace APSIM.Builds.Tests
{
    /// <summary>
    /// Unit tests for the <see cref="Upgrade" /> class.
    /// </summary>
    public class NextGenDBControllerTests
    {
        /// <summary>
        /// List of upgrades behind the DB Context. Changes made by the DB
        /// controller will be applied to this list.
        /// </summary>
        private List<Upgrade> upgrades;

        /// <summary>
        /// The DB Controller instance. Database access is mocked out to access
        /// the above list of upgrades.
        /// </summary>
        private NextGenDBController controller;

        public NextGenDBControllerTests()
        {
            upgrades = new List<Upgrade>();
            DbSet<Upgrade> upgradesDbSet = MockDBContext.GetQueryableMockDbSet<Upgrade>(upgrades);

            // This object mocks the DB context.
            var mockDBContext = new Mock<INextGenDbContext>();
            mockDBContext.Setup(p => p.Upgrades).Returns(MockDBContext.GetQueryableMockDbSet<Upgrade>(upgrades));
            mockDBContext.Setup(p => p.SaveChanges()).Returns(1); // number of objects

            // This object mocks the DB context generator.
            var mockDbContextGenerator = new Mock<INextGenDbContextGenerator>();
            mockDbContextGenerator.Setup(p => p.GenerateDbContext()).Returns(mockDBContext.Object);

            // Now, create a new DB controller.
            controller = new NextGenDBController(mockDbContextGenerator.Object);
        }

        [Fact]
        public void TestAddBuild()
        {
            controller.AddBuild(14);
            Assert.Single(upgrades);
            Assert.Equal<uint>(14, upgrades[0].PullRequestNumber);
        }

        [Fact]
        public void TestReleaseUpgrade()
        {
            // Passing in an invalid pull request ID should result in
            // InvalidOperationException.
            Assert.Throws<InvalidOperationException>(() => controller.ReleaseUpgrade(1));
            upgrades.Add(new Upgrade(DateTime.Now, 0, 6, "", ""));

            // Pull Request ID of 1 is still invalid.
            Assert.Throws<InvalidOperationException>(() => controller.ReleaseUpgrade(1));

            // Test the simple case.
            controller.ReleaseUpgrade(6);
            Assert.True(upgrades[0].Released);
            upgrades[0].Released = false;

            // Ensure that when multiple upgrades are present, only the last one with the
            // matching pull request ID is updated.
            upgrades.Add(new Upgrade(DateTime.Now, 0, 99, "", ""));
            upgrades.Add(new Upgrade(DateTime.Now, 0, 6, "", ""));

            controller.ReleaseUpgrade(6);
            Assert.False(upgrades[0].Released);
            Assert.False(upgrades[1].Released);
            Assert.True(upgrades[2].Released);
        }
    }
}