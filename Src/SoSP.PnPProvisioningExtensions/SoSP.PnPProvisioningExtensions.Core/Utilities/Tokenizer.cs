﻿using Microsoft.SharePoint.Client;
using System;
using System.Linq;

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
            if (input == null) return input;
            if (input == string.Empty) return input;

            var web = m_Context.Web;
            var fields = web.Fields;
            var lists = web.Lists;

            EnsureData();

            foreach (var list in lists)
            {
                input = input.ReplaceCaseInsensitive(list.Id.ToString(), "{listid:" + list.Title + "}");
                foreach (var view in list.Views.AsEnumerable().Where(v=>!string.IsNullOrWhiteSpace(v.Title))) // Exclude hidden views, since the Pnp engine ignore these views
                {
                    input = input.ReplaceCaseInsensitive(view.Id.ToString(), "{viewid:" + view.Title + "}");
                }
            }
            foreach (var field in fields)
            {
                input = input.ReplaceCaseInsensitive(field.Id.ToString(), "{fieldtitle:" + field.Id + "}");
            }
            input = input.ReplaceCaseInsensitive(web.Url, "{site}");
            input = input.ReplaceCaseInsensitive(web.ServerRelativeUrl, "{site}");
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

                m_Context.Load(
                    web,
                    w => w.Id,
                    w => w.ServerRelativeUrl,
                    w => w.Url
                    );
                m_Context.Load(
                    fields,
                    col => col.Include(
                        f => f.InternalName,
                        f => f.Id
                        )
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