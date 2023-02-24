using System;

namespace SIPSorcery.SIP
{
    public static class SIPEventPackageType
    {
        public const string DIALOG_EVENT_VALUE = "dialog";
        public const string MESSAGE_SUMMARY_EVENT_VALUE = "message-summary";
        public const string PRESENCE_EVENT_VALUE = "presence";
        public const string REFER_EVENT_VALUE = "refer";

        public static bool IsValid(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }
            else {
                value = value.Trim();

                return 
                    string.Equals(value, DIALOG_EVENT_VALUE, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, MESSAGE_SUMMARY_EVENT_VALUE, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, PRESENCE_EVENT_VALUE, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, REFER_EVENT_VALUE, StringComparison.OrdinalIgnoreCase);
            }
        }

        public static SIPEventPackagesEnum Parse(string value)
        {
            if (!IsValid(value))
            {
                return SIPEventPackagesEnum.None;
            }
            else
            {
                value = value.Trim().ToLower();
                switch (value)
                {
                    case DIALOG_EVENT_VALUE:
                        return SIPEventPackagesEnum.Dialog;
                    case MESSAGE_SUMMARY_EVENT_VALUE:
                        return SIPEventPackagesEnum.MessageSummary;
                    case PRESENCE_EVENT_VALUE:
                        return SIPEventPackagesEnum.Presence;
                    case REFER_EVENT_VALUE:
                        return SIPEventPackagesEnum.Refer;
                    default:
                        return SIPEventPackagesEnum.None;
                }
            }
        }

        public static string GetEventHeader(SIPEventPackagesEnum eventPackage)
        {
            switch(eventPackage)
            {
                case SIPEventPackagesEnum.Dialog:
                    return DIALOG_EVENT_VALUE;
                case SIPEventPackagesEnum.MessageSummary:
                    return MESSAGE_SUMMARY_EVENT_VALUE;
                case SIPEventPackagesEnum.Presence:
                    return PRESENCE_EVENT_VALUE;
                case SIPEventPackagesEnum.Refer:
                    return REFER_EVENT_VALUE;
                default:
                    return null;
            }
        }
    }
}