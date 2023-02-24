using System;

namespace SIPSorcery.Net
{
    public static class STUNMessageTypes
    {
        public static STUNMessageTypesEnum GetSTUNMessageTypeForId(int stunMessageTypeId)
        {
            return (STUNMessageTypesEnum)Enum.Parse(typeof(STUNMessageTypesEnum), stunMessageTypeId.ToString(), true);
        }
    }
}