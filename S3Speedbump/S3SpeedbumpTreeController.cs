using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Trees;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Trees;
using Umbraco.Cms.Web.BackOffice.Trees;
using Umbraco.Cms.Web.Common.Attributes;
using Umbraco.Extensions;

namespace S3.Utilities.S3Speedbump;

[Tree("settings", "speedbumpTree", TreeTitle = "Speedbump", TreeGroup = Constants.Trees.Groups.Settings, SortOrder = 100)]
[PluginController("S3Speedbump")]
public class S3SpeedbumpTreeController : TreeController {

    private readonly IMenuItemCollectionFactory _menuItemCollectionFactory;

    public S3SpeedbumpTreeController(ILocalizedTextService localizedTextService,
        UmbracoApiControllerTypeCollection umbracoApiControllerTypeCollection,
        IMenuItemCollectionFactory menuItemCollectionFactory,
        IEventAggregator eventAggregator)
        : base(localizedTextService, umbracoApiControllerTypeCollection, eventAggregator) {
        _menuItemCollectionFactory = menuItemCollectionFactory ?? throw new ArgumentNullException(nameof(menuItemCollectionFactory));
    }

    protected override ActionResult<TreeNodeCollection> GetTreeNodes(string id, FormCollection queryStrings) {
        return new TreeNodeCollection();
    }

    protected override ActionResult<MenuItemCollection> GetMenuForNode(string id, FormCollection queryStrings) {
        // we don't have any menu item options (such as create/delete/reload) & only use the root node to load a custom UI
        return _menuItemCollectionFactory.Create();
    }

    protected override ActionResult<TreeNode?> CreateRootNode(FormCollection queryStrings) {
        var rootResult = base.CreateRootNode(queryStrings);
        if (!(rootResult.Result is null)) {
            return rootResult;
        }

        var root = rootResult.Value;

        if (root is not null) {
            // points to [web project]/app-plugins/s3speedbump/backoffice/speedbumptree/overview
            root.RoutePath = $"{Constants.Applications.Settings}/speedbumpTree/overview";
            // set the icon
            root.Icon = "icon-stop-hand";
            // could be set to false for a custom tree with a single node.
            root.HasChildren = false;
            // url for menu
            root.MenuUrl = null;
        }

        return root;
    }
}