namespace SIPSorcery.Net
{
    /// <summary>
    /// Represents the current status of an SCTP association.
    /// </summary>
    /// <remarks>
    /// The address list items have not been included due to the assumption
    /// they are not relevant for SCTP encapsulated in UDP.
    /// The status data is defined on page 115 of the SCTP RFC
    /// https://tools.ietf.org/html/rfc4960#page-115.
    /// </remarks>
    public struct SctpStatus
    {
        public SctpAssociationState AssociationConnectionState;
        public int ReceiverWindowSize;
        public int CongestionWindowSizes;
        public int UnacknowledgedChunksCount;
        public int PendingReceiptChunksCount;
    }
}