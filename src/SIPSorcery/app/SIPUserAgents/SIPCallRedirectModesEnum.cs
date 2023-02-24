namespace SIPSorcery.SIP.App
{
    public enum SIPCallRedirectModesEnum
    {
        None = 0,
        //Add = 1,        // (option=a)
        //Replace = 2,    // (option=r)
        NewDialPlan = 3,// (option=n)
        Manual = 4,      // (option=m) Means don't do anything with a redirect response. Let the user handle it in their dialplan.
    }
}