using System;

namespace SIPSorcery.Net
{
    public static class STUNAttributeTypes
    {
        public static STUNAttributeTypesEnum GetSTUNAttributeTypeForId(int stunAttributeTypeId)
        {
            return (STUNAttributeTypesEnum)Enum.Parse(typeof(STUNAttributeTypesEnum), stunAttributeTypeId.ToString(), true);
        }
    }
}