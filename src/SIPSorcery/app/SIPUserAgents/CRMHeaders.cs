namespace SIPSorcery.SIP.App
{
    public class CRMHeaders
    {
        public string PersonName;
        public string CompanyName;
        public string AvatarURL;
        public bool Pending = true;
        public string LookupError;

        public CRMHeaders()
        { }

        public CRMHeaders(string personName, string companyName, string avatarURL)
        {
            PersonName = personName;
            CompanyName = companyName;
            AvatarURL = avatarURL;
            Pending = false;
        }
    }
}