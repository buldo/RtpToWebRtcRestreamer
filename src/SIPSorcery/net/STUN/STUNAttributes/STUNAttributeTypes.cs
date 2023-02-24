using System;

namespace SIPSorcery.Net
{
    public class STUNAttributeTypes
    {
        public static STUNAttributeTypesEnum GetSTUNAttributeTypeForId(int stunAttributeTypeId)
        {
            return (STUNAttributeTypesEnum)Enum.Parse(typeof(STUNAttributeTypesEnum), stunAttributeTypeId.ToString(), true);
        }
    }
}