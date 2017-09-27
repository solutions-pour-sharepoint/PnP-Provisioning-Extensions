using Microsoft.SharePoint.Client;
using System;
using System.Linq;

namespace SoSP.PnPProvisioningExtensions.Core.Utilities
{
    public static class ListItemHelper
    {
        public static string GetFieldValueAsText(this ListItem item, string fieldName)
        {
            var actualValue = item[fieldName];
            if (actualValue == null)
            {
                return null;
            }

            var valueType = item.ParentList.Fields.GetFieldByInternalName(fieldName).FieldTypeKind;

            switch (valueType)
            {
                case FieldType.URL:
                    var typedValueUrl = (FieldUrlValue)actualValue;
                    return $"{ typedValueUrl.Url },{ typedValueUrl.Description }";
                case FieldType.User:
                    var typedValueUser = actualValue as FieldUserValue;
                    var typedValueUserMulti = actualValue as FieldUserValue[];
                    if (typedValueUser != null)
                    {
                        return typedValueUser.LookupValue;
                    }
                    else
                    {
                        return string.Concat(typedValueUserMulti.Select(u => u.LookupValue));
                    }
                case FieldType.Lookup:
                    var typedValueLookup = actualValue as FieldLookupValue;
                    var typedValueLookupMulti = actualValue as FieldLookupValue[];
                    if (typedValueLookup != null)
                    {
                        return typedValueLookup.LookupId.ToString();
                    }
                    else
                    {
                        return string.Concat(typedValueLookupMulti.Select(u => u.LookupId));
                    }
                default:
                    return Convert.ToString(actualValue);
            }
        }
    }
}