using System;
using System.Globalization;
using System.Linq;
using Sitecore.Analytics.Aggregation.Pipeline;
using Sitecore.Analytics.Model;
using Sitecore.ContentTesting.Analytics.Aggregation.Data.Model.Facts;
using Sitecore.ContentTesting.Configuration;
using Sitecore.Data;
using Sitecore.Diagnostics;
using Sitecore.ContentTesting.Analytics.Aggregation;

namespace Sitecore.Support.ContentTesting.Analytics.Aggregation.Pipeline
{
    /// <summary>
    /// Implements TestConversionsProcessor Processor.
    /// </summary>
    public class TestConversionsProcessor : AggregationProcessor
    {
        #region Private Fields

        /// <summary>The <see cref="TestPages"/> used to filter the visit pages.</summary>
        private readonly TestPages _testPages = null;

        /// <summary>The <see cref="TestPageStatistics"/> used to perform calculations for the test.</summary>
        private readonly TestPageStatistics _testPageStatistics = null;

        /// <summary>The <see cref="IContentTestingFactory"/> to load types from.</summary>
        private readonly ContentTestingFactory _factory = null;

        #endregion Private Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="TestConversionsProcessor"/> class.
        /// </summary>
        public TestConversionsProcessor()
            : this(null, null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TestConversionsProcessor"/> class.
        /// </summary>
        /// <param name="testPages">
        /// The test pages.
        /// </param>
        /// <param name="testPageStatistics">
        /// The test page statistics.
        /// </param>
        /// <param name="factory">The <see cref="IContentTestingFactory"/> to load types from.</param>
        public TestConversionsProcessor([CanBeNull] TestPages testPages, [CanBeNull] TestPageStatistics testPageStatistics, [CanBeNull] ContentTestingFactory factory)
        {
            _testPages = testPages ?? new TestPages();
            _testPageStatistics = testPageStatistics ?? new TestPageStatistics();
            _factory = factory ??  ContentTestingFactory.Instance;
        }

        #endregion Constructors

        /// <summary>
        ///   Executes the TestConversionsProcessor.
        /// </summary>
        /// <param name="args">Context of the pipeline.</param>
        protected override void OnProcess([NotNull] AggregationPipelineArgs args)
        {
            if (!Settings.IsAutomaticContentTestingEnabled)
            {
                return;
            }

            Assert.ArgumentNotNull(args, "args");

            var repo = _factory.GoalDefinitionManager;
            if (repo == null)
            {
                return;
            }

            var visit = args.Context.Visit;

            if ((null == visit) || (null == visit.Pages) || (visit.Pages.Count <= 0))
            {
                return;
            }

            var facts = args.GetFact<TestConversions>();

            // Get list of the first time test pages in the visit
            var firstTimeTestPages = _testPages.GetFirstTimeTestPages(visit.Pages);

            foreach (var page in firstTimeTestPages)
            {
                var pageEvents = _testPages.GetTestPageEvents(page, visit.Pages).ToArray();
                var eventsValue = _testPageStatistics.GetPageEventsValue(pageEvents);

                var goals = from pe in pageEvents
                            where pe.IsGoal &&
                            repo.Get(new ID(pe.PageEventDefinitionId), CultureInfo.InvariantCulture) != null
                            group pe by pe.PageEventDefinitionId
                            into gpe
                            select new
                            {
                                GoalId = gpe.Key,
                                Conversion = gpe.Count()
                            };

                foreach (var goal in goals)
                {
                    var key = GetKey(page, visit, goal.GoalId);
                    var value = GetValue(page, visit, eventsValue, goal.Conversion);

                    facts.Emit(key, value);
                }
            }
        }

        /// <summary>
        /// Get the key for this fact.
        /// </summary>
        /// <param name="pageData">The <see cref="PageData"/> of the page.</param>
        /// <param name="visitData">The <see cref="VisitData"/> of the visit.</param>
        /// <param name="goalId">The ID of the goal that was triggered.</param>
        /// <returns>The generated key.</returns>
        protected virtual TestConversionsKey GetKey([NotNull] PageData pageData, [NotNull] VisitData visitData, Guid goalId)
        {
            Assert.ArgumentNotNull(pageData, "pageData");
            Assert.ArgumentNotNull(visitData, "visitData");

            return new TestConversionsKey
            {
                TestSetId = pageData.MvTest.Id,
                TestValues = pageData.MvTest.Combination,
                GoalId = goalId
            };
        }

        /// <summary>
        /// Get the value for this fact.
        /// </summary>
        /// <param name="pageData">The <see cref="PageData"/> of the page.</param>
        /// <param name="visitData">The <see cref="VisitData"/> of the visit.</param>
        /// <param name="goalValue">The value of the triggered goals.</param>
        /// <param name="conversions">The number of conversions for the goal.</param>
        /// <returns>The generated value.</returns>
        protected virtual TestConversionsValue GetValue([NotNull] PageData pageData, [NotNull] VisitData visitData, long goalValue, int conversions)
        {
            Assert.ArgumentNotNull(pageData, "pageData");
            Assert.ArgumentNotNull(visitData, "visitData");

            return new TestConversionsValue
            {
                Value = goalValue,
                Visits = 1,
                Count = conversions
            };
        }

        /// <summary>
        /// Calculate the value of the goal.
        /// </summary>
        /// <param name="pageData">The current page.</param>
        /// <param name="visitData">The current visit.</param>
        /// <returns>The goal value gained during the visit.</returns>
        [Obsolete("Please, do not use this method. Use TestPageStatistics instead to get value.")]
        protected virtual long CalculateGoalValue([NotNull] PageData pageData, [NotNull] VisitData visitData)
        {
            Assert.ArgumentNotNull(pageData, "pageData");
            Assert.ArgumentNotNull(visitData, "visitData");

            return visitData.Pages.Where(x => x.VisitPageIndex > pageData.VisitPageIndex).SelectMany(x => x.PageEvents).Sum(x => x.Value);
        }
    }
}