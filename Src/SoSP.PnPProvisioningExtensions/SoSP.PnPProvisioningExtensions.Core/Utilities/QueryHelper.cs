using Microsoft.SharePoint.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoSP.PnPProvisioningExtensions.Core.Utilities
{
    public static class QueryHelper
    {
        public static IEnumerable<ListItem> GetItems(List list, CamlQuery query)
        {
            ListItemCollectionPosition position = null;
            var ctx = list.Context;
            do
            {
                var listItems = list.GetItems(query);
                ctx.Load(listItems);
                ctx.ExecuteQueryRetry();
                position = listItems.ListItemCollectionPosition;
                foreach (var item in listItems)
                {
                    yield return item;
                }

            } while (position != null);
        }

    }
}
