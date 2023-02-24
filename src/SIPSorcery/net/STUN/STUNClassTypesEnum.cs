namespace SIPSorcery.Net
{
    /// <summary>
    /// The class is interpreted from the message type. It does not get explicitly
    /// set in the STUN header.
    /// </summary>
    public enum STUNClassTypesEnum
    {
        Request = 0,
        Indication = 1,
        SuccessResponse = 2,
        ErrorResponse = 3,
    }
}