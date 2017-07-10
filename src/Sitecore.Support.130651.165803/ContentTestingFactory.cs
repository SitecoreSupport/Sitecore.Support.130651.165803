using System;
using System.Web;
using Sitecore.Configuration;
using Sitecore.ContentTesting.Analytics;
using Sitecore.ContentTesting.Analytics.Calculation;
using Sitecore.ContentTesting.Caching;
using Sitecore.ContentTesting.ContentSearch;
using Sitecore.ContentTesting.Data;
using Sitecore.ContentTesting.Editing;
using Sitecore.ContentTesting.Inspectors;
using Sitecore.ContentTesting.Models;
using Sitecore.ContentTesting.Reports;
using Sitecore.ContentTesting.Rules.ForceWinnerBehavior;
using Sitecore.ContentTesting.Screenshot;
using Sitecore.ContentTesting.Services;
using Sitecore.ContentTesting.Web;
using Sitecore.Data.Items;
using Sitecore.Marketing.Definitions;
using Sitecore.Marketing.Definitions.Goals;
using Sitecore.Reflection;
using Sitecore.ContentTesting;

namespace Sitecore.Support.ContentTesting
{
    public class ContentTestingFactory : IContentTestingFactory
    {
        #region Fields

        private static readonly ContentTestingFactory FactoryInstance;
        private IContentTestStore _contentTestStore;
        private IContentTestPerformanceFactory _contentTestPerformanceFactory;
        private IForcedWinnerBehaviorFactory _forcedWinnerBehaviorFactory = null;
        private ICandidatesInspector _candidatesInspector;
        private IExperienceInspector _experienceInspector;
        private ITestValueInspector _testValueInspector;
        private ITestCandidateInspectionInitiator _startTestOptionsService;
        private IListsService _listsService;
        private IPersonalizationTestStore _personalizationTestStore;
        private IPersonalizationService _personalizationService;

        #endregion Fields

        #region Static

        static ContentTestingFactory()
        {
            FactoryInstance = new ContentTestingFactory();
        }

        public static ContentTestingFactory Instance
        {
            get
            {
                return FactoryInstance;
            }
        }

        #endregion Static

        #region Public Properties

        public virtual ICandidatesInspector CandidatesInspector
        {
            get
            {
                if (_candidatesInspector != null)
                {
                    return _candidatesInspector;
                }

                _candidatesInspector = new CandidatesInspector();
                return _candidatesInspector;
            }
        }

        public virtual IExperienceInspector ExperienceInspector
        {
            get
            {
                if (_experienceInspector != null)
                {
                    return _experienceInspector;
                }

                _experienceInspector = new ExperienceInspector();
                return _experienceInspector;
            }
        }

        public virtual ITestValueInspector TestValueInspector
        {
            get
            {
                if (_testValueInspector != null)
                {
                    return _testValueInspector;
                }

                _testValueInspector = new TestValueInspector();
                return _testValueInspector;
            }
        }

        #endregion Public Properties

        public virtual IContentTestStore ContentTestStore
        {
            get
            {
                return
                  _contentTestStore ??
                    (_contentTestStore = GetObject<IContentTestStore>("contentTestStore", () => new SitecoreContentTestStore()));
            }
        }

        public virtual IContentTestPerformanceFactory ContentTestPerformanceFactory
        {
            get
            {
                return
                  _contentTestPerformanceFactory ??
                  (_contentTestPerformanceFactory = GetObject<IContentTestPerformanceFactory>("contentTestPerformanceFactory", () => new ContentTestPerformanceFactory()));
            }
        }

        public virtual IForcedWinnerBehaviorFactory ForcedWinnerBehaviorFactory
        {
            get
            {
                return
                  _forcedWinnerBehaviorFactory ??
                  (_forcedWinnerBehaviorFactory = GetObject<IForcedWinnerBehaviorFactory>("forceWinnerBehaviorFactory", () => new ForcedWinnerBehaviorFactory()));
            }
        }

        public virtual ITestCandidateInspectionInitiator TestCandidateInspectionInitiator
        {
            get
            {
                return
                  _startTestOptionsService ??
                    (_startTestOptionsService = GetObject<ITestCandidateInspectionInitiator>("startTestOptionsService", () => new TestCandidateInspectionInitiator()));
            }
        }

        public virtual ITestingSearch TestingSearch
        {
            get
            {
                return GetObject<ITestingSearch>("testingSearch", () => new TestingSearch());
            }
        }

