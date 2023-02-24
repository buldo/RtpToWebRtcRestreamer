using System;
using SIPSorcery.Sys;

namespace SIPSorcery.SIP
{
    public struct SIPEventDialogStateEvent
    {
        public static SIPEventDialogStateEvent None = new SIPEventDialogStateEvent(null);
        public static SIPEventDialogStateEvent Cancelled = new SIPEventDialogStateEvent("cancelled");
        public static SIPEventDialogStateEvent Error = new SIPEventDialogStateEvent("error");
        public static SIPEventDialogStateEvent LocalBye = new SIPEventDialogStateEvent("local-bye");
        public static SIPEventDialogStateEvent Rejected = new SIPEventDialogStateEvent("rejected");
        public static SIPEventDialogStateEvent Replaced = new SIPEventDialogStateEvent("replaced");
        public static SIPEventDialogStateEvent RemoteBye = new SIPEventDialogStateEvent("remote-bye");
        public static SIPEventDialogStateEvent Timeout = new SIPEventDialogStateEvent("timeout");

        private string m_value;

        private SIPEventDialogStateEvent(string value)
        {
            m_value = value;
        }

        public static bool IsValid(string value)
        {
            if (value.IsNullOrBlank())
            {
                return false;
            }
            else if (value.ToLower() == "cancelled" || value.ToLower() == "error" || value.ToLower() == "local-bye" ||
                     value.ToLower() == "rejected" || value.ToLower() == "replaced" || value.ToLower() == "remote-bye" ||
                     value.ToLower() == "timeout")
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static SIPEventDialogStateEvent Parse(string value)
        {
            if (!IsValid(value))
            {
                throw new ArgumentException("The value is not valid for a SIPEventDialogStateEvent.");
            }
            else
            {
                string trimmedValue = value.Trim().ToLower();
                switch (trimmedValue)
                {
                    case "cancelled":
                        return SIPEventDialogStateEvent.Cancelled;
                    case "error":
                        return SIPEventDialogStateEvent.Error;
                    case "local-bye":
                        return SIPEventDialogStateEvent.LocalBye;
                    case "rejected":
                        return SIPEventDialogStateEvent.Rejected;
                    case "replaced":
                        return SIPEventDialogStateEvent.Replaced;
                    case "remote-bye":
                        return SIPEventDialogStateEvent.RemoteBye;
                    case "timeout":
                        return SIPEventDialogStateEvent.Timeout;
                    default:
                        throw new ArgumentException("The value is not valid for a SIPEventDialogStateEvent.");
                }
            }
        }

        public override string ToString()
        {
            return m_value;
        }

        public override bool Equals(object obj)
        {
            return AreEqual(this, (SIPEventDialogStateEvent)obj);
        }

        public static bool AreEqual(SIPEventDialogStateEvent x, SIPEventDialogStateEvent y)
        {
            return x == y;
        }

        public static bool operator ==(SIPEventDialogStateEvent x, SIPEventDialogStateEvent y)
        {
            return x.m_value == y.m_value;
        }

        public static bool operator !=(SIPEventDialogStateEvent x, SIPEventDialogStateEvent y)
        {
            return !(x == y);
        }

        public override int GetHashCode()
        {
            return m_value.GetHashCode();
        }
    }
}