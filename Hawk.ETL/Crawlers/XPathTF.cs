﻿using System;
using Hawk.Core.Utils;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls.WpfPropertyGrid.Attributes;
using Hawk.Core.Connectors;
using Hawk.Core.Utils;
using Hawk.Core.Utils.Logs;
using Hawk.Core.Utils.Plugins;
using Hawk.ETL.Plugins.Transformers;
using HtmlAgilityPack;
using ScrapySharp.Extensions;

namespace Hawk.ETL.Crawlers
{
    [XFrmWork("XPathTF", "XPathTF_desc")]
    public class XPathTF : TransformerBase
    {
        public XPathTF()
        {
            IsManyData = ScriptWorkMode.One;
            SelectorFormat= SelectorFormat.XPath;
            XPath = "";
        }
        [PropertyOrder(3)]
        [LocalizedDisplayName("key_163")]
        public string XPath { get; set; }

        [PropertyOrder(2)]
        [LocalizedDisplayName("key_188")]
        [LocalizedDescription("key_189")]
        public ScriptWorkMode IsManyData { get; set; }

        [LocalizedCategory("key_190")]
        [LocalizedDisplayName("key_191")]
        [LocalizedDescription("key_192")]
        public bool GetText { get; set; }


        [PropertyOrder(0)]
        [LocalizedDisplayName("key_162")]
        [LocalizedDescription("")]

        public SelectorFormat SelectorFormat { get; set; }

        [PropertyOrder(1)]
        [LocalizedDisplayName("key_193")]
        [LocalizedDescription("")]
        public CrawlType CrawlType { get; set; }

     

        protected override IEnumerable<IFreeDocument> InternalTransformManyData(IFreeDocument data)
        {
            var item = data[Column];
            var docu = new HtmlDocument();

            docu.LoadHtml(item.ToString());
           var  path = data.Query(XPath);

            var p2 = docu.DocumentNode.SelectNodes(path, this.SelectorFormat);
            if (p2 == null)
                return new List<IFreeDocument>();
            return p2.Select(node =>
            {
                var doc = new FreeDocument();
               
                 doc.MergeQuery(data, NewColumn);
                doc.SetValue("Text", node.GetNodeText());
                doc.SetValue("HTML", node.InnerHtml);
                doc.SetValue("OHTML", node.OuterHtml);
                return doc;
            });
        }

        public override bool Init(IEnumerable<IFreeDocument> docus)
        {
            IsMultiYield = IsManyData==ScriptWorkMode.List;
            return base.Init(docus);
        }

        public override object TransformData(IFreeDocument document)
        {
            var item = document[Column];

            if (item is IFreeDocument)
            {
                return (item as IFreeDocument).GetDataFromXPath(XPath);
            }
            var docu = new HtmlDocument();

            docu.LoadHtml(item.ToString());
            string path;
            if (GetText)
            {
                 path = docu.DocumentNode.GetTextNode();
                return docu.DocumentNode.GetDataFromXPath(path, CrawlType);
            }
            else
            {
                 path = document.Query(XPath);
                return docu.DocumentNode.GetDataFromXPath(path, CrawlType, SelectorFormat);


            }
          
        }
    }
  
    [XFrmWork("XPathTF2", "xpath2_desc")]
    public class XPathTF2 : ResponseTF
    {
        private Dictionary<string, string> xpaths;

        [Browsable(false)]
        public override string HeaderFilter { get; set; }

        public override bool Init(IEnumerable<IFreeDocument> docus)
        {
            base.Init(docus);
            if (Crawler == null)
                return false;
            IsMultiYield = true;
            xpaths = Crawler.CrawlItems.GroupBy(d => d.Name).Select(d =>
            {
                var column = d.Key;
                var path = XPath.GetMaxCompareXPath(d.Select(d2 => d2.XPath).ToList());
                return new {Column = column, XPath = path};
            }).ToDictionary(d => d.Column, d => d.XPath);
            return true;
        }

        private IEnumerable<IFreeDocument> Get(HtmlDocument docu, IEnumerable<IFreeDocument> source, string name,
            string xpath)
        {
            HtmlNodeCollection nodes;
            try
            {
                nodes = docu.DocumentNode.SelectNodes(xpath);
            }
            catch (Exception ex)
            {
                XLogSys.Print.Warn(GlobalHelper.Get("key_196") + xpath);
                return source;
            }
            if (nodes.Count == 0)
            {
                XLogSys.Print.Warn(GlobalHelper.Get("key_197") + xpath + GlobalHelper.Get("key_198"));
                return source;
            }
            var new_docs = nodes.Select(node =>
            {
                var doc = new FreeDocument();
                doc.Add(name + "_text", node.GetNodeText());
                doc.Add(name + "_ohtml", node.OuterHtml);
                return doc;
            });
            return new_docs.Cross(source);
        }

        protected override IEnumerable<IFreeDocument> InternalTransformManyData(IFreeDocument data)
        {
            {
                var item = data[Column];
                var docu = new HtmlDocument();

                docu.LoadHtml(item.ToString());
                var d = new FreeDocument();
                d.MergeQuery(data, NewColumn);
                IEnumerable<IFreeDocument> source = new List<IFreeDocument> {d};
                source = xpaths.Aggregate(source, (current, xpath) => Get(docu, current, xpath.Key, xpath.Value));
                return source.ToList();
            }
        }
    }
}