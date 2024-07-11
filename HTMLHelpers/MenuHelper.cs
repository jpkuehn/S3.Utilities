using Microsoft.Extensions.DependencyInjection;
using System;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Web.Common;
using Umbraco.Extensions;

namespace S3.Utilities.HTMLHelpers {
    public class MenuHelper {
        // Naviagation tab fields:
        // MenuCaption, UmbracoNavihide, ShowinTopMenu, ShowInSiteMap, UseJavaScriptCommand, JavaScriptCommand, UseUrlRedirection, UrlRedirection, ItemCssClass

        public static string GetMenuCaption<T>(IPublishedContent? item) {
            var menuCaption = item.Value("MenuCaption");

            if (menuCaption.GetType() == typeof(string)) {
                if (!string.IsNullOrEmpty(menuCaption.ToString())) {
                    return menuCaption.ToString();
                }
            }

            return item.Name;
        }

        /*
        public static string GetMenuCaption<T>(string name, T? menuCaption) {
            if (menuCaption != null) {
                if (menuCaption.GetType() == typeof(string)) {
                    if (!string.IsNullOrEmpty(menuCaption.ToString())) {
                        return menuCaption.ToString();
                    }
                }
            }

            return name;
        }
        */

        public static string GetMenuHref(IPublishedContent? item) {
            if (item.Value<bool>("UseUrlRedirection") == true) {
                if (!string.IsNullOrEmpty(item.Value("UrlRedirection").ToString())) {
                    return item.Value("UrlRedirection").ToString();
                }
            }

            if (item.Value<bool>("UseJavaScriptCommand") == true) {
                if (!string.IsNullOrEmpty(item.Value("JavaScriptCommand").ToString())) {
                    return "#";
                }
            }

            return item.Url();
        }

        public static string InsertMenuOnClick(IPublishedContent? item) {
            if (item.Value<bool>("UseJavaScriptCommand") == true) {
                if (!string.IsNullOrEmpty(item.Value("JavaScriptCommand").ToString())) {
                    return $" onclick=\"{item.Value("JavaScriptCommand").ToString()}\"";
                }
            }

            return string.Empty;
        }

        public static bool IsCurrentPage(int itemId) {
            if (itemId > 0) {
                var accessor = StaticServiceProvider.Instance.GetRequiredService<IUmbracoHelperAccessor>();
                if (accessor.TryGetUmbracoHelper(out var umbracoHelper)) {
                    return (itemId == umbracoHelper.AssignedContentItem.Id);
                }
            }
            return false;
        }

        public static string GetLICssClasses(IPublishedContent? item, string defaultClass, string currentItemClass) {
            string classList = defaultClass;
            if (!string.IsNullOrEmpty(item.Value("ItemCSSClass").ToString())) {
                classList += " " + item.Value("ItemCSSClass").ToString();
            }
            // is item Id the same as the current page (Model.Id)?
            if (IsCurrentPage(item.Id)) {
                classList += $" {currentItemClass}";
            }

            return classList;
        }

        public static string GetAriaCurrent(IPublishedContent? item) {
            return IsCurrentPage(item.Id) ? "aria-current=true" : string.Empty;
        }

    }
}
