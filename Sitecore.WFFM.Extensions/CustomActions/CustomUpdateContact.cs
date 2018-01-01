using Sitecore.Analytics.Tracking;
using Sitecore.Data;
using Sitecore.Diagnostics;
using Sitecore.WFFM.Abstractions.Actions;
using Sitecore.WFFM.Abstractions.Analytics;
using Sitecore.WFFM.Abstractions.Shared;
using Sitecore.WFFM.Actions.Base;
using Sitecore.WFFM.Actions.SaveActions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Sitecore.Analytics;
using Sitecore.Analytics.Model;
using Sitecore.Analytics.Data;

namespace Sitecore.WFFM.Extensions.CustomActions
{
    public class CustomUpdateContact : WffmSaveAction
    {
        private readonly IAnalyticsTracker analyticsTracker;
        private readonly IAuthentificationManager authentificationManager;
        private readonly ILogger logger;
        private readonly IFacetFactory facetFactory;

        public string Mapping { get; set; }

        public CustomUpdateContact() { }

        public CustomUpdateContact(IAnalyticsTracker analyticsTracker, IAuthentificationManager authentificationManager, ILogger logger, IFacetFactory facetFactory)
        {
            Assert.IsNotNull((object)analyticsTracker, nameof(analyticsTracker));
            Assert.IsNotNull((object)authentificationManager, nameof(authentificationManager));
            Assert.IsNotNull((object)logger, nameof(logger));
            Assert.IsNotNull((object)facetFactory, nameof(facetFactory));
            this.analyticsTracker = analyticsTracker;
            this.authentificationManager = authentificationManager;
            this.logger = logger;
            this.facetFactory = facetFactory;
        }

        [Obsolete]
        public CustomUpdateContact(IAnalyticsTracker analyticsTracker, IAuthentificationManager authentificationManager, ILogger logger, IFacetFactory facetFactory, IContactManager contactManager)
      : this(analyticsTracker, authentificationManager, logger, facetFactory)
        {
        }

        public override void Execute(ID formId, AdaptedResultList adaptedFields, ActionCallContext actionCallContext = null, params object[] data)
        {
            this.UpdateContact(adaptedFields);
        }

        protected virtual void UpdateContact(AdaptedResultList fields)
        {
            Assert.ArgumentNotNull(fields, "adaptedFields");
            Assert.IsNotNullOrEmpty(this.Mapping, "Empty mapping xml.");
            Assert.IsNotNull(this.analyticsTracker.CurrentContact, "Tracker.Current.Contact");
            this.logger.Warn("[UPDATE CONTACT DETAILS Save action] User is not authenticated to edit contact details.", this);

            IEnumerable<FacetNode> enumerable = this.ParseMapping(this.Mapping, fields);
            IContactFacetFactory contactFacetFactory = this.facetFactory.GetContactFacetFactory();


            // Identifying contact if exist
            var EmailAddressPath = "Emails/Entries/SmtpAddress";
            string EmailAddressValue = string.Empty;
            EmailAddressValue = enumerable.Where(node => node.Path == EmailAddressPath).FirstOrDefault().Value;
            
            Contact currentContact = Tracker.Current.Session.Contact;


            Contact contact = ContactFactory.GetContact(EmailAddressValue);
            ContactRepository contactRepository = Sitecore.Configuration.Factory.CreateObject("tracking/contactRepository", true) as ContactRepository;

            // The data will be transferred from the dyingContact to the survivingContact
            contactRepository.MergeContacts(currentContact, contact);
            
            IdenitifyContact(EmailAddressValue);

            foreach (FacetNode node in enumerable)
            {
                contactFacetFactory.SetFacetValue(currentContact, node.Key, node.Path, node.Value, true);
            }
        }

        private void IdenitifyContact(string EmailAddress)
        {
            try
            {
                if (Sitecore.Analytics.Tracker.Current == null || Sitecore.Analytics.Tracker.Current.Session == null)
                {
                    Sitecore.Diagnostics.Log.Info("Trying to identify visitor by email, But Tracker is not initialized ", this);
                    return;
                }
                if (!Sitecore.Analytics.Tracker.IsActive)
                {
                    Sitecore.Diagnostics.Log.Info("Trying to identify visitor by email, But Tracker is not Active ", this);
                    return;
                }

                if (!string.IsNullOrEmpty(EmailAddress))
                {
                    Sitecore.Diagnostics.Log.Info("Identifying vistor by email : '" + EmailAddress.Trim() + "'", this);
                    Tracker.Current.Session.Identify(EmailAddress.Trim());
                }
            }
            catch (Exception ex)
            {
                Sitecore.Diagnostics.Log.Error(ex.Message, ex, this);
            }
        }

        public IEnumerable<FacetNode> ParseMapping(string mapping, AdaptedResultList adaptedFieldResultList)
        {
            Assert.ArgumentNotNullOrEmpty(mapping, nameof(mapping));
            Assert.ArgumentNotNull((object)adaptedFieldResultList, nameof(adaptedFieldResultList));
            return (IEnumerable<FacetNode>)((object[])new JavaScriptSerializer().Deserialize(mapping, typeof(object))).Cast<Dictionary<string, object>>().Select(item => new
            {
                item = item,
                itemValue = item["value"].ToString()
            }).Select(_param0 => new
            {
                TransparentIdentifier = _param0,
                itemId = !_param0.item.ContainsKey("id") || _param0.item["id"] == null ? "Preferred" : _param0.item["id"].ToString()
            }).Select(_param1 => new
            {
                TransparentIdentifier1 = _param1,
                value = adaptedFieldResultList.GetValueByFieldID(ID.Parse(_param1.TransparentIdentifier.item["key"].ToString()))
            }).Where(_param0 => !string.IsNullOrEmpty(_param0.value)).Select(_param0 => new FacetNode(_param0.TransparentIdentifier1.itemId, _param0.TransparentIdentifier1.TransparentIdentifier.itemValue, _param0.value)).ToList<FacetNode>();
        }
    }
}
