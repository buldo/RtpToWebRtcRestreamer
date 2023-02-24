using System;

namespace SIPSorcery.Net
{
    public class STUNMessageTypes
    {
        public static STUNMessageTypesEnum GetSTUNMessageTypeForId(int stunMessageTypeId)
        {
            return (STUNMessageTypesEnum)Enum.Parse(typeof(STUNMessageTypesEnum), stunMessageTypeId.ToString(), true);
        }
    }
}