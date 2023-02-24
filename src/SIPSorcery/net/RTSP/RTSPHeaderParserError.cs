namespace SIPSorcery.Net
{
    public enum RTSPHeaderParserError
    {
        None = 0,
        MandatoryColonMissing = 1,
        CSeqMethodMissing = 2,
        CSeqNotValidInteger = 3,
        CSeqEmpty = 4,
    }
}