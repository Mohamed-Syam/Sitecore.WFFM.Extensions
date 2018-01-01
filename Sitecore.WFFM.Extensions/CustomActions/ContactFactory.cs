using Sitecore.Analytics;
using Sitecore.Analytics.Data;
using Sitecore.Analytics.DataAccess;
using Sitecore.Analytics.Model;
using Sitecore.Analytics.Tracking;
using Sitecore.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sitecore.WFFM.Extensions.CustomActions
{
    public class ContactFactory
    {
        private static ContactRepository contactRepository = Sitecore.Configuration.Factory.CreateObject("tracking/contactRepository", true) as ContactRepository;
        private static ContactManager contactManager = Sitecore.Configuration.Factory.CreateObject("tracking/contactManager", true) as ContactManager;

        public static Contact GetContact(string emailAddress)
        {
            if (IsContactInSession(emailAddress))
            {
                return Tracker.Current.Session.Contact;
            }

            var existingContact = contactRepository.LoadContactReadOnly(emailAddress);

            Contact contact;

            if (existingContact != null)
            {
                LockAttemptResult<Contact> lockResult = contactManager.TryLoadContact(existingContact.ContactId);

                switch (lockResult.Status)
                {
                    case LockAttemptStatus.Success:
                        Contact lockedContact = lockResult.Object;
                        lockedContact.ContactSaveMode = ContactSaveMode.AlwaysSave;
                        contact = lockedContact;
                        break;
                    case LockAttemptStatus.NotFound:
                        contact = Tracker.Current.Session.Contact;
                        break;
                    default:
                        throw new Exception(" Contact could not be locked - " + emailAddress);
                }
            }
            // if Contact in session's identifier is set and not equal to email address create new Contact
            else if (Tracker.Current.Session.Contact.Identifiers.Identifier != null
                && !Tracker.Current.Session.Contact.Identifiers.Identifier.Equals(emailAddress, StringComparison.InvariantCultureIgnoreCase))
            {
                contact = contactRepository.CreateContact(ID.NewID);
                contact.System.Value = 0;
                contact.System.VisitCount = 0;
                contact.ContactSaveMode = ContactSaveMode.AlwaysSave;
            }
            // Contact in session's identifier is null use that Contact
            else
            {
                contact = Tracker.Current.Session.Contact;
            }

            // If the matched Contact is not as the same as the Contact Sitecore loaded into session 
            // or the Contact in session is not identified
            // then we need to call .Identify() - more on this later
            if (!contact.ContactId.Equals(Tracker.Current.Session.Contact.ContactId)
                || (contact.ContactId.Equals(Tracker.Current.Session.Contact.ContactId) && Tracker.Current.Session.Contact.Identifiers.Identifier == null))
            {
                Tracker.Current.Session.Identify(emailAddress);

                // Contact has been updated via Identify so update the reference
                contact = Tracker.Current.Session.Contact;
            }

            return contact;
        }

        private static bool IsContactInSession(string emailAddress)
        {
            var tracker = Tracker.Current;

            if (tracker != null &&
              tracker.IsActive &&
              tracker.Session != null &&
              tracker.Session.Contact != null &&
              tracker.Session.Contact.Identifiers != null &&
              tracker.Session.Contact.Identifiers.Identifier != null &&
              tracker.Session.Contact.Identifiers.Identifier.Equals(emailAddress, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }

            return false;
        }
    }
}
