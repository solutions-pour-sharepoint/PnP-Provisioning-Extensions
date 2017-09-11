using Microsoft.SharePoint.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace SoSP.PnPProvisioningExtensions.Core.Utilities
{
    public static class QueryHelper
    {
        public static IEnumerable<IDictionary<string, string>> GetItemsAllFields(List list)
        {
            var writeableFields = list.Fields.Where(
                f => !f.InternalName.StartsWith("_", StringComparison.Ordinal)
                && !f.InternalName.StartsWith("ows", StringComparison.Ordinal)
                && !f.ReadOnlyField
                && !f.Hidden
                && f.FieldTypeKind != FieldType.Attachments
                );

            ListItemCollectionPosition position = null;
            var ctx = list.Context;
            do
            {
                var query = CamlQuery.CreateAllItemsQuery();
                query.ListItemCollectionPosition = position;
                var listItems = list.GetItems(query);
                ctx.Load(listItems);
                ctx.ExecuteQueryRetry();
                position = listItems.ListItemCollectionPosition;
                foreach (var item in listItems)
                {
                    yield return item.FieldValues
                        .Where(i => writeableFields.Any(fld => fld.InternalName == i.Key))
                        .ToDictionary(
                            i => i.Key,
                            i => item.GetFieldValueAsText(i.Key)
                            );
                }
            } while (position != null);
        }
    }
}