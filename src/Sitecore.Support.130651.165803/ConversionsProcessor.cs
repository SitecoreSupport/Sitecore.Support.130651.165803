
using System;
using System.Collections.Generic;
using System.Globalization;
using Sitecore.Analytics.Core;
using Sitecore.Diagnostics;
using Sitecore.Analytics.Data.Items;
using Sitecore.Analytics.Model;
using Sitecore.Analytics.Aggregation.Data.Model.Facts;
using Sitecore.Data;
using Sitecore.Marketing.Definitions;
using Sitecore.Marketing.Definitions.Goals;
using Sitecore.Analytics.Aggregation.Pipeline;

namespace Sitecore.Support.Analytics.Aggregation.Pipeline
{
    /// <summary>
    /// Aggregation processor for Conversions.
    /// </summary>
    public class ConversionsProcessor : AggregationProcessor
    {
        protected override void OnProcess([NotNull] AggregationPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");

            //
            // Prepare values.
            //

            VisitData visit = args.Context.Visit;

            //
            // Process page events.
            //

            List<Guid> eventTypes = new List<Guid>();
            IDefinitionManager<IGoalDefinition> definitionManager = DefinitionManagerFactory.Default.GetDefinitionManager<IGoalDefinition>();
            Assert.IsNotNull(definitionManager, "definitionManager");

            if ((null != visit.Pages) && (0 < visit.Pages.Count))
            {
                Conversions facts = null;

                foreach (var page in visit.Pages)
                {
                    if ((page.PageEvents != null) && (0 < page.PageEvents.Count))
                    {
                        foreach (var pageEvent in page.PageEvents)
                        {
                            if (pageEvent.PageEventDefinitionId == Guid.Empty)
                            {
                                Log.Warn("[Analytics]: PageEvent ID can't be retrieved. Conversion is skipped.", this);
                                continue;
                            }
                            IGoalDefinition goal = definitionManager.Get(new ID(pageEvent.PageEventDefinitionId), CultureInfo.InvariantCulture);

                            if (goal != null)
                            {
                                //
                                // Update dimensions.
                                //

                                Guid accountId = AggregationProcessor.UpdateAccountDimension(args);
                                Hash32 siteNameId = AggregationProcessor.UpdateSiteNamesDimension(args);
                                Hash32 deviceNameId = AggregationProcessor.UpdateDeviceNamesDimension(args);
                                Hash32 languageId = AggregationProcessor.UpdateLanguagesDimension(args);

                                //
                                // Update facts.
                                //

                                long points = goal.EngagementValuePoints;

                                ConversionsKey key = new ConversionsKey();

                                key.Date = args.DateTimeStrategy.Translate(visit.StartDateTime);
                                key.TrafficType = visit.TrafficType;
                                key.ContactId = visit.ContactId;
                                key.CampaignId = (visit.CampaignId ?? Guid.Empty);
                                key.SiteNameId = siteNameId;
                                key.DeviceNameId = deviceNameId;
                                key.LanguageId = languageId;
                                key.AccountId = accountId;
                                key.GoalId = pageEvent.PageEventDefinitionId;
                                key.ItemId = pageEvent.ItemId;
                                key.GoalPoints = points;
                                ConversionsValue value = new ConversionsValue();

                                //
                                // Each goal event is registered in the fact table, but the
                                // visit count and value is added only once per visit.
                                //

                                bool isFirstOfType = !eventTypes.Contains(pageEvent.PageEventDefinitionId);

                                value.Count = 1;

                                if (true == isFirstOfType)
                                {
                                    value.Visits = 1;
                                    value.Value = visit.Value;

                                    eventTypes.Add(pageEvent.PageEventDefinitionId);
                                }
                                else
                                {
                                    value.Visits = 0;
                                }

                                if (null == facts)
                                {
                                    facts = args.GetFact<Conversions>();
                                }

                                facts.Emit(key, value);
                            }
                        }
                    }
                }
            }
        }
    }
}