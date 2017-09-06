using Microsoft.SharePoint.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SoSP.PnPProvisioningExtensions.Core.Utilities
{
    public class Tokenizer
    {
        private readonly ClientContext m_Context;
        private bool m_IsLoaded;

        public Tokenizer(ClientContext context)
        {
            this.m_Context = context;
        }

        public string Tokenize(string input)
        {
            var web = m_Context.Web;
            var fields = web.Fields;
            var lists = web.Lists;

            EnsureData();

            foreach (var list in lists)
            {
                input = input.ReplaceCaseInsensitive(list.Id.ToString(), "{listid:" + Regex.Escape(list.Title) + "}");
                foreach (var view in list.Views)
                {
                    input = input.ReplaceCaseInsensitive(view.Id.ToString(), "{viewid:" + Regex.Escape(view.Title) + "}");

                }
            }
            foreach (var field in fields)
            {
                input = input.ReplaceCaseInsensitive(field.Id.ToString(), "{fieldtitle:" + field.Id + "}");
            }
            input = input.ReplaceCaseInsensitive(web.ServerRelativeUrl, "{siteurl}");
            input = input.ReplaceCaseInsensitive(web.Id.ToString(), "{siteid}");

            return input;
        }

        private void EnsureData()
        {
            if (!m_IsLoaded)
            {
                var web = m_Context.Web;

                var fields = web.Fields;
                var lists = web.Lists;

                m_Context.Load(web, w => w.Id, w => w.ServerRelativeUrl);
                m_Context.Load(
                    fields,
                    col => col.Include(f => f.InternalName, f => f.Id)
                    );
                m_Context.Load(
                    lists,
                    col => col.Include(
                        l => l.Title,
                        l => l.Id,
                        l => l.Views.Include(
                            v => v.Title,
                            v => v.Id
                        )));
                m_Context.ExecuteQueryRetry();

                m_IsLoaded = true;
            }
        }
    }
}