        public virtual TestCombinationContextBase GetTestCombinationContext(HttpContextBase httpContext)
        {
            // This object is bound to a single client request.
            if (httpContext.Items["testCombinationContext"] == null)
            {
                var configNode = Factory.GetConfigNode("contentTesting/testCombinationContext");
                if (configNode == null)
                {
                    httpContext.Items["testCombinationContext"] = new TestCombinationContext(httpContext);
                }

                var type = Factory.CreateType(configNode, true);
                httpContext.Items["testCombinationContext"] = ReflectionUtil.CreateObject(type, new object[] { httpContext }) as TestCombinationContextBase;
            }

            return httpContext.Items["testCombinationContext"] as TestCombinationContextBase;
        }

        public virtual TestRunEstimator GetTestRunEstimator(string language, string deviceName)
        {
            TestRunEstimator estimator = null;

            var configNode = Factory.GetConfigNode("contentTesting/testRunEstimator");
            if (configNode == null)
            {
                estimator = new TestRunEstimator(language ?? string.Empty, deviceName ?? string.Empty);
            }
            else
            {
                var type = Factory.CreateType(configNode, true);
                estimator = ReflectionUtil.CreateObject(type, new object[] { language ?? string.Empty, deviceName ?? string.Empty }) as TestRunEstimator;
            }

            return estimator;
        }

        public virtual IListsService ListsService
        {
            get
            {
                return _listsService ??
                  (_listsService = GetObject<IListsService>("personalizationService", () =>
                    new ListsService(ContentTestPerformanceFactory))
                  );
            }
        }

        public virtual IPersonalizationTestStore PersonalizationTestStore
        {
            get
            {
                return
                  _personalizationTestStore ??
                  (_personalizationTestStore = GetObject<IPersonalizationTestStore>("personalizationTestStore", () => new SitecorePersonalizationTestStore()));
            }
        }

        public virtual IPersonalizationService PersonalizationService
        {
            get
            {
                return _personalizationService ??
                  (_personalizationService = GetObject<IPersonalizationService>("personalizationService", () =>
                    new PersonalizationService(ContentTestStore, PersonalizationTestStore, ContentTestPerformanceFactory, TestValueInspector))
                  );
            }
        }

        protected static T GetObject<T>(string configName, Func<T> defaultObject) where T : class
        {
            var configPath = "contentTesting/" + configName;

            var configNode = Factory.GetConfigNode(configPath);
            if (configNode == null)
            {
                return defaultObject();
            }

            // Use this overload so singleInstance attribute can be used
            return Factory.CreateObject(configPath, true) as T;
        }

        public virtual GoalDefinitionManager GoalDefinitionManager
        {
            get
            {
                return DefinitionManagerFactory.Default.GetDefinitionManager<IGoalDefinition>(null) as GoalDefinitionManager;
            }
        }

        public virtual ITestSetFilterer GetTestSetFilterer(TestSet testSet)
        {
            return new TestSetFilterer(testSet, Context.Item, Context.Device);
        }

        public virtual IEditModeContext EditModeContext
        {
            get { return GetObject<IEditModeContext>("editModeContext", () => new EditModeContext()); }
        }

        public virtual ITestingTracker TestingTracker
        {
            get { return GetObject<ITestingTracker>("testingTracker", () => new TestingTracker()); }
        }

        public virtual IPersonalizationTracker PersonalizationTracker
        {
            get { return GetObject<IPersonalizationTracker>("personalizationTracker", () => new PersonalizationTracker()); }
        }

        /// <summary>
        /// Gets the class with specific calculation methods.
        /// </summary>
        /// <returns>The instance of the KPIsCalculation.</returns>
        public virtual KPIsCalculation GetKPIsCalculation()
        {
            return new KPIsCalculation();
        }

        public virtual IScreenshotGenerator ScreenshotGenerator
        {
            get { return GetObject<IScreenshotGenerator>("screenshotGenerator", () => new ScreenshotGenerator()); }
        }

        public virtual IScreenshotContextFactory ScreenshotContextFactory
        {
            get { return GetObject<IScreenshotContextFactory>("screenshotContextFactory", () => new ScreenshotContextFactory()); }
        }

        public virtual IRequestCache<Item, VersionRedirect> VersionRedirectionRequestCache
        {
            get
            {
                return new VersionRedirectionRequestCache();
            }
        }

        public IRequestCache<ActiveTestCacheKey, ItemTestState> ActiveTestCache
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public IDataSourceResolver DataSourceResolver
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public IExperienceAssignor ExperienceAssignor
        {
            get
            {
                throw new NotImplementedException();
            }
        }
    }
}